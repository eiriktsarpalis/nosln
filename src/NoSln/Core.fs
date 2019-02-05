module NoSln.Core

// Simple tool for generating Solution files out of a list of projects.

open System
open System.Collections.Generic
open System.IO
open System.Text.RegularExpressions
open Fake.IO

type Solution = Solution of SlnNode list

and SlnNode =
    | Directory of id:Guid * name:string * nodes:SlnNode list * files:string list
    | Project of id:Guid * name:string * path:string

let private projectGuid = Guid.Parse "6EC3EE1D-3C4E-46DD-8F32-0CC8E7565705"
let private directoryGuid = Guid.Parse "2150E333-8FDC-42A3-9474-1A3956D46DE8"

/// Formats the contents of a solution file using a list of projects
let formatSolutionFile (Solution nodes) = 
    seq {
        let fmtGuid (g:Guid) = g.ToString().ToUpper()

        yield ""
        yield "Microsoft Visual Studio Solution File, Format Version 12.00"
        yield "# Visual Studio 15"
        yield "VisualStudioVersion = 15.0.27428.2002"
        yield "MinimumVisualStudioVersion = 10.0.40219.1"

        let projects = ResizeArray<Guid>()
        let nestedNodes = ResizeArray<Guid * Guid>()

        // Declaration Section
        let rec fmtNode (node : SlnNode) = seq {
            match node with
            | Project (id, name, path) ->
                projects.Add id

                yield sprintf """Project("{%s}") = "%s", "%s", "{%s}" """
                            (fmtGuid projectGuid) name path (fmtGuid id)

                yield "EndProject"
        
            | Directory (id, name, nodes, files) ->
                yield sprintf """Project("{%s}") = "%s", "%s", "{%s}" """
                            (fmtGuid directoryGuid) name name (fmtGuid id)

                if not(List.isEmpty files) then
                    yield "\tProjectSection(SolutionItems) = preProject"
                    for file in files do yield sprintf "\t\t%s = %s" file file
                    yield "\tEndProjectSection"

                yield "EndProject"

                for node in nodes do
                    let nodeId = match node with Project(id = id) | Directory(id = id) -> id
                    nestedNodes.Add (nodeId, id)
                    yield! fmtNode node
        }

        for node in nodes do yield! fmtNode node

        // Global Sections
        yield "Global"

        yield "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution"
        yield "\t\tDebug|Any CPU = Debug|Any CPU"
        yield "\t\tRelease|Any CPU = Release|Any CPU"
        yield "\tEndGlobalSection"

        yield "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution"
        for proj in projects do 
            let fmt config platform cfg = 
                sprintf "\t\t{%s}.%s|%s.%s = %s|%s"
                    (fmtGuid proj) config platform cfg config platform

            yield fmt "Debug" "Any CPU" "ActiveCfg"
            yield fmt "Debug" "Any CPU" "Build.0"
            yield fmt "Release" "Any CPU" "ActiveCfg"
            yield fmt "Release" "Any CPU" "Build.0"
        yield "\tEndGlobalSection"

        yield "\tGlobalSection(NestedProjects) = preSolution"
        for child,parent in nestedNodes do yield sprintf "\t\t{%s} = {%s}" (fmtGuid child) (fmtGuid parent)
        yield "\tEndGlobalSection"

        yield "EndGlobal"
    } |> String.concat Environment.NewLine


let createSolutionFromDirectory (solutionFolder : string option) (path : string) =
    let relativizePath =
        match solutionFolder with
        | None -> id
        | Some f -> fun p -> Path.GetRelativePath(f, p)

    let skippedFolders = HashSet [".git" ; ".vs" ; ".store" ]

    let rec walk path =
        let files = Directory.EnumerateFiles path |> Seq.toList
        match files |> List.tryFind (fun f -> f.EndsWith "proj") with
        | Some proj -> Project(Guid.NewGuid(), Path.GetFileNameWithoutExtension proj, relativizePath proj)
        | None ->
            let children = 
                Directory.EnumerateDirectories path 
                |> Seq.filter (fun d -> Path.GetFileName d |> skippedFolders.Contains |> not)
                |> Seq.map walk 
                |> Seq.toList

            let directoryName = Path.GetFileName path
            let relFiles = files |> List.map relativizePath
            Directory(Guid.NewGuid(), directoryName, children, relFiles)
            
    if not (Directory.Exists path) then invalidArg "path" "path does not exist"

    match walk path with
    | Project _ as p -> Solution [p]
    | Directory(nodes = nodes ; files = []) -> Solution(nodes)
    | Directory(id = id; nodes = nodes; files = files) ->
        let slnItems = Directory(id, "Solution Items", [], files)
        Solution(slnItems :: nodes)

let writeSolutionFile (path : string) (contents : string) =
    let dir = Path.GetDirectoryName path
    Directory.ensure dir
    File.WriteAllText(path, contents)

/// Calculates the transitive closure of project-2-project references
let getTransitiveClosure (projects : seq<string>) =
    // TODO replace with `dotnet list reference`
    let p2pRegex = Regex("""<\s*ProjectReference\s+Include\s*=\s*"(.+)"\s*/?>""")
    let getProjectReferences (proj:string) =
        let projectDir = Path.GetDirectoryName proj
        let xml = File.ReadAllText proj

        p2pRegex.Matches(xml) 
        |> Seq.cast<Match> 
        |> Seq.map (fun m -> m.Groups.[1].Value)
        |> Seq.map (fun r -> Path.Combine(projectDir, r))
        |> Seq.map Path.GetFullPath
        |> Seq.toArray

    let remaining = Queue projects
    let visited = HashSet<string>()

    while remaining.Count > 0 do
        let proj = remaining.Dequeue()
        if visited.Add proj then
            for r in getProjectReferences proj do
                remaining.Enqueue r
        
    Seq.toList visited