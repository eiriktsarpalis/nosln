# dotnet-nosln

Dotnet-NoSln (pronounced "Noslyn") is a small cli tool designed minimize the awkwardness of solution files.
Instead of having to manually maintain solution files, nosln treats solution files as disposable, 
auto-generated entities derived from a file system containing dotnet project files.

To use nosln, simply navigate to your favorite repo and type
```
$ dotnet nosln --start
```
This will will automatically generate a solution file based on the folder structure in your current directory,
then immediately start Visual Studio using that particular solution file.

## Building & Installing

To install nosln on your machine, you need to clone the repo and run
```
make install
```

## More argument

Full list of all nosln command line arguments:
```
USAGE: dotnet nosln [--help] [--version] [--output <solution file>] [--include-projects <pattern>]
                    [--exclude-projects <pattern>] [--include-files <pattern>]
                    [--exclude-files <pattern>] [--no-files] [--no-transitive-projects]
                    [--absolute-paths] [--flatten] [--start] [--quiet] [--temp] [<path>]

PATH:

    <path>                Base directory used for populating the solution file. All projects and
                          files within the directory will be added to the solution. Defaults to
                          the current directory.

OPTIONS:

    --version             Display the version string and exit.
    --output, -o <solution file>
                          Output solution file. Defaults to a solution file at the root of the
                          supplied base directory.
    --include-projects, -I <pattern>
                          Included project files globbing pattern.
    --exclude-projects, -E <pattern>
                          Excluded project files globbing pattern.
    --include-files, -i <pattern>
                          Included solution files globbing pattern.
    --exclude-files, -e <pattern>
                          Excluded solution files globbing pattern.
    --no-files, -F        Do not include any solution items in the generated solution.
    --no-transitive-projects, -T
                          By default, nosln will include transitive p2p dependencies, even if
                          excluded by a globbing pattern or outside of the project
                          directory.Enable this flag to avoid expanding to transitive projects.
    --absolute-paths, -a  Use absolute paths in generated solution file. Otherwise contents will
                          be relative to the solution file output folder.
    --flatten, -f         Places all projects at the root of the solution file without
                          replicating the filesystem structure. Also implies --no-files.
    --start, -s           For windows systems, starts a Visual Studio process with the newly
                          generated solution file.
    --quiet, -q           Quiet mode, only output the file name of the generated solution to
                          stdout. Useful for passing generated solution to shell scripts.
    --temp, -t            Creates a disposable solution file in the system temp folder. Overrides
                          the --output argument. Also implies --absolute-paths.
    --help                display this list of options.
```