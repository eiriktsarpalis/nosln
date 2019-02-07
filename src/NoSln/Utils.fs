[<AutoOpen>]
module NoSln.Utils

open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Runtime.InteropServices
open Argu

module Assembly =
    let currentAssembly = Assembly.GetExecutingAssembly()

    let packageVersion =
        let attribute = currentAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        attribute.InformationalVersion

module Environment =

    type Os = Windows | Mac | Linux | Unknown

    let osPlatform =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then Windows
        elif RuntimeInformation.IsOSPlatform OSPlatform.Linux then Linux
        elif RuntimeInformation.IsOSPlatform OSPlatform.OSX then Mac
        else Unknown

module Console =

    let log (msg : string) = Console.WriteLine msg
    let logf fmt = Printf.ksprintf log fmt

    let logColor color (msg : string) =
        let currColor = Console.ForegroundColor
        Console.ForegroundColor <- color
        try Console.WriteLine msg
        finally Console.ForegroundColor <- currColor

    let logfColor color fmt = Printf.ksprintf (logColor color) fmt

type ColoredProcessExiter() =
    interface IExiter with
        member __.Name = "exiter"
        member __.Exit(message, code) =
            if code = ErrorCode.HelpText then
                Console.WriteLine(message)
            else
                Console.logColor ConsoleColor.Red message

            exit(int code)

module Path =
    let toBackSlashSeparators (path : string) = path.Replace('/', '\\')
    let toForwardSlashSeparators (path : string) = path.Replace('\\', '/')
    
    /// works around Path.GetFullPath issues running on unix
    let getFullPath (path : string) =
        match Environment.osPlatform with
        | Environment.Windows 
        | Environment.Unknown -> Path.GetFullPath path
        | Environment.Linux
        | Environment.Mac -> 
            // not working properly on unices with backslash paths
            Path.GetFullPath(toForwardSlashSeparators path)

module File =

    let createOrReplace (path : string) (content : string) =
        Fake.IO.Directory.ensure (Path.GetDirectoryName path)
        let exists = File.Exists path
        File.WriteAllText(path, content)
        exists


module Process =

    let isFileExecutionSupported =
        match Environment.osPlatform with
        | Environment.Windows
        | Environment.Mac -> true
        | _ -> false
    
    let executeFile (path : string) =
        match Environment.osPlatform with
        | Environment.Windows -> 
            let psi = new ProcessStartInfo(path, UseShellExecute = true)
            let _ = Process.Start psi
            ()

        | Environment.Mac ->
            let psi = new ProcessStartInfo("open", path)
            let _ = Process.Start psi
            ()

        | env -> raise <| NotImplementedException(sprintf "execution of sln files not yet implemented in %O environments" env)