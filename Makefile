SOURCE_DIRECTORY := $(dir $(realpath $(lastword $(MAKEFILE_LIST))))

ARTIFACT_PATH := $(SOURCE_DIRECTORY)artifacts
LIBRARY_PROJECT := src/NoSln
NETTOOL_PROJECT := src/NoSln.Tool
TOOL_PATH := $(SOURCE_DIRECTORY)tools
NOSLN := $(TOOL_PATH)/dotnet-nosln
CONFIGURATION ?= Release
NUGET_SOURCE ?= "https://api.nuget.org/v3/index.json"
NUGET_API_KEY ?= ""

DOCKER_MAKE_TARGETS ?= test
DOCKER_IMAGE_LABEL ?= docker-nosln-build
DOCKER_CONTAINER_NAME ?= docker-nosln-build-container

clean:
	dotnet clean -c $(CONFIGURATION) $(NETTOOL_PROJECT) && rm -rf $(TOOL_PATH) && rm -rf $(ARTIFACT_PATH) && rm -rf *.slnx

minver: clean
	dotnet tool restore
	dotnet minver

build: minver
	dotnet restore $(LIBRARY_PROJECT)
	dotnet restore $(NETTOOL_PROJECT)
	dotnet pack --no-restore -c $(CONFIGURATION) -o $(ARTIFACT_PATH) $(LIBRARY_PROJECT)
	dotnet pack --no-restore -c $(CONFIGURATION) -o $(ARTIFACT_PATH) $(NETTOOL_PROJECT)
	dotnet tool install --add-source $(ARTIFACT_PATH) --tool-path $(TOOL_PATH) --version `dotnet minver -v e` dotnet-nosln

test: build
	$(NOSLN) -D -o nosln.slnx
	dotnet build nosln.slnx -c $(CONFIGURATION)
	dotnet test nosln.slnx -c $(CONFIGURATION) --no-build
	$(NOSLN) -D src/ -o $(ARTIFACT_PATH)/src.slnx
	$(NOSLN) -D examples/ -o $(ARTIFACT_PATH)/examples.slnx
	$(NOSLN) -DFT -I 'tests/**/*' -o $(ARTIFACT_PATH)/tests.slnx

install: build
	dotnet tool update --add-source $(ARTIFACT_PATH) -g dotnet-nosln --version `dotnet minver -v e`

uninstall:
	dotnet tool uninstall -g dotnet-nosln

push: test
	for nupkg in `ls $(ARTIFACT_PATH)/*.nupkg`; do \
		dotnet nuget push $$nupkg -s $(NUGET_SOURCE) -k $(NUGET_API_KEY); \
	done

docker-build:
	docker build -t $(DOCKER_IMAGE_LABEL) .
	docker run --rm --name $(DOCKER_CONTAINER_NAME) \
		   $(DOCKER_IMAGE_LABEL) \
		   make $(DOCKER_MAKE_TARGETS) \
		   NUGET_API_KEY=$(NUGET_API_KEY)

all: test

.DEFAULT_GOAL := test
