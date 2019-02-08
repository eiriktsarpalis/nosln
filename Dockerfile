FROM microsoft/dotnet:2.2.103-sdk-stretch

RUN apt-get update && \
    apt-get install -y make && \
    rm -rf /var/lib/apt/lists/*

# Build bits
WORKDIR /app
COPY . /app

ENV NUGET_API_KEY unspecified
ENV TARGETS test

CMD make $TARGETS NUGET_VERSION=$NUGET_VERSION NUGET_API_KEY=$NUGET_API_KEY