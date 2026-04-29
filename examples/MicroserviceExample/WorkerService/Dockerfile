ARG SDK_VERSION=11.0

FROM mcr.microsoft.com/dotnet/sdk:10.0.203@sha256:8a90a473da5205a16979de99d2fc20975e922c68304f5c79d564e666dc3982fc AS dotnet-sdk-10.0
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.3@sha256:f57ec3eed4e19479b72b7933fb1926fbb06859349d183f9614b202fd00575d7a AS dotnet-sdk-11.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net11.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.7@sha256:55e37c7795bfaf6b9cc5d77c155811d9569f529d86e20647704bc1d7dd9741d4 AS dotnet-aspnet-10.0
FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-preview.3@sha256:9d4ba37b5bcc35adf8e691bdb3e632c0e0aa231f9efbabfda236e785651dab05 AS dotnet-aspnet-11.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
