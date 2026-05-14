ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.421@sha256:ebecce75fb42c4c14db108f88054d20a93b0b9e6dfbacad56c8f744c342cf9ef AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.314@sha256:087fc98e5c6ffcea6c3e276c135c4a6717c589d9509a09cc22e7c634830a4db8 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.300@sha256:dc8430e6024d454edadad1e160e1973be3cabbb7125998ef190d9e5c6adf7dbb AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.27@sha256:93154e00cb227f2fad30724455ecd1e77f46afc9a0273590af652ac220664e54 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.16@sha256:76b7df121eacae21642e94d70d7211b3865bab808ce51a5bbac5b85e5f196a2f AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.8@sha256:9b5222b0ff8e9eb991a7c1a64b25f0f771d21ccc05dfa1c834f5668ffd9cd73f AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
