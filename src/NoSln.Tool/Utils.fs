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

    type Os = Windows | OSX | Linux | Unknown

    let osPlatform =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then Windows
        elif RuntimeInformation.IsOSPlatform OSPlatform.Linux then Linux
        elif RuntimeInformation.IsOSPlatform OSPlatform.OSX then OSX
        else Unknown

module Path =

    /// works around Path.GetFullPath issues running on unix
    let getFullPathXPlat (path : string) =
        let normalizedPath =
            match System.IO.Path.DirectorySeparatorChar with
            | '/' -> path.Replace('\\', '/') // Path.GetFullPath not handling backslashes properly on unices
            | _   -> path

        Path.GetFullPath(normalizedPath).TrimEnd('\\','/') // Trim trailing slashes

module Console =

    let log (msg : string) = Console.WriteLine msg
    let logf fmt = Printf.ksprintf log fmt

    let logColor color (msg : string) =
        let currColor = Console.ForegroundColor
        Console.ForegroundColor <- color
        try Console.WriteLine msg
        finally Console.ForegroundColor <- currColor

    let logfColor color fmt = Printf.ksprintf (logColor color) fmt

module File =

    let createOrReplace (path : string) (content : string) =
        Fake.IO.Directory.ensure (Path.GetDirectoryName path)
        let exists = File.Exists path
        File.WriteAllText(path, content)
        exists

module Process =

    let executeFile (path : string) =
        match Environment.osPlatform with
        | Environment.Windows -> 
            let psi = new ProcessStartInfo(path, UseShellExecute = true)
            let _ = Process.Start psi
            ()

        | Environment.OSX ->
            let psi = new ProcessStartInfo("open", path)
            let _ = Process.Start psi
            ()
        
        | Environment.Linux ->
            let psi = new ProcessStartInfo("xdg-open", path)
            let _ = Process.Start psi
            ()

        | env -> raise <| NotImplementedException(sprintf "execution of sln files not yet implemented in %O environments" env)

type ColoredProcessExiter() =
    interface IExiter with
        member __.Name = "exiter"
        member __.Exit(message, code) =
            if code = ErrorCode.HelpText then
                Console.WriteLine(message)
            else
                Console.logColor ConsoleColor.Red message

            exit(int code)