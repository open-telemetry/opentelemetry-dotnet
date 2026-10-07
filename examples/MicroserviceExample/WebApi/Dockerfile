ARG SDK_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.425@sha256:ec9c0a0dc5f60adc2065762050638dd5ed5facfc53716d4580a42d86027c8e80 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.318@sha256:1330d3c4144aec2695036a125d9a3b49a1fc555c9024c981248fa83d40fa8b23 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.31@sha256:a3cd573ac05cf88ca496e3309cb7f7c44aa16664352d1ee41eda93154f50cdbf AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.20@sha256:6b29f9d598b387b4b44653408d773cbdbf8f1d6b24289460b228a24f31a38b53 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
