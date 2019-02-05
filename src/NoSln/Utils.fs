[<AutoOpen>]
module NoSln.Utils

open System
open System.IO
open System.Reflection
open Argu

module Assembly =
    let currentAssembly = Assembly.GetExecutingAssembly()

    let packageVersion =
        let attribute = currentAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        attribute.InformationalVersion

module Console =

    let log (msg : string) = Console.WriteLine msg

    let logColor color (msg : string) =
        let currColor = Console.ForegroundColor
        Console.ForegroundColor <- color
        try Console.WriteLine msg
        finally Console.ForegroundColor <- currColor


module Path =
    let isCaseSensitiveFileSystem = 
        // this is merely a heuristic, but should always work in practice
        let tmp = Path.GetTempPath()
        not(Directory.Exists(tmp.ToUpper()) && Directory.Exists(tmp.ToLower()))

type ColoredProcessExiter() =
    interface IExiter with
        member __.Name = "exiter"
        member __.Exit(message, code) =
            if code = ErrorCode.HelpText then
                Console.WriteLine(message)
            else
                Console.logColor ConsoleColor.Red message

            exit(int code)