namespace NoSln

type Configuration =
    {
        baseDirectory : string
        targetSolutionFile : string
        targetSolutionDir : string

        projectIncludes : string list
        projectExcludes : string list
        gitIgnoreFile : string option
        fileIncludes : string list
        fileExcludes : string list        

        noFiles : bool
        noTransitiveProjects : bool
        useAbsolutePaths : bool
        flattenProjects : bool
        start : bool
        quiet : bool
        debug : bool
    }