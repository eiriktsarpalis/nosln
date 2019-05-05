[<AutoOpen>]
module internal NoSln.Utils

open System
open System.Collections.Generic
open System.IO

let throw ctor fmt = Printf.ksprintf (raise << ctor) fmt

module Environment =

    let isCaseSensitiveFileSystem =
        // this is merely a heuristic, but should always work in practice
        let tmp = Path.GetTempPath()
        not(Directory.Exists(tmp.ToUpper()) && Directory.Exists(tmp.ToLower()))

module Path =

    /// works around Path.GetFullPath issues running on unix
    let getFullPathXPlat (path : string) =
        let normalizedPath =
            match System.IO.Path.DirectorySeparatorChar with
            | '/' -> path.Replace('\\', '/') // Path.GetFullPath not handling backslashes properly on unices
            | _   -> path

        Path.GetFullPath(normalizedPath).TrimEnd('\\','/') // Trim trailing slashes

    /// Given two absolute paths, calculates a target path relative to the source path
    let getRelativePath (basePath : string) (targetPath : string) =
        // adapted from https://stackoverflow.com/a/22055937/1670977
        // TODO replace with implementation available in netstandard2.1
        let sep = Path.DirectorySeparatorChar
        let pathComparer = if Environment.isCaseSensitiveFileSystem then StringComparison.InvariantCulture else StringComparison.InvariantCultureIgnoreCase
        let (==) x y = String.Equals(x, y, pathComparer)

        if not(Path.IsPathRooted basePath) then invalidArg "sourcePath" "path must be absolute"
        if not(Path.IsPathRooted targetPath) then invalidArg "targetPath" "path must be absolute"

        let tokenize p = Path.GetFullPath(p).TrimEnd(sep).Split(sep)
        let baseTokens = tokenize basePath
        let targetTokens = tokenize targetPath

        let overlap =
            Seq.zip baseTokens targetTokens
            |> Seq.takeWhile (fun (l,r) -> l == r)
            |> Seq.length

        if overlap = 0 then targetPath else

        let relativeTokens = seq {
            for _ in 1 .. baseTokens.Length - overlap do yield ".."
            for i in overlap .. targetTokens.Length - 1 do yield targetTokens.[i]
        }

        String.concat (string sep) relativeTokens

module Dictionary =
    let create keyValuePairs =
        let dict = new Dictionary<_,_>()
        for k,v in keyValuePairs do dict.Add(k,v)
        dict

type Map<'k,'v when 'k : comparison> with
    member m.Keys   = (m :> IDictionary<'k,'v>).Keys
    member m.Values = (m :> IDictionary<'k,'v>).Values


module Graph =

    let tryGetTopologicalSorting (graph : seq<'T * #seq<'T>>) =
        // straightforward implementation of Kahn's algorithm
        let state = 
            graph 
            |> Seq.map (fun (v,es) -> v, HashSet(es :> seq<_>))
            |> Dictionary.create

        let sorted = ResizeArray()

        let stripVertex v =
            state.Remove v |> ignore
            for es in state.Values do es.Remove v |> ignore

        let rec aux () =
            if state.Count = 0 then Seq.toList sorted |> Some
            else
                match state |> Seq.tryFind (fun kv -> kv.Value.Count = 0) with
                | None -> None // graph contains cycles, cannot find topological sort
                | Some (KeyValue(v,_)) ->
                    stripVertex v
                    sorted.Add v
                    aux()
        aux()