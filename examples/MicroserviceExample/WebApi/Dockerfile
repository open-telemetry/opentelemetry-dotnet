ARG SDK_VERSION=9.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.416@sha256:a87db299d259cec53210df406cd5e51f900a0c6d938fe56d5f392629c0505d75 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.307@sha256:1ea5b7f2154cbd1e20a336cb56957dba30a7ce30ef5c1c032a37c42801594713 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.100@sha256:c7445f141c04f1a6b454181bd098dcfa606c61ba0bd213d0a702489e5bd4cd71 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.22@sha256:e9684fdc74056bda84e8f5918c7211cac837a979a26b8a56c4e424f1ab94c80b AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.11@sha256:1b8d328201297fd5ca40dc1a0788aa9e813f58279c3410fe82dce6744c36d03f AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0@sha256:7c4246c1c384319346d45b3e24a10a21d5b6fc9b36a04790e1588148ff8055b0 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
