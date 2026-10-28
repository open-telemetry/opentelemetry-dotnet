ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.427@sha256:ff39a765f04619c8d146796fc09b38499ae5984f4a2a4c3a0a57d0022855daee AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.320@sha256:d7cfd59385751fc87897a138ba173fb1bbc2bf946ea78aecc91a119993676997 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.403@sha256:afd0ccff9d306812a69ef0f47458376b6acb544ae866cfc4b8d28ff0eef35ea7 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.33@sha256:63a1e42746a20795273c219f1577a8826644f12f67d829e818d8ccec92915493 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.22@sha256:5eb35372c5f2b0295bddbd39758cac802178a2858945138e770e3bbdc75e61fa AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14@sha256:d247d17dd429d96580eeb96cfce220c1089d860dbbf9f94d599ca64cf3604fe4 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
