#!/usr/bin/env bash

TAG=$(date +%s)
IMAGE_LABEL="docker-nosln-build:$TAG"
CONTAINER_NAME="docker-nosln-build-container-$TAG"

# docker build
docker build -t $IMAGE_LABEL .

# dotnet build, test & nuget publish
docker run \
	-e NUGET_API_KEY=$NUGET_API_KEY \
	$IMAGE_LABEL

exit_code=$?

# clean up
docker rm -f $CONTAINER_NAME

exit $exit_code