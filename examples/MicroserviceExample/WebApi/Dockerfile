ARG SDK_VERSION=8.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.407@sha256:2d7f935b8c7fe032cd3d36b5ce9c82c24413881e6dad1b4fbdf36cf369e4244f AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.202@sha256:d7f4691d11f610d9b94bb75517c9e78ac5799447b5b3e82af9e4625d8c8d1d53 AS dotnet-sdk-9.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net8.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.8@sha256:30d8619d9a4f68508d9b17fc2088e857e629d3f9ceaaf57c22d6747f7326d89e AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.3@sha256:69d0eb9d3182372d0509630cb4996085ccafb3bed75e9adec68604640bf725d3 AS dotnet-aspnet-9.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
