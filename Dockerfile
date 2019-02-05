FROM microsoft/dotnet:2.1.401-sdk-stretch

# Set the locale; suppress perl warnings
ENV LC_ALL=en_US.UTF-8
RUN apt-get update && \
    apt-get install -y locales make && \
    echo "en_US.UTF-8 UTF-8" > /etc/locale.gen && \
    locale-gen en_US.UTF-8 && \
    /usr/sbin/update-locale LANG=en_US.UTF-8 && \
    rm -rf /var/lib/apt/lists/*

# Install mono; required for net4x builds
ENV FrameworkPathOverride=/usr/lib/mono/4.7.1-api/
RUN apt-get -y update && \
    apt-get -y install apt-transport-https dirmngr && \
        apt-key adv --no-tty --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF && \
        echo "deb https://download.mono-project.com/repo/debian stable-stretch main" | tee /etc/apt/sources.list.d/mono-official-stable.list && \
        apt-get -y update && \
        apt-get -y --no-install-recommends install mono-devel ca-certificates-mono && \
        rm -rf /var/lib/apt/lists/*

# Build bits
WORKDIR /app
COPY . /app

ENV MINVER_VERSION 1.0.0-beta.2
ENV NUGET_SOURCE "https://artifacts.notjet.net/api/nuget/nuget/jenkins"
ENV NUGET_API_KEY "undefined"

CMD export PATH="$PATH:$HOME/.dotnet/tools" && \
    dotnet tool install -g minver-cli --version $MINVER_VERSION && \
    NUGET_VERSION=$(minver) && \
    echo "##$(echo vso)[build.updatebuildnumber]$NUGET_VERSION" && \
    make push NUGET_VERSION=$NUGET_VERSION NUGET_SOURCE=$NUGET_SOURCE NUGET_API_KEY=$NUGET_API_KEY