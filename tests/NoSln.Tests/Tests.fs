module NoSln.Tests.Tests

open System
open System.IO
open Xunit
open Swensen.Unquote.Assertions
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators

open NoSln
open NoSln.Tests

let rec private findRepoDirectory directory =
    if File.Exists(directory @@ "Directory.Build.props") then directory
    else
        match Directory.GetParent directory with
        | null -> failwith "Could not locate the repository root."
        | parent -> findRepoDirectory parent.FullName

let private getRepoDirectory () = findRepoDirectory AppContext.BaseDirectory
let private getAllProjects repoDirectory = !! (repoDirectory @@ "**" @@ "*.??proj") |> Seq.toList
let private getAllFiles repoDirectory = !! (repoDirectory @@ "**" @@ "*") |> Seq.toList

[<Fact>]
let ``Generated solution should place test project in right folder`` () =
    let repoDirectory = getRepoDirectory ()
    let sln = NoSln.CreateSolution(baseDirectory = repoDirectory, projects = getAllProjects repoDirectory)

    sln |> tryFindPath ["tests"; "NoSln.Tests"] =! Some Project

[<Fact>]
let ``Generated solution should place solution items in right folder`` () =
    let repoDirectory = getRepoDirectory ()
    let sln = NoSln.CreateSolution(baseDirectory = repoDirectory, files = getAllFiles repoDirectory)

    sln |> tryFindPath ["Solution Items"; ".gitignore"] =! Some File

[<Fact>]
let ``Generated solution with flattened structure place test project in right folder`` () =
    let repoDirectory = getRepoDirectory ()
    let sln = NoSln.CreateSolution(baseDirectory = repoDirectory, projects = getAllProjects repoDirectory, flattenSolutionFolders = true)

    sln |> tryFindPath ["NoSln.Tests"] =! Some Project

[<Fact>]
let ``Referencing test projects should include transitive dependencies by default`` () =
    let repoDirectory = getRepoDirectory ()
    let testProject = repoDirectory @@ "tests" @@ "NoSln.Tests" @@ "NoSln.Tests.fsproj"
    let sln = NoSln.CreateSolution(baseDirectory = repoDirectory, projects = [testProject])

    sln |> tryFindPath ["src" ; "NoSln.Library"] =! Some Project

[<Fact>]
let ``Referencing test projects should not include transitive dependencies if disabled`` () =
    let repoDirectory = getRepoDirectory ()
    let testProject = repoDirectory @@ "tests" @@ "NoSln.Tests" @@ "NoSln.Tests.fsproj"
    let sln = NoSln.CreateSolution(baseDirectory = repoDirectory, projects = [testProject], includeTransitiveP2pReferences = false)

    sln |> tryFindPath ["src" ; "NoSln.Library"] =! None

[<Fact>]
let ``Generated solution should use XML format`` () =
    let repoDirectory = getRepoDirectory ()
    let project = repoDirectory @@ "src" @@ "NoSln" @@ "NoSln.Library.fsproj"
    let file = repoDirectory @@ "README.md"
    let solution = NoSln.CreateSolution(baseDirectory = repoDirectory, projects = [project], files = [file])

    let expected =
        String.concat Environment.NewLine
            [ "<Solution>"
              "  <Folder Name=\"/Solution Items/\">"
              "    <File Path=\"README.md\" />"
              "  </Folder>"
              "  <Folder Name=\"/src/\">"
              "    <Project Path=\"src/NoSln/NoSln.Library.fsproj\" />"
              "  </Folder>"
              "</Solution>"
              "" ]

    solution.Format() =! expected

[<Fact>]
let ``Generated solution paths should use the .slnx extension`` () =
    let repoDirectory = getRepoDirectory ()
    let solution = NoSln.CreateSolution(baseDirectory = repoDirectory)
    let tempSolution = NoSln.CreateSolution(baseDirectory = repoDirectory, useTempSolutionFile = true)

    Path.GetExtension solution.targetSolutionFile =! ".slnx"
    Path.GetExtension tempSolution.targetSolutionFile =! ".slnx"