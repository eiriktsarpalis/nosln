module NoSln.GitIgnore

open System
open System.IO
open Fake.IO.Globbing

/// locate gitignore file by looking in all parent directories
let rec tryFindGitIgnoreFile (path : string) =
    let candidate = Path.Combine(path, ".gitignore")
    if File.Exists candidate then Some candidate
    else
        match Path.GetDirectoryName path with
        | null -> None
        | parent -> tryFindGitIgnoreFile parent

// poor man's .gitignore parser, improvements welcome
// https://git-scm.com/docs/gitignore
/// converts a .gitignore file to a globbing representation
let parse (gitIgnoreFile : string) =
    let baseDir = Path.GetDirectoryName gitIgnoreFile

    let stripComments (entry : string) =
        let rec aux (s : int) (entry : string) =
            match entry.IndexOf('#', s) with
            | -1 -> entry
            |  0 -> ""
            | i when entry.[i - 1] = '\\' -> aux (i + 1) entry // ignore escapes
            | i -> entry.[.. i - 1]

        aux 0 entry

    let parseEntry (entry : string) =
        let isNegation, entry =
            if entry.StartsWith "!" then
                true, entry.TrimStart '!'
            elif entry.StartsWith "\!" then
                false, entry.TrimStart '\\'
            else
                false, entry

        let entry =
            if entry.StartsWith "/" || entry.StartsWith "\\" then
                entry.TrimStart('/', '\\')
            else
                Path.Combine("**", entry)

        let entry =
            if entry.EndsWith "/" || entry.EndsWith "\\" then
                Path.Combine(entry, "**/*")
            else
                entry
            
        isNegation, entry.Replace('\\', '/')

    let ignorePatterns = 
        File.ReadLines(gitIgnoreFile)
        |> Seq.map stripComments
        |> Seq.filter (not << String.IsNullOrWhiteSpace)
        |> Seq.map parseEntry
        |> Seq.choose (fun (neg,entry) -> if not neg then Some entry else None) // we sadly won't support negation patterns for now
        |> Seq.toList

    {
        BaseDirectory = baseDir
        Includes = ["**/*"]
        Excludes = ignorePatterns
    }