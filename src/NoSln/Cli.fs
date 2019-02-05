module NoSln.Cli

open System
open System.IO
open Fake.IO.FileSystemOperators
open Argu

type Argument =
    | Version
    | [<MainCommand>] Project of path:string
    | [<AltCommandLine("-o")>] Output of solution_file:string
with
    interface IArgParserTemplate with
        member a.Usage =
            match a with
            | Version ->
                "Display nosln version in use."
            | Project _ -> 
                "Directory to generate a solution file for. Defaults to current directory."
            | Output _ ->
                "Output solution file."

let mkParser() = 
    ArgumentParser.Create<Argument>(
        programName = "dotnet nosln", 
        errorHandler = ColoredProcessExiter(),
        checkStructure = true)

type CliAction =
    | ShowVersion
    | GenerateSln of projectDir:string * slnFile:string

let processArguments (results : ParseResults<Argument>) =
    if results.Contains <@ Argument.Version @> then ShowVersion else

    let project =
        let validate (proj : string) =
            if File.Exists proj then Path.GetDirectoryName proj
            elif Directory.Exists proj then proj
            else failwithf "invalid project path %A" proj
            |> Path.GetFullPath

        match results.TryPostProcessResult(<@ Project @>, validate) with
        | Some p -> p
        | None -> Environment.CurrentDirectory


    let output = 
        match results.TryGetResult <@ Output @> with
        | Some o -> Path.GetFullPath o
        | None -> project @@ Path.GetFileName(project) + ".sln"
    
    GenerateSln(project, output)

let handleCliAction (action : CliAction) =
    match action with
    | ShowVersion -> Console.log Assembly.packageVersion
    | GenerateSln(projectDir, targetSolutionFile) ->
        let solutionDir = Path.GetDirectoryName targetSolutionFile
        let sln = Core.createSolutionFromDirectory (Some solutionDir) projectDir
        let slnContents = Core.formatSolutionFile sln
        Core.writeSolutionFile targetSolutionFile slnContents