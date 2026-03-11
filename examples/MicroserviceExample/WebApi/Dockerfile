ARG SDK_VERSION=11.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.200@sha256:ea13841f10c6c410a6df63c6c97ab549dfd2b5fcfff1c00186531ba30208117d AS dotnet-sdk-10.0
FROM mcr.microsoft.com/dotnet/sdk:11.0-preview@sha256:97f9050efe8b816bd5e616997f754f31ab8ccbdd536272c639f94b1c23d96c53 AS dotnet-sdk-11.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net11.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.4@sha256:8b75cdf59a5068d9adfd8a6d202cc7671b2dc8f5f46c51e3b88a0a632e8fad1f AS dotnet-aspnet-10.0
FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview@sha256:e74f6c63a18661d28eeb39ec7990f649da73cb099bf813aee956b8020c843a72 AS dotnet-aspnet-11.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
