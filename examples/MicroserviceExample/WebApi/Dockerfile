ARG SDK_VERSION=11.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.201@sha256:478b9038d187e5b5c29bfa8173ded5d29e864b5ad06102a12106380ee01e2e49 AS dotnet-sdk-10.0
FROM mcr.microsoft.com/dotnet/sdk:11.0-preview@sha256:97f9050efe8b816bd5e616997f754f31ab8ccbdd536272c639f94b1c23d96c53 AS dotnet-sdk-11.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net11.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.5@sha256:a04d1c1d2d26119049494057d80ea6cda25bbd8aef7c444a1fc1ef874fd3955b AS dotnet-aspnet-10.0
FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview@sha256:e74f6c63a18661d28eeb39ec7990f649da73cb099bf813aee956b8020c843a72 AS dotnet-aspnet-11.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
