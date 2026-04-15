ARG SDK_VERSION=11.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.202@sha256:adc02be8b87957d07208a4a3e51775935b33bad3317de8c45b1e67357b4c073b AS dotnet-sdk-10.0
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.2@sha256:98c96a6331e1ab4eb73abe6ba341807d686d2ab24a915c617497a8b5995eefe9 AS dotnet-sdk-11.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net11.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.6@sha256:9271888dde1f4408dfb7ce75bc0f513c903d20ff2f1287ab153b641d4588ec7d AS dotnet-aspnet-10.0
FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-preview.2@sha256:9546302bd8d9700170fb88274c99bb3ddb1a9afb34921ff1957e17b6127edba3 AS dotnet-aspnet-11.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
