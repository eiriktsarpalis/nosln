FROM mcr.microsoft.com/dotnet/sdk:10.0.302

RUN apt-get update && \
    apt-get install -y --no-install-recommends make && \
    rm -rf /var/lib/apt/lists/*

# Build bits
WORKDIR /app
COPY . /app

CMD make
