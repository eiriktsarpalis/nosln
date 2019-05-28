FROM eiriktsarpalis/dotnet-sdk-mono:2.2.204-stretch

RUN apt-get update && \
    apt-get install -y make && \
    rm -rf /var/lib/apt/lists/*

# Build bits
WORKDIR /app
COPY . /app

ENV NUGET_API_KEY unspecified
ENV TARGETS test

CMD make $TARGETS NUGET_VERSION=$NUGET_VERSION NUGET_API_KEY=$NUGET_API_KEY