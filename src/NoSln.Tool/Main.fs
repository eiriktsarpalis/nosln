﻿module NoSln.Main

open System

[<EntryPoint>]
let main (argv:string[]) =
    let parser = Cli.mkParser()
    let results = parser.ParseCommandLine argv
    
    let inline handler showUsage f =
        try f ()
        with 
        | NoSlnException message ->
            results.Raise(message, showUsage = showUsage)
        | e ->
            results.Raise(string e, showUsage = showUsage)

    let action = handler true (fun () -> Cli.processArguments results)
    handler false (fun () -> Cli.handleCliAction action)

    0