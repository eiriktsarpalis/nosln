namespace NoSln

open System
open System.Runtime.CompilerServices
open NoSln.Builder

[<AutoOpen>]
module Extensions =

    type Solution with
        /// Formats solution object to solution file payload
        member sln.Format() = Formatter.formatSolution sln
        /// Writes solution contents to specified target solution file
        member sln.Write() = Formatter.writeSolutionFile sln

type NoSln =

    /// <summary>
    ///     Builds a solution object from a collection of project and file paths.
    /// </summary>
    /// <param name="baseDirectory">Base directory for the generated solution. Sln folder structure will reflect paths relative to that base directory. Defaults to current directory.</param>
    /// <param name="projects">List of project files to be included in the solution as projects.</param>
    /// <param name="files">List of regular files to be included in the solution.</param>
    /// <param name="targetSolutionFile">Target path of the generated solution file. Defaults to a solution file located in the base directory.</param>
    /// <param name="includeTransitiveP2pReferences">Include transitive p2p dependencies no included in the projet list. Defaults to true.</param>
    /// <param name="flattenSolutionFolders">Flatten solution structure so that all items are contained in the root. Defaults to false.</param>
    /// <param name="useTempSolutionFile">Uses a randomly generated solution file in the system temp folder. Defaults to false.</param>
    /// <param name="useAbsolutePaths">Use absolute paths to solution items in the generated solution. Defaults to false.</param>
    static member CreateSolution
       (?baseDirectory : string,
        ?projects : seq<string>, 
        ?files : seq<string>,
        ?targetSolutionFile : string,
        ?includeTransitiveP2pReferences : bool,
        ?flattenSolutionFolders : bool,
        ?useTempSolutionFile : bool,
        ?useAbsolutePaths : bool) : Solution =

            let configuration : SolutionConfiguration =
                let baseDirectory = defaultArg baseDirectory Environment.CurrentDirectory |> Path.getFullPathXPlat
                let getFullPaths paths = paths |> Seq.map Path.getFullPathXPlat |> Seq.toList
                let targetSolutionFile =
                    match targetSolutionFile with
                    | None when defaultArg useTempSolutionFile false -> Builder.mkTempSolutionfile baseDirectory
                    | None -> Builder.mkSolutionFileForDirectory baseDirectory
                    | Some f -> f

                {
                    baseDirectory = baseDirectory
                    projects = defaultArg projects Seq.empty |> getFullPaths
                    files = defaultArg files Seq.empty |> getFullPaths
                    targetSolutionFile = targetSolutionFile
                    includeTransitiveReferences = defaultArg includeTransitiveP2pReferences true
                    flattenSolutionFolders = defaultArg flattenSolutionFolders false
                    useAbsolutePaths = defaultArg useAbsolutePaths false
                }

            Builder.validateConfiguration configuration
            Builder.mkSolution configuration

    /// <summary>
    ///     Writes a solution file from a collection of project and file paths.
    /// </summary>
    /// <param name="baseDirectory">Base directory for the generated solution. Sln folder structure will reflect paths relative to that base directory. Defaults to current directory.</param>
    /// <param name="projects">List of project files to be included in the solution as projects.</param>
    /// <param name="files">List of regular files to be included in the solution.</param>
    /// <param name="targetSolutionFile">Target path of the generated solution file. Defaults to a solution file located in the base directory.</param>
    /// <param name="includeTransitiveP2pReferences">Include transitive p2p dependencies no included in the projet list. Defaults to true.</param>
    /// <param name="flattenSolutionFolders">Flatten solution structure so that all items are contained in the root. Defaults to false.</param>
    /// <param name="useTempSolutionFile">Uses a randomly generated solution file in the system temp folder. Defaults to false.</param>
    /// <param name="useAbsolutePaths">Use absolute paths to solution items in the generated solution. Defaults to false.</param>
    static member WriteSolutionFile
       (?baseDirectory : string,
        ?projects : seq<string>, 
        ?files : seq<string>,
        ?targetSolutionFile : string,
        ?includeTransitiveP2pReferences : bool,
        ?flattenSolutionFolders : bool,
        ?useTempSolutionFile : bool,
        ?useAbsolutePaths : bool) : string =

        let solution = 
            NoSln.CreateSolution
               (?baseDirectory = baseDirectory,
                ?projects = projects,
                ?files = files,
                ?targetSolutionFile = targetSolutionFile,
                ?includeTransitiveP2pReferences = includeTransitiveP2pReferences,
                ?flattenSolutionFolders = flattenSolutionFolders,
                ?useTempSolutionFile = useTempSolutionFile,
                ?useAbsolutePaths = useAbsolutePaths)

        do solution.Write()

        solution.targetSolutionFile