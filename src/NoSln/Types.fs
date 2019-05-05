namespace rec NoSln

open System

/// Solution tree representation
type Solution =
    {
        /// Solution UUID
        id : Guid
        /// Root folders in solution
        folders : SolutionFolder list
        /// Root projects in solution
        projects : SolutionProject list
        /// Filesystem path to solution file
        /// All solution projects/files referenced relative to this
        targetSolutionFile : string
    }

/// Solution folder representation
type SolutionFolder =
    {
        /// Folder UUID
        id : Guid
        /// Project type UUID
        projectTypeGuid : Guid
        /// Folder name
        name  : string
        /// Folders contained within folder
        folders : SolutionFolder list
        /// Projects contained within folder
        projects : SolutionProject list
        /// Files contained within folder
        files : SolutionFile list
    }

/// Solution project representation
type SolutionProject =
    {
        /// Project UUID
        id : Guid
        /// Project name
        name : string
        /// Project type UUID
        projectTypeGuid : Guid
        /// P2P references of project
        p2pReferences : string list
        /// Fully qualified filesystem path
        fullPath : string
        /// Filesystem path relative to solution file
        relativePath : string
        /// Logical path of solution item
        logicalPath : string list
    }

/// Solution file representation
type SolutionFile =
    {
        /// File identifier
        id : string
        /// Fully qualified filesystem path
        fullPath : string
        /// Filesystem path relative to solution file
        relativePath : string
        /// Logical path of solution item
        logicalPath : string list
    }


exception NoSlnException of message:string
  with override e.Message = e.message