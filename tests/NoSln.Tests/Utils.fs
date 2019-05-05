[<AutoOpen>]
module NoSln.Tests.Utils

open NoSln

type Node =
    | File
    | Folder
    | Project

let tryFindPath (path : string list) (solution : Solution) =
    let rec aux (folders : SolutionFolder list) (projects : SolutionProject list) (files : SolutionFile list) path =
        match path with
        | [] -> None
        | [item] ->
            match folders |> List.tryFind (fun f -> f.name = item) with
            | Some f -> Some Folder
            | None -> 
                match projects |> Seq.filter (fun p -> p.name = item) |> Seq.tryHead with
                | Some p -> Some Project
                | None ->
                    match files |> Seq.filter (fun f -> f.id = item) |> Seq.tryHead with
                    | Some f -> Some File
                    | None -> None

        | folder :: rest ->
            match folders |> List.tryFind (fun f -> f.name = folder) with
            | Some f -> aux f.folders f.projects f.files rest
            | None -> None

    aux solution.folders solution.projects [] path