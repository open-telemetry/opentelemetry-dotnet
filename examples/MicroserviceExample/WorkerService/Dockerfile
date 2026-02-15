ARG SDK_VERSION=11.0

FROM mcr.microsoft.com/dotnet/sdk:10.0.103@sha256:0a506ab0c8aa077361af42f82569d364ab1b8741e967955d883e3f23683d473a AS dotnet-sdk-10.0
FROM mcr.microsoft.com/dotnet/sdk:11.0-preview@sha256:c5aa0a301759a1b07654450e10e3ec78c3d476d0537263ca1ab5850d9f302fca AS dotnet-sdk-11.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net11.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.3@sha256:52dcfb4225fda614c38ba5997a4ec72cbd5260a624125174416e547ff9eb9b8c AS dotnet-aspnet-10.0
FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview@sha256:65a932bd7101f50eb99f8b45e2c970851092e0090304a48faf1c9f6c456d60ff AS dotnet-aspnet-11.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
