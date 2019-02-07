#!/usr/bin/env bash

TARGETS=$1
IMAGE_LABEL=${2:-"docker-nosln-build"}
CONTAINER_NAME=${3:-"docker-nosln-build-container"}

# docker build
docker build -t $IMAGE_LABEL .

# dotnet build, test & nuget publish
docker run --name $CONTAINER_NAME \
           -e NUGET_API_KEY=$NUGET_API_KEY \
           -e TARGETS=$TARGETS \
		   $IMAGE_LABEL

exit_code=$?

# clean up
docker rm -f $CONTAINER_NAME

exit $exit_code