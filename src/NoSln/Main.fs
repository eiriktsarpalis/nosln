module NoSln.Main

[<EntryPoint>]
let main (argv:string[]) =
    let parser = Cli.mkParser()
    let results = parser.ParseCommandLine argv
    
    let action = results.Catch((fun () -> Cli.processArguments results), showUsage = true)
    do results.Catch((fun () -> Cli.handleCliAction action), showUsage = false)

    0