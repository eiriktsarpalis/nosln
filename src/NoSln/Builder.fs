module internal NoSln.Builder

// Simple tool for generating solution files out of a list of projects.

open System
open System.Collections.Generic
open System.IO
open System.Xml.Linq
open NoSln

type SolutionConfiguration =
    {
        baseDirectory : string
        targetSolutionFile : string

        projects : string list
        files : string list

        includeTransitiveReferences : bool
        flattenSolutionFolders : bool
        useAbsolutePaths : bool
    }

/// creates a solution file contained in supplied directory
let mkSolutionFileForDirectory (baseDirectory : string) =
    let fileName = Path.GetFileName baseDirectory + ".slnx"
    Path.Combine(baseDirectory, fileName)

/// Generates a random solution file in the system temp directory
let mkTempSolutionfile (baseDirectory : string) =
    let tempPath = Path.GetTempPath()
    let slnIdentifier = Path.GetFileName baseDirectory // get filename of directory
    let randomSuffix = Path.GetRandomFileName() |> Path.GetFileNameWithoutExtension
    let filename = $"{slnIdentifier}-{randomSuffix}.slnx"
    Path.Combine(tempPath, filename)

/// gets the file path to be used within the solution file
let getPathRelativeToSolutionFile (config : SolutionConfiguration) (fullPath : string) =
    let path =
        if config.useAbsolutePaths then fullPath
        else Path.getRelativePath (Path.GetDirectoryName config.targetSolutionFile) fullPath

    // Paths in solution files always use forward slashes, regardless of OS.
    path.Replace('\\', '/')

/// extracts the solution path (i.e. location of item within nested logical folders) from a given filesystem full path
let getLogicalPath (config : SolutionConfiguration) (fullPath : string) =
    if config.flattenSolutionFolders then [] else
    let relativePath = Path.getRelativePath config.baseDirectory fullPath
    let slnPath = Path.GetDirectoryName relativePath
    match slnPath.Split Path.DirectorySeparatorChar with
    | [| x |] when String.IsNullOrEmpty x -> []
    | path -> 
        // '..' folder names not accepted by Visual Studio,
        // replace with something acceptable
        path |> Seq.map (function ".." -> "[..]" | p -> p) |> Seq.toList


/// creates a new project node from project file path
let mkProject (config : SolutionConfiguration) (fullPath : string) : SolutionProject =
    let projectDir = Path.GetDirectoryName fullPath
    let name = Path.GetFileNameWithoutExtension fullPath
    let path = getPathRelativeToSolutionFile config fullPath
    let logicalPath = 
        match getLogicalPath config fullPath with
        | [] -> []
        | p -> List.take (p.Length - 1) p // do not include project folders in logical paths

    let xdocument = 
        try XDocument.Load fullPath
        with :? FileNotFoundException -> throw NoSlnException "project file %A not found." fullPath

    let p2pReferences =
        xdocument.Root.Descendants()
        |> Seq.filter (fun n -> n.Name.LocalName = "ProjectReference")
        |> Seq.map (fun n -> n.Attribute(XName.op_Implicit "Include").Value)
        |> Seq.map (fun r ->
            let combined = Path.Combine(projectDir, r)
            let p2pFullPath = Path.getFullPathXPlat combined
            if File.Exists p2pFullPath then p2pFullPath
            else throw NoSlnException "project %A contains p2p reference %A which was not found" fullPath r)
        |> Seq.toList

    { p2pReferences = p2pReferences ;
      name = name ; 
      relativePath = path ; 
      logicalPath = logicalPath ; 
      fullPath = fullPath }

/// Calculates the transitive closure of project-2-project references
let getTransitiveClosure (config : SolutionConfiguration) (projects : seq<SolutionProject>) =
    let remaining = Queue projects
    let visited = HashSet<string>()
    let projects = projects |> Seq.map (fun p -> p.fullPath, p) |> Dictionary.create

    while remaining.Count > 0 do
        let proj = remaining.Dequeue()
        if visited.Add proj.fullPath then
            for r in proj.p2pReferences |> Seq.filter (not << projects.ContainsKey) do
                let proj = mkProject config r
                remaining.Enqueue proj
                projects.Add(proj.fullPath, proj)
        
    Seq.toList projects.Values

let mkProjects (config : SolutionConfiguration) =
    let parsedProjects = config.projects |> List.map (mkProject config)
    if config.includeTransitiveReferences 
    then getTransitiveClosure config parsedProjects
    else parsedProjects

/// creates a new file node from file path
let mkFile (config : SolutionConfiguration) (fullPath : string) : SolutionFile =
    let path = getPathRelativeToSolutionFile config fullPath
    let logicalPath =
        match getLogicalPath config fullPath with
        | [] -> ["Solution Items"] // files cannot live in solution root
        | p -> p

    { relativePath = path ; logicalPath = logicalPath ; fullPath = fullPath }

let mkFiles (config : SolutionConfiguration) =
    config.files |> List.map (mkFile config)

/// mutable version of the Folder type used for constructing solutions
type FolderBuilder = 
    {
        name : string
        projects : ResizeArray<SolutionProject> ; 
        files : ResizeArray<SolutionFile> ; 
        folders : Dictionary<string, FolderBuilder> 
    }
with
    static member MkEmpty(name : string) = 
        { name = name ; projects = ResizeArray() ; files = ResizeArray() ; folders = Dictionary() }

    /// Maps to immutable folder structure
    member folderB.ToFolder() : SolutionFolder =
        let rec aux (folderB : FolderBuilder) : SolutionFolder =
            let projects = folderB.projects |> Seq.sortBy _.name |> Seq.toList
            let files = folderB.files |> Seq.sortBy _.relativePath |> Seq.toList
            let folders = 
                folderB.folders.Values
                |> Seq.map aux
                |> Seq.sortBy _.name
                |> Seq.toList
            {
                name = folderB.name
                folders = folders
                projects = projects
                files = files
            }

        aux folderB

/// performs an update operation on the appropriate folder based on supplied logical path;
/// will create nested folders as required
let rec updateFolder (updater : FolderBuilder -> unit) (current : FolderBuilder) (path : string list) =
    match path with
    | [] -> updater current
    | next :: tail ->
        let inner =
            match current.folders.TryGetValue next with
            | true, f -> f
            | false, _ ->
                let inner = FolderBuilder.MkEmpty next
                current.folders.Add(next, inner)
                inner

        updateFolder updater inner tail

/// builds a solution tree using provided configuration
let mkSolution (config : SolutionConfiguration) =
    let projects = mkProjects config
    let files = mkFiles config

    let root = FolderBuilder.MkEmpty "root"
    for proj in projects do updateFolder (fun fb -> fb.projects.Add proj) root proj.logicalPath
    for file in files do updateFolder (fun fb -> fb.files.Add file) root file.logicalPath

    let rootFolder = root.ToFolder()

    // root folder should not contain files
    assert rootFolder.files.IsEmpty

    {
        targetSolutionFile = config.targetSolutionFile
        projects = rootFolder.projects
        folders = rootFolder.folders
    }