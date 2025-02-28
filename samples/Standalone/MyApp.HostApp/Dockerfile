# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER root
RUN DEBIAN_FRONTEND=noninteractive TZ=America/Sao_Paulo apt-get update && apt-get -y install tzdata
ENV TZ=America/Sao_Paulo
RUN cat /usr/share/zoneinfo/$TZ > /etc/localtime \
&& cat /usr/share/zoneinfo/$TZ > /etc/timezone
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["./MyApp.props", "./"]
COPY ["MyApp.HostApp/MyApp.HostApp.csproj", "MyApp.HostApp/"]
COPY ["MyApp.Application/MyApp.Application.csproj", "MyApp.Application/"]
COPY ["MyApp.ApplicationContracts/MyApp.ApplicationContracts.csproj", "MyApp.ApplicationContracts/"]
RUN dotnet restore "./MyApp.HostApp/MyApp.HostApp.csproj"
COPY . .
WORKDIR "/src/MyApp.HostApp"
RUN dotnet build "./MyApp.HostApp.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MyApp.HostApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyApp.HostApp.dll"]
