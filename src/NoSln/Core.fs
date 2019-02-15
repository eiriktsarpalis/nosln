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
/// solution folder used for transitive p2p dependencies that are outside the base directory
let transitiveProjectsFolder : string list = [".external"]
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
    match slnPath.Split Path.DirectorySeparatorChar |> Array.toList with
    | [x] when String.IsNullOrEmpty x -> []
    | path -> path

/// creates an empty solution folder
let mkSln() : Solution = { id = Guid.NewGuid() ; folders = Map.Empty ; projects = [] }

/// creates a new project node from project file path
let mkProject (config : Configuration) (fullPath : string) : Project =
    let name = Path.GetFileNameWithoutExtension fullPath
    let path = getFilePath config fullPath
    let logicalPath = 
        match getLogicalPath config fullPath with
        | [] -> []
        | ".." :: _ -> transitiveProjectsFolder
        | p -> List.take (p.Length - 1) p // do not include project folders in logical paths

    let projType =
        match Path.GetExtension(fullPath).ToLower() with
        | "fsproj" -> FsProj
        | "csproj" -> CsProj
        | "vbproj" -> VbProj
        | _ -> Unrecognized

    { id = Guid.NewGuid() ; projectType = projType ; name = name ; path = path ; logicalPath = logicalPath ; fullPath = fullPath }

/// creates a new file node from file path
let mkFile (config : Configuration) (fullPath : string) : File =
    let path = getFilePath config fullPath
    let logicalPath = 
        match getLogicalPath config path with
        | [] -> rootFilesFolder
        | p -> p

    { id = path ; path = path ; logicalPath = logicalPath ; fullPath = fullPath }

/// Calculates the transitive closure of project-2-project references
let getTransitiveClosure (projects : seq<string>) =
    let getProjectReferences (proj:string) =
        let projectDir = Path.GetDirectoryName proj
        let xdoc = XDocument.Load proj

        xdoc.Root.Descendants(XName.op_Implicit "ProjectReference")
        |> Seq.map (fun n -> n.Attribute(XName.op_Implicit "Include").Value)
        |> Seq.map (fun r ->
            let fullPath = Path.getFullPathXPlat(projectDir @@ r)
            if File.Exists fullPath then fullPath
            else failwithf "project %A contains p2p reference %A which was not found" proj r)
        |> Seq.toArray

    let remaining = Queue projects
    let visited = HashSet<string>()

    while remaining.Count > 0 do
        let proj = remaining.Dequeue()
        if visited.Add proj then
            for r in getProjectReferences proj do
                remaining.Enqueue r
        
    Seq.toList visited
    
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

    let projectsWithAppliedGlobPatterns =
        match config.projectIncludes, config.projectExcludes with
        | [], [] -> projectsUnderBaseDir
        | includes, excludes ->
            let pattern =
                { BaseDirectory = config.baseDirectory
                  Includes = includes
                  Excludes = excludes }

            projectsUnderBaseDir |> List.filter pattern.IsMatch

    let projects =
        if config.noTransitiveProjects
        then projectsWithAppliedGlobPatterns
        else getTransitiveClosure projectsWithAppliedGlobPatterns

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

    let parsedProjects = projects |> List.map (mkProject config)
    let parsedFiles = files |> List.map (mkFile config)

    let mutable sln = mkSln()
    for project in parsedProjects do sln <- insertProject config sln project
    for file in parsedFiles do sln <- insertFile config sln file
    sln