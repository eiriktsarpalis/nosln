open System
open Argu

type Argument =
    | Echo of string
with
    interface IArgParserTemplate with
        member __.Usage = "echo argument"


let parser = ArgumentParser.Create<Argument>()

[<EntryPoint>]
let main argv =
    let results = parser.Parse argv
    let value = results.TryGetResult <@ Echo @>
    match value with
    | None -> printfn "No echo!"
    | Some e -> printfn "Echoed %A" e

    0