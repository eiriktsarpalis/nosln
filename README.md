# dotnet-nosln [![Build Status](https://travis-ci.org/eiriktsarpalis/nosln.svg?branch=master)](https://travis-ci.org/eiriktsarpalis/nosln) [![NuGet](https://img.shields.io/nuget/vpre/dotnet-nosln.svg)](https://www.nuget.org/packages/dotnet-nosln/) [![license](https://img.shields.io/github/license/eiriktsarpalis/nosln.svg)](License.md)

dotnet-nosln (pronounced "noslyn") is a cli tool that generates solution files. 
Designed to minimize the awkwardness of solution files, 
nosln treats them as disposable, auto-generated entities deriving from the file system.

To use nosln, simply navigate to your favorite repo and type
```
$ dotnet nosln --start
```
This will will automatically generate a solution file based on the folder structure in your current directory,
then immediately start Visual Studio using that particular solution file.

It will also include any solution items that happen to exist in the particular folder hierarchy, respecting any `.gitignore` files that happen to exist in the repo.

Running
```
$ dotnet nosln -TF -I '**/*Tests*' -o tests.sln
```
will create a solution file containing test projects only.

## Building & Installing

To install nosln on your machine, just clone the repo and run
```
make install
```
GNU make is required.

## More arguments

Full list of all nosln command line arguments:
```
USAGE: dotnet nosln [--help] [--version] [--output <solution file>] [--include-projects <pattern>]
                    [--exclude-projects <pattern>] [--include-files <pattern>] [--exclude-files <pattern>]
                    [--no-git-ignore] [--no-files] [--no-transitive-projects] [--absolute-paths] [--flatten]
                    [--start] [--quiet] [--temp] [--debug] [<path>]

PATH:

    <path>                Base directory or project file used for populating the solution file. All projects and
                          files within the directory will be added to the solution. Defaults to the current
                          directory.

OPTIONS:

    --version             Display the version string and exit.
    --output, -o <solution file>
                          Output solution file. Defaults to a solution file at the root of the supplied base
                          directory.
    --include-projects, -I <pattern>
                          Included project files globbing pattern. Multiple arguments are treated using OR semantics.
    --exclude-projects, -E <pattern>
                          Excluded project files globbing pattern. Multiple arguments are treated using OR semantics.
    --include-files, -i <pattern>
                          Included solution files globbing pattern. Multiple arguments are treated using OR
                          semantics.
    --exclude-files, -e <pattern>
                          Excluded solution files globbing pattern. Multiple arguments are treated using OR
                          semantics.
    --no-git-ignore, -G   Do not take .gitignore files into account when excluding files.
    --no-files, -F        Do not include any solution items in the generated solution.
    --no-transitive-projects, -T
                          By default, nosln will include transitive p2p dependencies, even if excluded by a globbing
                          pattern or outside of the project directory.Enable this flag to avoid expanding to
                          transitive projects.
    --absolute-paths, -a  Use absolute paths in generated solution file. Otherwise contents will be relative to the
                          solution file output folder.
    --flatten, -f         Places all projects at the root of the solution file without replicating the filesystem
                          structure. Also implies --no-files.
    --start, -s           Starts an IDE process with the newly generated solution file. Uses 'start' command on
                          Windows, 'open' on OSX, 'xdg-open' on Linux.
    --quiet, -q           Quiet mode, only output the file name of the generated solution to stdout. Useful for
                          passing generated solution to shell scripts.
    --temp, -t            Creates a disposable solution file in the system temp folder. Overrides the --output
                          argument. Also implies --absolute-paths.
    --debug, -D           Generate debug logs. Overrides --quiet mode.
    --help                display this list of options.
```
