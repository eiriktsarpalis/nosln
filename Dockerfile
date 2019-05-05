FROM andrewlock/dotnet-mono:2.1.504-sdk

RUN apt-get update && \
    apt-get install -y make && \
    rm -rf /var/lib/apt/lists/*

# Enables building and testing of net4x projects using mono sdk
ENV FrameworkPathOverride=/usr/lib/mono/4.7.1-api/

# Build bits
WORKDIR /app
COPY . /app

ENV NUGET_API_KEY unspecified
ENV TARGETS test

CMD make $TARGETS NUGET_VERSION=$NUGET_VERSION NUGET_API_KEY=$NUGET_API_KEY