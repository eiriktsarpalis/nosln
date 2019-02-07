FROM microsoft/dotnet:2.2.103-sdk-stretch

RUN apt-get update && \
    apt-get install -y make && \
    rm -rf /var/lib/apt/lists/*

# Build bits
WORKDIR /app
COPY . /app

ENV MINVER_VERSION 1.0.0-beta.2
ENV NUGET_API_KEY unspecified
ENV TARGETS test

CMD export PATH="$PATH:$HOME/.dotnet/tools" && \
    dotnet tool install -g minver-cli --version $MINVER_VERSION && \
    NUGET_VERSION=$(minver) && \
    make $TARGETS NUGET_VERSION=$NUGET_VERSION NUGET_API_KEY=$NUGET_API_KEY