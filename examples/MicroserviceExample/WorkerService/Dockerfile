ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.418@sha256:bfb6ed602caa605141700aea7dc7d42574b74b704368e67d683c71a002123808 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.311@sha256:7ba12a329980dad8291cc7339ad073a8666ed949a78138eb8543a8cc8ccaa0de AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.103@sha256:e362a8dbcd691522456da26a5198b8f3ca1d7641c95624fadc5e3e82678bd08a AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.24@sha256:88c86e5469c2b69969e07343193fd3dbe4ff6936d95b994dbfa27348299af111 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.13@sha256:9f5100cdf26f7679ccf40dba48af9062a43e5ba0b243e80b324e8591f247b092 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.3@sha256:aec87aa74ddf129da573fa69f42f229a23c953a1c6fdecedea1aa6b1fe147d76 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
