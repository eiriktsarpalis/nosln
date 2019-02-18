module NoSln.Tests.Tests

open System
open Xunit
open TypeShape.Empty
open Swensen.Unquote.Assertions
open NoSln

TypeShape.Empty.register (fun () -> Unchecked.defaultof<System.Xml.Linq.XDocument>)

let mkProj name logicalPath = { empty<Project> with name = name ; logicalPath = logicalPath }
let mkFile name logicalPath = { empty<File> with id = name ; logicalPath = logicalPath }

[<Fact>]
let ``Inserting a project should place it in the right path`` () =
    let project = mkProj "foo" ["src" ; "myproj"]

    let sln = Core.mkSln()
    let sln0 = Core.insertProject empty sln project

    tryFindPath sln0 ["src" ; "myproj" ; "foo"] =! Some (Project project)

[<Fact>]
let ``Inserting a file should place it in the right path`` () =
    let file = mkFile "sln.txt" ["items" ]

    let sln = Core.mkSln()
    let sln0 = Core.insertFile empty sln file

    tryFindPath sln0 ["items" ; "sln.txt"] =! Some (File file)

[<Fact>]
let ``Inserting an item should create a corresponding solution folder`` () =
    let project = mkProj "foo" ["src" ; "myproj"]

    let sln = Core.mkSln()
    let sln0 = Core.insertProject empty sln project

    test <@ match tryFindPath sln0 ["src" ; "myproj"] with Some (Folder _) -> true | _ -> false @>