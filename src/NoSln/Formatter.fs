module internal rec NoSln.Formatter

open System

/// Formats a solution tree into a solution file text
let formatSolution solution =
    formatSolutionFileLines solution
    |> String.concat Environment.NewLine

/// writes solution object to its target file
let writeSolutionFile solution =
    let contents = formatSolution solution
    System.IO.File.WriteAllText(path = solution.targetSolutionFile, contents = contents)

let private formatSolutionFileLines (solution : Solution) = seq {
    // flatten the solution structure into a list of guid relations
    let projects = ResizeArray<SolutionProject>()
    let folders = ResizeArray<SolutionFolder>()
    let nestedNodes = ResizeArray<Guid * Guid>()

    let rec visitProject (project : SolutionProject) =
        projects.Add project

    and visitFolder (folder : SolutionFolder) =
        folders.Add folder
        for proj in folder.projects do 
            nestedNodes.Add(proj.id, folder.id)
            visitProject proj

        for f in folder.folders do 
            nestedNodes.Add(f.id, folder.id)
            visitFolder f

    for proj in solution.projects do visitProject proj
    for folder in solution.folders do visitFolder folder

    // begin formatting
    let fmtGuid (g:Guid) = g.ToString().ToUpper()

    yield "Microsoft Visual Studio Solution File, Format Version 12.00"
    yield "# Visual Studio 15"
    yield "VisualStudioVersion = 15.0.27428.2002"
    yield "MinimumVisualStudioVersion = 10.0.40219.1"

    let fmtProject (project : SolutionProject) = seq {
        yield sprintf """Project("{%s}") = "%s", "%s", "{%s}" """
                    (fmtGuid project.projectTypeGuid) project.name project.relativePath (fmtGuid project.id)

        yield "EndProject"
    }

    let fmtFolder (folder : SolutionFolder) = seq {
        yield sprintf """Project("{%s}") = "%s", "%s", "{%s}" """
                (fmtGuid folder.projectTypeGuid) folder.name folder.name (fmtGuid folder.id)

        match folder.files with
        | [] -> ()
        | files ->
            yield "\tProjectSection(SolutionItems) = preProject"
            for file in files |> Seq.sortBy (fun f -> f.id.ToLowerInvariant()) do yield sprintf "\t\t%s = %s" file.id file.relativePath
            yield "\tEndProjectSection"

        yield "EndProject"
    }

    // Need to topologically sort when formatting projects in solution,
    // otherwise we get this https://github.com/dotnet/cli/issues/10484
    for proj in getTopologicalOrdering projects do yield! fmtProject proj
    for folder in folders do yield! fmtFolder folder

    // Global Section
    yield "Global"

    yield "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution"
    yield "\t\tDebug|Any CPU = Debug|Any CPU"
    yield "\t\tRelease|Any CPU = Release|Any CPU"
    yield "\tEndGlobalSection"

    yield "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution"
    for proj in projects do 
        let fmt config platform cfg = 
            sprintf "\t\t{%s}.%s|%s.%s = %s|%s"
                (fmtGuid proj.id) config platform cfg config platform

        // TODO: detect nonstandard project configurations
        yield fmt "Debug" "Any CPU" "ActiveCfg"
        yield fmt "Debug" "Any CPU" "Build.0"
        yield fmt "Release" "Any CPU" "ActiveCfg"
        yield fmt "Release" "Any CPU" "Build.0"
    yield "\tEndGlobalSection"

    yield "\tGlobalSection(SolutionProperties) = preSolution"
    yield "\t\tHideSolutionNode = FALSE"
    yield "\tEndGlobalSection"

    if nestedNodes.Count > 0 then
        yield "\tGlobalSection(NestedProjects) = preSolution"
        for child,parent in nestedNodes do yield sprintf "\t\t{%s} = {%s}" (fmtGuid child) (fmtGuid parent)
        yield "\tEndGlobalSection"

    yield "\tGlobalSection(ExtensibilityGlobals) = postSolution"
    yield sprintf "\t\tSolutionGuid = {%s}"(fmtGuid solution.id)
    yield "\tEndGlobalSection"

    yield "EndGlobal"
    yield ""
}

/// calculates topological ordering of supplied project files
let private getTopologicalOrdering (projects : seq<SolutionProject>) =
    let index = projects |> Seq.map (fun p -> p.fullPath, p) |> dict

    let graph =
        index.Values
        |> Seq.map (fun p -> 
            // prune references that point to nodes outside of the current project set
            let trimmedEdges = p.p2pReferences |> Seq.filter index.ContainsKey |> ResizeArray
            p.fullPath, trimmedEdges)

    match Graph.tryGetTopologicalSorting graph with
    | Some sorted -> sorted |> Seq.map (fun k -> index.[k]) |> Seq.toList
    | None -> Seq.toList projects // silently ignore cyclic dependencies