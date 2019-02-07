module NoSln.Formatter

open System
open Fake.IO

let private projectGuid = Guid.Parse "6EC3EE1D-3C4E-46DD-8F32-0CC8E7565705"
let private directoryGuid = Guid.Parse "2150E333-8FDC-42A3-9474-1A3956D46DE8"

let private formatSolutionFileLines (solution : Solution) = seq {
    let fmtGuid (g:Guid) = g.ToString().ToUpper()

    yield "Microsoft Visual Studio Solution File, Format Version 12.00"
    yield "# Visual Studio 15"
    yield "VisualStudioVersion = 15.0.27428.2002"
    yield "MinimumVisualStudioVersion = 10.0.40219.1"

    let projects = ResizeArray<Guid>()
    let nestedNodes = ResizeArray<Guid * Guid>()

    let fmtProject (project : Project) = seq {
        projects.Add project.id
        yield sprintf """Project("{%s}") = "%s", "%s", "{%s}" """
                    (fmtGuid projectGuid) project.name project.path (fmtGuid project.id)

        yield "EndProject"
    }

    let rec fmtFolder (folder : Folder) = seq {
        yield sprintf """Project("{%s}") = "%s", "%s", "{%s}" """
                (fmtGuid directoryGuid) folder.name folder.name (fmtGuid folder.id)

        match folder.files with
        | [] -> ()
        | files ->
            yield "\tProjectSection(SolutionItems) = preProject"
            for file in files |> Seq.sortBy (fun f -> f.id.ToLowerInvariant()) do yield sprintf "\t\t%s = %s" file.id file.path
            yield "\tEndProjectSection"

        yield "EndProject"

        for proj in folder.projects do 
            yield! fmtProject proj
            nestedNodes.Add(proj.id, folder.id)

        for f in folder.folders.Values do
            yield! fmtFolder f
            nestedNodes.Add(f.id, folder.id)
    }

    for proj in solution.projects do yield! fmtProject proj
    for folder in solution.folders.Values do yield! fmtFolder folder

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
                (fmtGuid proj) config platform cfg config platform

        // TODO: detect nonstandard project configurations
        yield fmt "Debug" "Any CPU" "ActiveCfg"
        yield fmt "Debug" "Any CPU" "Build.0"
        yield fmt "Release" "Any CPU" "ActiveCfg"
        yield fmt "Release" "Any CPU" "Build.0"
    yield "\tEndGlobalSection"

    yield "\tGlobalSection(SolutionProperties) = preSolution"
    yield "\t\tHideSolutionNode = FALSE"
    yield "\tEndGlobalSection"

    yield "\tGlobalSection(NestedProjects) = preSolution"
    for child,parent in nestedNodes do yield sprintf "\t\t{%s} = {%s}" (fmtGuid child) (fmtGuid parent)
    yield "\tEndGlobalSection"

    yield "\tGlobalSection(ExtensibilityGlobals) = postSolution"
    yield sprintf "\t\tSolutionGuid = {%s}"(fmtGuid solution.id)
    yield "\tEndGlobalSection"

    yield "EndGlobal"
}

/// Formats a solution tree into a solution file text
let formatSolution solution =
    formatSolutionFileLines solution
    |> String.concat Environment.NewLine