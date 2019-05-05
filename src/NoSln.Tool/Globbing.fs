module NoSln.Globbing

open System.IO
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing
open Fake.IO.Globbing.Operators

/// files to be always excluded from globbing
let private excludedPatterns = 
    [ 
        "**/.vs/**/*"
        "**/.idea/**/*"
        "**/.git/**/*"
        "**/.store/**/*" 
    ]

/// Builds a solution object using provided globbing patterns
let mkSolution (config : Configuration) : Solution =
    let projectsUnderBaseDir = !! (config.baseDirectory @@ "**/*.??proj") |> Seq.toList

    let projects =
        match config.projectIncludes, config.projectExcludes with
        | [], [] -> projectsUnderBaseDir
        | includes, excludes ->
            let pattern =
                { BaseDirectory = config.baseDirectory
                  Includes = includes
                  Excludes = excludes }

            projectsUnderBaseDir |> List.filter pattern.IsMatch

    let files =
        if config.noFiles then []
        else
            // exclude files within project directories from solution items
            let projectFileExcludes =
                projectsUnderBaseDir 
                |> Seq.map (fun p -> Path.GetDirectoryName p)
                |> Seq.map (fun p -> Path.GetRelativePath(config.baseDirectory, p))
                |> Seq.map (fun p -> p @@ "**/*")
                |> Seq.toList

            let cliFileIncludes =
                { BaseDirectory = config.baseDirectory
                  Includes = match config.fileIncludes with [] -> ["**/*"] | es -> es
                  Excludes = excludedPatterns @ config.fileExcludes @ projectFileExcludes }

            let gitIgnorePatterns = 
                match GitIgnore.tryFindGitIgnoreFile config.baseDirectory with
                | None -> None
                | Some gitIgnoreFile -> GitIgnore.parse gitIgnoreFile |> Some

            match gitIgnorePatterns with
            | None -> Seq.toList cliFileIncludes
            | Some g -> 
                // we apply .gitignore as a separate globbing pattern as relative paths might be different
                cliFileIncludes |> Seq.filter g.IsMatch |> Seq.toList

    NoSln.CreateSolution
       (baseDirectory = config.baseDirectory,
        projects = projects,
        files = files,
        targetSolutionFile = config.targetSolutionFile,
        includeTransitiveP2pReferences = not config.noTransitiveProjects,
        useAbsolutePaths = config.useAbsolutePaths,
        flattenSolutionFolders = config.flattenProjects)