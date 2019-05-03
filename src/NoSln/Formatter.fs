﻿module NoSln.Formatter

open System

let private csProjGuid = Guid.Parse "9A19103F-16F7-4668-BE54-9A1E7A4F7556"
let private vbProjGuid = Guid.Parse "778DAE3C-4631-46EA-AA77-85C1314464D9"
let private fsprojGuid = Guid.Parse "6EC3EE1D-3C4E-46DD-8F32-0CC8E7565705"
//---------------------
let private csProjGuidLegacy = Guid.Parse "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC"
let private vbProjGuidLegacy = Guid.Parse "F184B08F-C81C-45F6-A57F-5ABD9991F28F"
let private fsprojGuidLegacy = Guid.Parse "F2A71F9B-5D33-465A-A702-920D77279786"
//---------------------
let private directoryGuid = Guid.Parse "2150E333-8FDC-42A3-9474-1A3956D46DE8"

let getProjectGuid (proj : Project) =
    // we use the legacy guids for backward compatibility
    match proj.projectType with
    | CsProj -> csProjGuid
    | VbProj -> vbProjGuid
    | FsProj -> fsprojGuid
    | CsProjLegacy -> csProjGuidLegacy
    | VbProjLegacy -> vbProjGuidLegacy
    | FsProjLegacy -> fsprojGuidLegacy
    | Unrecognized -> csProjGuidLegacy // just pick something


let private formatSolutionFileLines (solution : Solution) = seq {
    // flatten the solution structure into a list of guid relations
    let projects = ResizeArray<Project>()
    let folders = ResizeArray<Folder>()
    let nestedNodes = ResizeArray<Guid * Guid>()

    let rec visitProject (project : Project) =
        projects.Add project

    and visitFolder (folder : Folder) =
        folders.Add folder
        for proj in folder.projects do 
            nestedNodes.Add(proj.id, folder.id)
            visitProject proj

        for f in folder.folders.Values do 
            nestedNodes.Add(f.id, folder.id)
            visitFolder f

    for proj in solution.projects do visitProject proj
    for folder in solution.folders.Values do visitFolder folder

    // begin formatting
    let fmtGuid (g:Guid) = g.ToString().ToUpper()

    yield "Microsoft Visual Studio Solution File, Format Version 12.00"
    yield "# Visual Studio 15"
    yield "VisualStudioVersion = 15.0.27428.2002"
    yield "MinimumVisualStudioVersion = 10.0.40219.1"

    let fmtProject (project : Project) = seq {
        yield sprintf """Project("{%s}") = "%s", "%s", "{%s}" """
                    (fmtGuid (getProjectGuid project)) project.name project.path (fmtGuid project.id)

        yield "EndProject"
    }

    let fmtFolder (folder : Folder) = seq {
        yield sprintf """Project("{%s}") = "%s", "%s", "{%s}" """
                (fmtGuid directoryGuid) folder.name folder.name (fmtGuid folder.id)

        match folder.files with
        | [] -> ()
        | files ->
            yield "\tProjectSection(SolutionItems) = preProject"
            for file in files |> Seq.sortBy (fun f -> f.id.ToLowerInvariant()) do yield sprintf "\t\t%s = %s" file.id file.path
            yield "\tEndProjectSection"

        yield "EndProject"
    }

    // Need to topologically sort when formatting projects in solution,
    // otherwise we get this https://github.com/dotnet/cli/issues/10484
    for proj in Core.getTopologicalOrdering projects do yield! fmtProject proj
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

/// Formats a solution tree into a solution file text
let formatSolution solution =
    formatSolutionFileLines solution
    |> String.concat Environment.NewLine