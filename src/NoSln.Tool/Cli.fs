module NoSln.Cli

open System
open System.IO
open System.Runtime.InteropServices
open System.Text.RegularExpressions
open Fake.IO.FileSystemOperators
open Argu

type Argument =
    | Version
    | [<MainCommand>] Path of path:string
    | [<AltCommandLine("-o")>] Output of solution_file:string
    | [<AltCommandLine("-I")>] Include_Projects of pattern:string
    | [<AltCommandLine("-E")>] Exclude_Projects of pattern:string
    | [<AltCommandLine("-i")>] Include_Files of pattern:string
    | [<AltCommandLine("-e")>] Exclude_Files of pattern:string
    | [<AltCommandLine("-G")>] No_Git_Ignore
    | [<AltCommandLine("-F")>] No_Files
    | [<AltCommandLine("-T")>] No_Transitive_Projects
    | [<AltCommandLine("-a")>] Absolute_Paths
    | [<AltCommandLine("-f")>] Flatten
    | [<AltCommandLine("-s")>] Start
    | [<AltCommandLine("-q")>] Quiet
    | [<AltCommandLine("-t")>] Temp
    | [<AltCommandLine("-D")>] Debug
with
    interface IArgParserTemplate with
        member a.Usage =
            match a with
            | Version ->
                "Display the version string and exit."
            | Path _ -> 
                "Base directory or project file used for populating the solution file. " +
                "All projects and files within the directory will be added to the solution. " +
                "Defaults to the current directory."
            | Output _ ->
                "Output solution file. Defaults to a solution file at the root of the supplied base directory."
            | Include_Projects _ ->
                "Included project files globbing pattern. Multiple arguments are treated using OR semantics."
            | Exclude_Projects _ ->
                "Excluded project files globbing pattern. Multiple arguments are treated using OR semantics."
            | Include_Files _ ->
                "Included solution files globbing pattern. Multiple arguments are treated using OR semantics."
            | Exclude_Files _ ->
                "Excluded solution files globbing pattern. Multiple arguments are treated using OR semantics."
            | No_Git_Ignore ->
                "Do not take .gitignore files into account when excluding files."
            | No_Files ->
                "Do not include any solution items in the generated solution."
            | No_Transitive_Projects ->
                "By default, nosln will include transitive p2p dependencies, even if excluded by a globbing pattern or outside of the project directory." +
                "Enable this flag to avoid expanding to transitive projects."
            | Absolute_Paths ->
                "Use absolute paths in generated solution file. Otherwise contents will be relative to the solution file output folder."
            | Flatten ->
                "Places all projects at the root of the solution file without replicating the filesystem structure. Also implies --no-files."
            | Start ->
                "Starts an IDE process with the newly generated solution file. Uses 'start' command on Windows, 'open' on OSX, 'xdg-open' on Linux."
            | Temp ->
                "Creates a disposable solution file in the system temp folder. Overrides the --output argument. Also implies --absolute-paths."
            | Quiet ->
                "Quiet mode, only output the file name of the generated solution to stdout. Useful for passing generated solution to shell scripts."
            | Debug ->
                "Generate debug logs. Overrides --quiet mode."

let mkParser() = 
    ArgumentParser.Create<Argument>(
        programName = "dotnet nosln", 
        errorHandler = ColoredProcessExiter(),
        checkStructure = true)

type CliAction =
    | ShowVersion
    | GenerateSln of Configuration

let processArguments (results : ParseResults<Argument>) =
    if results.Contains <@ Argument.Version @> then ShowVersion else

    let baseDirectory =
        let validate (path : string) =
            let fullPath = Path.getFullPathXPlat path
            if Directory.Exists fullPath then fullPath
            elif Regex.IsMatch(fullPath, "\...proj") && File.Exists path then Path.GetDirectoryName fullPath
            else failwithf "supplied project path %A is not a valid directory or project file." path

        match results.TryPostProcessResult(<@ Path @>, validate) with
        | Some p -> p
        | None -> Environment.CurrentDirectory

    let projectIncludes = results.GetResults <@ Include_Projects @> 
    let projectExcludes = results.GetResults <@ Exclude_Projects @>
    let fileIncludes = results.GetResults <@ Include_Files @>
    let fileExcludes = results.GetResults <@ Exclude_Files @>
    let noFiles = results.Contains <@ No_Files @>
    let noTransitiveProjects = results.Contains <@ No_Transitive_Projects @>
    let useAbsolutePaths = results.Contains <@ Absolute_Paths @>
    let flattenProjects = results.Contains <@ Flatten @>
    let tmpSln = results.Contains <@ Temp @>
    let quiet = results.Contains <@ Quiet @>
    let debug = results.Contains <@ Debug @>
    let start = results.Contains <@ Start @>
    let gitIgnoreFile =
        if results.Contains <@ No_Git_Ignore @> then None
        else
            GitIgnore.tryFindGitIgnoreFile baseDirectory

    let targetSln =
        if tmpSln then
            // use random solution file in temp directory
            let fileName = 
                let slnIdentifier = Path.GetFileName baseDirectory // get filename of directory
                let randomSuffix = Path.GetRandomFileName() |> Path.GetFileNameWithoutExtension
                sprintf "%s-%s.sln" slnIdentifier randomSuffix

            Path.GetTempPath() @@ fileName
        else
            match results.TryGetResult <@ Output @> with
            | Some o -> Path.getFullPathXPlat o
            | None -> baseDirectory @@ Path.GetFileName baseDirectory + ".sln"

    GenerateSln {
        baseDirectory = baseDirectory
        projectIncludes = projectIncludes
        projectExcludes = projectExcludes
        fileIncludes = fileIncludes
        fileExcludes = fileExcludes
        gitIgnoreFile = gitIgnoreFile
        
        targetSolutionFile = targetSln
        targetSolutionDir = Path.GetDirectoryName targetSln

        noFiles = noFiles || flattenProjects
        noTransitiveProjects = noTransitiveProjects
        useAbsolutePaths = useAbsolutePaths || tmpSln
        flattenProjects = flattenProjects
        start = start
        quiet = quiet && not debug
        debug = debug
    }


let handleCliAction (action : CliAction) =
    match action with
    | ShowVersion -> Console.log Assembly.packageVersion
    | GenerateSln config ->
        if not config.quiet then
            Console.logf "dotnet-nosln version %s" Assembly.packageVersion

        let sln = Globbing.mkSolution config

        if config.debug then
            Console.logf "Config: %A" config
            Console.logf "Solution: %A" sln

        let slnContents = sln.Format()
        let isReplaced = File.createOrReplace config.targetSolutionFile slnContents

        if config.quiet then Console.log config.targetSolutionFile
        elif isReplaced then Console.logfColor ConsoleColor.Yellow "replaced solution file %A" config.targetSolutionFile
        else Console.logfColor ConsoleColor.DarkGreen "created solution file %A" config.targetSolutionFile

        if config.start then Process.executeFile config.targetSolutionFile