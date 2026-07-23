module internal NoSln.Formatter

open System
open System.Xml.Linq

let private addAttribute name value (element : XElement) =
    element.Add(XAttribute(XName.Get name, value))
    element

let private formatProject (project : SolutionProject) =
    XElement(XName.Get "Project")
    |> addAttribute "Path" project.relativePath

let private formatFile (file : SolutionFile) =
    XElement(XName.Get "File")
    |> addAttribute "Path" file.relativePath

let rec private formatFolder parentPath (folder : SolutionFolder) = seq {
    let path = parentPath @ [folder.name]
    let folderName = "/" + String.concat "/" path + "/"
    let element =
        XElement(XName.Get "Folder")
        |> addAttribute "Name" folderName

    for file in folder.files do element.Add(formatFile file)
    for project in folder.projects do element.Add(formatProject project)

    yield element

    for child in folder.folders do
        yield! formatFolder path child
}

/// Formats a solution tree into a solution file text.
let formatSolution (solution : Solution) =
    let element = XElement(XName.Get "Solution")

    for folder in solution.folders do
        for folderElement in formatFolder [] folder do
            element.Add folderElement

    for project in solution.projects do
        element.Add(formatProject project)

    element.ToString() + Environment.NewLine

/// Writes a solution object to its target file.
let writeSolutionFile solution =
    let contents = formatSolution solution
    System.IO.File.WriteAllText(path = solution.targetSolutionFile, contents = contents)