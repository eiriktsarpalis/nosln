module NoSln.Core

// Simple tool for generating Solution files out of a list of projects.

open System
open System.Collections.Generic
open System.IO
open System.Xml.Linq
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing
open Fake.IO.Globbing.Operators

open NoSln

/// Folder name used for placing files that are at the base directory root folder
let rootFilesFolder = ["Solution Items"]
/// folder name used for placing transitive project dependencies outside of the solution context
let transitiveProjectSeparator = "[..]"
/// files to be always excluded from globbing
let excludedFiles = [ "**/.vs/**/*" ; "**/.idea/**/*" ; "**/.git/**/*" ; "**/.store/**/*" ]

/// gets the file path to be used within the solution file
let getFilePath (config : Configuration) (fullPath : string) =
    let path =
        if config.useAbsolutePaths then fullPath
        else Path.GetRelativePath(config.targetSolutionDir, fullPath)

    // Paths in solution file always separated by backslash, regardless of OS
    Path.toBackSlashSeparators path

/// extracts the solution path (i.e. location of item within nested logical folders) from a given filesystem full path
let getLogicalPath (config : Configuration) (fullPath : string) =
    let relativePath = Path.GetRelativePath(config.baseDirectory, fullPath)
    let slnPath = Path.GetDirectoryName relativePath
    match slnPath.Split Path.DirectorySeparatorChar with
    | [| x |] when String.IsNullOrEmpty x -> []
    | path -> path |> Seq.map (function ".." -> transitiveProjectSeparator | p -> p) |> Seq.toList

/// creates an empty solution folder
let mkSln() : Solution = { id = Guid.NewGuid() ; folders = Map.Empty ; projects = [] }

/// creates a new project node from project file path
let mkProject (config : Configuration) (fullPath : string) : Project =
    let projectDir = Path.GetDirectoryName fullPath
    let name = Path.GetFileNameWithoutExtension fullPath
    let path = getFilePath config fullPath
    let logicalPath = 
        match getLogicalPath config fullPath with
        | [] -> []
        | p -> List.take (p.Length - 1) p // do not include project folders in logical paths

    let xdocument = XDocument.Load fullPath

    let p2pReferences =
        xdocument.Root.Descendants()
        |> Seq.filter (fun n -> n.Name.LocalName = "ProjectReference")
        |> Seq.map (fun n -> n.Attribute(XName.op_Implicit "Include").Value)
        |> Seq.map (fun r ->
            let fullPath = Path.getFullPathXPlat(projectDir @@ r)
            if File.Exists fullPath then fullPath
            else failwithf "project %A contains p2p reference %A which was not found" fullPath r)
        |> Seq.toList

    let isLegacyProject = xdocument.Root.Attributes() |> Seq.exists (fun attr -> attr.Name.LocalName = "Sdk") |> not

    let projType =
        match Path.GetExtension(fullPath).ToLower() with
        | ".fsproj" -> if isLegacyProject then FsProjLegacy else FsProj
        | ".csproj" -> if isLegacyProject then CsProjLegacy else CsProj
        | ".vbproj" -> if isLegacyProject then VbProjLegacy else VbProj
        | _ -> Unrecognized

    { id = Guid.NewGuid() ; 
      projectType = projType ; 
      p2pReferences = p2pReferences ; 
      content = xdocument ; 
      name = name ; 
      path = path ; 
      logicalPath = logicalPath ; 
      fullPath = fullPath }

/// creates a new file node from file path
let mkFile (config : Configuration) (fullPath : string) : File =
    let path = getFilePath config fullPath
    let logicalPath = 
        match getLogicalPath config path with
        | [] -> rootFilesFolder
        | p -> p

    { id = path ; path = path ; logicalPath = logicalPath ; fullPath = fullPath }

/// Calculates the transitive closure of project-2-project references
let getTransitiveClosure (config : Configuration) (projects : seq<Project>) =
    let remaining = Queue projects
    let visited = HashSet<string>()
    let projects = Dictionary(seq { for p in projects -> KeyValuePair(p.fullPath, p) })

    while remaining.Count > 0 do
        let proj = remaining.Dequeue()
        if visited.Add proj.fullPath then
            for r in proj.p2pReferences |> Seq.filter (not << projects.ContainsKey) do
                let proj = mkProject config r
                remaining.Enqueue proj
                projects.Add(proj.fullPath, proj)
        
    Seq.toList projects.Values

/// calculates topological ordering of supplied project files
let getTopologicalOrdering (projects : seq<Project>) =
    let index = projects |> Seq.map (fun p -> p.fullPath, p) |> dict

    let graph =
        index.Values
        // prune p2p references not part of dependency graph
        |> Seq.map (fun p -> p.fullPath, p.p2pReferences |> List.filter index.ContainsKey)
        |> Seq.toList

    match Graph.tryGetTopologicalOrdering graph with
    | Ok sorted -> sorted |> List.map (fun k -> index.[k])
    | Error _ -> Seq.toList projects // silently ignore depedencies that are not DAGs. Return original ordering.

let mkProjects (config : Configuration) (projects : string list) =
    let parsedProjects = projects |> List.map (mkProject config)
    if config.noTransitiveProjects then parsedProjects
    else getTransitiveClosure config parsedProjects
    
/// general-purpose function that updates solution folder within a given path using existing solution
/// if path has not been created yet, it will be populated recursively
let updateFolder (solution : Solution) (path : string list) (updater : Folder -> Folder) =
    let inline getNestedFolder (folders : Map<string, Folder>) (name : string) =
        match folders.TryGetValue name with
        | true, folder -> folder
        | false, _ -> { id = Guid.NewGuid() ; name = name ; folders = Map.Empty ; projects = [] ; files = [] }

    let inline insertFolder (folders : Map<string, Folder>) (folder : Folder) = folders.SetItem(folder.name, folder)

    match path with
    | [] -> failwith "internal error: updateFolder solution path cannot be empty"
    | next :: rest ->
        let rec aux (folder : Folder) path =
            match path with
            | [] -> updater folder
            | next :: rest ->
                let nested = getNestedFolder folder.folders next
                let updated = aux nested rest
                { folder with folders = insertFolder folder.folders updated }

        let nested = getNestedFolder solution.folders next
        let updated = aux nested rest
        { solution with folders = insertFolder solution.folders updated }

/// inserts a new project into existing solution tree
let insertProject (config : Configuration) (sln : Solution) (project : Project) =
    if config.flattenProjects || List.isEmpty project.logicalPath then
        { sln with projects = project :: sln.projects }
    else
        updateFolder sln project.logicalPath (fun f -> { f with projects = project :: f.projects })

/// inserts a new file into existing solution tree
let insertFile (config : Configuration) (sln : Solution) (file : File) =
    if config.flattenProjects then sln 
    else
        updateFolder sln file.logicalPath (fun f -> { f with files = file :: f.files})

/// main function: creates a solution tree from given configuration set
let createSolution (config : Configuration) =
    let projectsUnderBaseDir = !! (config.baseDirectory @@ "**/*.??proj") |> Seq.toList

    let projects =
        match config.projectIncludes, config.projectExcludes with
        | [], [] -> projectsUnderBaseDir
        | includes, excludes ->
            let pattern =
                { BaseDirectory = config.baseDirectory
                  Includes = includes
                  Excludes = excludes }

            projectsUnderBaseDir |> List.filter pattern.IsMatch

    let files =
        if config.noFiles then []
        else
            // exclude files within project directories from solution items
            let projectFileExcludes =
                projectsUnderBaseDir 
                |> Seq.map (fun p -> Path.GetDirectoryName p)
                |> Seq.map (fun p -> Path.GetRelativePath(config.baseDirectory, p))
                |> Seq.map (fun p -> p @@ "**/*")
                |> Seq.toList

            let cliFileIncludes =
                { BaseDirectory = config.baseDirectory
                  Includes = match config.fileIncludes with [] -> ["**/*"] | es -> es
                  Excludes = excludedFiles @ config.fileExcludes @ projectFileExcludes }

            let gitIgnorePatterns = config.gitIgnoreFile |> Option.map GitIgnore.parse

            match gitIgnorePatterns with
            | None -> Seq.toList cliFileIncludes
            | Some g -> 
                // we apply .gitignore as a separate globbing pattern as relative paths might be different
                cliFileIncludes |> Seq.filter g.IsMatch |> Seq.toList

    if config.debug then
        Console.logf "Configuration: %A" config
        Console.logf "Projects: %A" projects
        Console.logf "Files: %A" files

    let parsedProjects = mkProjects config projects
    let parsedFiles = files |> List.map (mkFile config)

    let mutable sln = mkSln()
    for project in parsedProjects do sln <- insertProject config sln project
    for file in parsedFiles do sln <- insertFile config sln file
    sln