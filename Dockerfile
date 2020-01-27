FROM eiriktsarpalis/dotnet-sdk-mono:3.1.101-buster

RUN apt-get update && \
    apt-get install -y make && \
    rm -rf /var/lib/apt/lists/*

# Build bits
WORKDIR /app
COPY . /app

CMD make
