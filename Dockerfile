FROM mcr.microsoft.com/dotnet/sdk:10.0

USER root

RUN export PATH="$PATH:/root/.dotnet/tools" && \
dotnet tool install --global dotnet-sonarscanner && \
dotnet tool install --global dotnet-coverage

# Default to UTF-8 file.encoding
ENV LANG=C.UTF-8

# Testcontainers Docker-in-Docker configuration
ENV TESTCONTAINERS_RYUK_DISABLED=true
ENV TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal

# Install docker (cli and engine)
RUN curl -fsSL https://get.docker.com -o get-docker.sh && sh get-docker.sh && rm get-docker.sh

RUN apt-get update && \
apt-get install -y --no-install-recommends openjdk-21-jdk && \
apt clean

