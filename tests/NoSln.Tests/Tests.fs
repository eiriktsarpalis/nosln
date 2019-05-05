module NoSln.Tests.Tests

open System
open System.IO
open Xunit
open TypeShape.Empty
open Swensen.Unquote.Assertions
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators

open NoSln
open NoSln.Tests

let repoDirectory = __SOURCE_DIRECTORY__ @@ ".." @@ ".." |> Path.GetFullPath

let allProjects = lazy(!! (repoDirectory @@ "**" @@ "*.??proj") |> Seq.toList)
let allFiles = lazy(!! (repoDirectory @@ "**" @@ "*") |> Seq.toList)

[<Fact>]
let ``Generated solution should place test project in right folder`` () =
    let sln = NoSln.CreateSolution(baseDirectory = repoDirectory, projects = allProjects.Value)

    sln |> tryFindPath ["tests"; "NoSln.Tests"] =! Some Project

[<Fact>]
let ``Generated solution should place solution items in right folder`` () =
    let sln = NoSln.CreateSolution(baseDirectory = repoDirectory, files = allFiles.Value)

    sln |> tryFindPath ["Solution Items"; ".gitignore"] =! Some File

[<Fact>]
let ``Generated solution with flattened structure place test project in right folder`` () =
    let sln = NoSln.CreateSolution(baseDirectory = repoDirectory, projects = allProjects.Value, flattenSolutionFolders = true)

    sln |> tryFindPath ["NoSln.Tests"] =! Some Project

[<Fact>]
let ``Referencing test projects should include transitive dependencies by default`` () =
    let project = __SOURCE_DIRECTORY__ @@ "NoSln.Tests.fsproj"
    let sln = NoSln.CreateSolution(baseDirectory = repoDirectory, projects = [project])

    sln |> tryFindPath ["src" ; "NoSln.Library"] =! Some Project

[<Fact>]
let ``Referencing test projects should not include transitive dependencies if disabled`` () =
    let project = __SOURCE_DIRECTORY__ @@ "NoSln.Tests.fsproj"
    let sln = NoSln.CreateSolution(baseDirectory = repoDirectory, projects = [project], includeTransitiveP2pReferences = false)

    sln |> tryFindPath ["src" ; "NoSln.Library"] =! None