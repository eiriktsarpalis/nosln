SOURCE_DIRECTORY := $(dir $(realpath $(lastword $(MAKEFILE_LIST))))

ARTIFACT_PATH := "$(SOURCE_DIRECTORY)artifacts"
PROJECT_PATH := src/NoSln
TOOL_PATH := "$(SOURCE_DIRECTORY)tools"
PATH := $(PATH):$(TOOL_PATH)
CONFIGURATION ?= Release
NUGET_VERSION ?= 0.0.1
NUGET_SOURCE ?= ""
NUGET_API_KEY ?= ""
DOCKER_TAG = $(shell date +%s)

clean:
	dotnet clean -c $(CONFIGURATION) $(PROJECT_PATH) && rm -rf $(TOOL_PATH) && rm -rf $(ARTIFACT_PATH) && rm -rf *.sln

build:
	dotnet pack -c $(CONFIGURATION) -o $(ARTIFACT_PATH) -p:Version=$(NUGET_VERSION) $(PROJECT_PATH)
	rm -rf $(TOOL_PATH) && dotnet tool install --add-source $(ARTIFACT_PATH) --tool-path $(TOOL_PATH) dotnet-nosln --version $(NUGET_VERSION)

test: build
	dotnet nosln -o nosln.sln
	dotnet nosln tests/ -o $(ARTIFACT_PATH)/tests.sln
	dotnet nosln src/ -o $(ARTIFACT_PATH)/src.sln

install: build
	dotnet tool install --add-source $(ARTIFACT_PATH) -g dotnet-nosln --version $(NUGET_VERSION)

uninstall:
	dotnet tool uninstall -g dotnet-nosln

push: build
	dotnet nuget push `ls $(ARTIFACT_PATH)/*.nupkg` -s $(NUGET_SOURCE) -k $(NUGET_API_KEY)

all: build

.DEFAULT_GOAL := build