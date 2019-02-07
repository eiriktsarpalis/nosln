[<AutoOpen>]
module NoSln.Tests.Utils

open NoSln

type Type =
    | File of File
    | Folder of Folder
    | Project of Project

let tryFindPath (solution : Solution) (path : string list) =
    let rec aux (folders : Map<string, Folder>) (projects : Project list) (files : File list) path =
        match path with
        | [] -> None
        | [item] ->
            match folders.TryGetValue item with
            | true, f -> Some(Folder f)
            | _ -> 
                match projects |> Seq.filter (fun p -> p.name = item) |> Seq.tryHead with
                | Some p -> Some(Project p)
                | None ->
                    match files |> Seq.filter (fun f -> f.id = item) |> Seq.tryHead with
                    | Some f -> Some(File f)
                    | None -> None

        | folder :: rest ->
            match folders.TryGetValue folder with
            | true, f -> aux f.folders f.projects f.files rest
            | _ -> None

    aux solution.folders solution.projects [] path