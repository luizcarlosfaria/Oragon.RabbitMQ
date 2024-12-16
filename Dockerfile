FROM mcr.microsoft.com/dotnet/sdk:9.0

RUN export PATH="$PATH:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-sonarscanner
RUN dotnet tool install --global dotnet-coverage


# Default to UTF-8 file.encoding
ENV LANG C.UTF-8

# Install docker (cli and engine)
RUN curl -fsSL https://get.docker.com -o get-docker.sh && sh get-docker.sh && rm get-docker.sh


#ENV JAVA_HOME /usr/local/openjdk-17

#ENV PATH $JAVA_HOME/bin:$PATH

RUN apt-get update && \
apt-get install -y --no-install-recommends openjdk-17-jdk

