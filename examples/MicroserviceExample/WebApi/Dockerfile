ARG SDK_VERSION=11.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:4ea6fe75dd36706bb6d8c3c293d4c4315840f5d76ea28ac97def77e3ec487fa5 AS dotnet-sdk-10.0
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1@sha256:86afb5e989113ab0c32f04002635ff1c6fe0620cbb8ca0e22d2db1e151384a3f AS dotnet-sdk-11.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net11.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:1fe86375600b62e6566b465da9553eef0621f13c67f40fe764cd8dbb1dee1497 AS dotnet-aspnet-10.0
FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.1@sha256:656770db703ebbc4f5115104095573677d9a21a906fcccc16393d18ea949a49f AS dotnet-aspnet-11.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
