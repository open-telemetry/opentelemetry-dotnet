ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.422@sha256:63ebabdcde24cc8134304a6f50719cf1e22bb4e9f7148ac4ef967c7680187356 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.315@sha256:24fe7c00719afc72e2b67cace264dea2ee39ae45f5b553bd59493fe3ddfc920f AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.301@sha256:ea8bde36c11b6e7eec2656d0e59101d4462f6bd630730f2c8201ed0572b295d5 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.28@sha256:e78fda31142e28746a6908e288e0d40346793f691ca99d8d150bcbe95c0ef035 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.17@sha256:193650562ae3ccfcdbb00a6bccbef2ef5bd71d4ceb25e971475ae4c0a23271fd AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.9@sha256:7644f992230d35cf230017189d4038c0ae0f7388b13f4f7ae1900a155bafb597 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
