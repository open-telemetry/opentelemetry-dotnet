ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.419@sha256:312aeb1357b2461f5a729b5ca38a54b8381c8e7880d0672585e8613cdc972096 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.312@sha256:0cbece2e210d064199e7ab3bb9ac695c5ed8bfb35b31eee8d80e051abfbef22b AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.200@sha256:ea13841f10c6c410a6df63c6c97ab549dfd2b5fcfff1c00186531ba30208117d AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.25@sha256:a1a6ed414bde21fce19c1f84e7b4c17d8020d9a82dad55a8adb3775ae5be644d AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.14@sha256:cec7519c080fff418bcce2e07c44141e4eba866f9801e2b35ef7567f487b207b AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.4@sha256:8b75cdf59a5068d9adfd8a6d202cc7671b2dc8f5f46c51e3b88a0a632e8fad1f AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
