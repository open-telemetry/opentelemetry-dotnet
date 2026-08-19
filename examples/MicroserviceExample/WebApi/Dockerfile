ARG SDK_VERSION=11.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.400@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS dotnet-sdk-10.0
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.7@sha256:fd96d62893be03527912ce16b32e4331a64e6cf93393129f783a59b11e651790 AS dotnet-sdk-11.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net11.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS dotnet-aspnet-10.0
FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-preview.7@sha256:1d636bd12f01151eaf001803fbd24a4afb35e22e086d00cdb25df97c85b36a1e AS dotnet-aspnet-11.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
