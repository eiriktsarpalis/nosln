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
    
    let trimTrailingSlashes (path : string) = path.TrimEnd('/', '\\')

    /// works around Path.GetFullPath issues running on unix
    let getFullPathXPlat (path : string) =
        match Environment.osPlatform with
        | Environment.Windows 
        | Environment.Unknown -> Path.GetFullPath path
        | Environment.Linux
        | Environment.OSX -> 
            // not working properly on unices with backslash paths
            Path.GetFullPath(toForwardSlashSeparators path) 
        |> trimTrailingSlashes

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


/// Directed graph representation
type Graph<'T> = ('T * 'T list) list

module Graph =

    /// <summary>
    ///     Maps directed graph to isomorphic instance of relabeled nodes.
    /// </summary>
    /// <param name="f">Mapper function.</param>
    /// <param name="graph">Input graph.</param>
    let map (f : 'T -> 'S) (graph : Graph<'T>) : Graph<'S> =
        graph |> List.map (fun (n, edges) -> f n, List.map f edges)

    /// <summary>
    ///     Filters nodes (and adjacent edges) that satisfy the provided predicate.
    /// </summary>
    /// <param name="f">Node filter function.</param>
    /// <param name="graph">Input directed graph.</param>
    let filterNode (f : 'T -> bool) (graph : Graph<'T>) : Graph<'T> =
        graph |> List.choose(fun (n, edges) -> if f n then Some(n, List.filter f edges) else None)

    /// <summary>
    ///     Filters directed edges from graph that satisfy provided predicate.
    /// </summary>
    /// <param name="f">Directed edge filter predicate.</param>
    /// <param name="graph">Input directed graph.</param>
    let filterEdge (f : 'T -> 'T -> bool) (graph : Graph<'T>) : Graph<'T> =
        graph |> List.map (fun (n, edges) -> (n, List.filter (fun e -> f n e) edges))

    /// Attempt to compute a topological sorting for graph if DAG,
    /// If not DAG returns a cycle within the graph for further debugging.
    let tryGetTopologicalOrdering<'T when 'T : equality> (g : Graph<'T>) : Result<'T list, 'T list> =
        let locateCycle (g : Graph<'T>) =
            let d = dict g
            let rec tryFindCycleInPath (path : 'T list) (acc : 'T list) (t : 'T) =
                match path with
                | [] -> None
                | h :: _ when h = t -> Some (h :: acc)
                | h :: tl -> tryFindCycleInPath tl (h :: acc) t

            let rec walk (path : 'T list) (t : 'T) =
                match tryFindCycleInPath path [] t with
                | Some _ as cycle -> cycle
                | None -> d.[t] |> List.tryPick (walk (t :: path))

            g |> List.head |> fst |> walk [] |> Option.get

        let rec aux sorted (g : Graph<'T>) =
            if List.isEmpty g then Ok (List.rev sorted) else

            match g |> List.tryFind (function (_,[]) -> true | _ -> false) with
            | None -> Error (locateCycle g) // not a DAG, detect and report a cycle in graph
            | Some (t,_) ->
                let g0 = g |> filterNode ((<>) t)
                aux (t :: sorted) g0

        aux [] g