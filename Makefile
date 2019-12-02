SOURCE_DIRECTORY := $(dir $(realpath $(lastword $(MAKEFILE_LIST))))

ARTIFACT_PATH := $(SOURCE_DIRECTORY)artifacts
LIBRARY_PROJECT := src/NoSln
NETTOOL_PROJECT := src/NoSln.Tool
TOOL_PATH := $(SOURCE_DIRECTORY)tools
VERSION_FILE := $(TOOL_PATH)/version
PATH := $(PATH):$(TOOL_PATH)
CONFIGURATION ?= Release
NUGET_SOURCE ?= "https://api.nuget.org/v3/index.json"
NUGET_API_KEY ?= ""

clean:
	dotnet clean -c $(CONFIGURATION) $(NETTOOL_PROJECT) && rm -rf $(TOOL_PATH) && rm -rf $(ARTIFACT_PATH) && rm -rf *.sln

minver: clean
	dotnet tool restore
	mkdir -p $(TOOL_PATH)
	dotnet minver > $(VERSION_FILE)

build: minver
	dotnet pack -c $(CONFIGURATION) -o $(ARTIFACT_PATH) -p:Version=`cat $(VERSION_FILE)` $(LIBRARY_PROJECT)
	dotnet pack -c $(CONFIGURATION) -o $(ARTIFACT_PATH) -p:Version=`cat $(VERSION_FILE)` $(NETTOOL_PROJECT)
	dotnet tool install --add-source $(ARTIFACT_PATH) --tool-path $(TOOL_PATH) dotnet-nosln --version `cat $(VERSION_FILE)`

test: build
	dotnet nosln -D -o nosln.sln
	dotnet test -c $(CONFIGURATION)
	dotnet nosln -D src/ -o $(ARTIFACT_PATH)/src.sln
	dotnet nosln -D examples/ -o $(ARTIFACT_PATH)/examples.sln
	dotnet nosln -DFT -I 'tests/**/*' -o $(ARTIFACT_PATH)/tests.sln

install: build
	dotnet tool install --add-source $(ARTIFACT_PATH) -g dotnet-nosln --version `cat $(VERSION_FILE)`

uninstall:
	dotnet tool uninstall -g dotnet-nosln

push: test
	for nupkg in `ls $(ARTIFACT_PATH)/*.nupkg`; do \
		dotnet nuget push $$nupkg -s $(NUGET_SOURCE) -k $(NUGET_API_KEY); \
	done

all: test

.DEFAULT_GOAL := build
