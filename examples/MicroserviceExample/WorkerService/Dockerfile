ARG SDK_VERSION=9.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.414@sha256:3cef19377b2ef2a0171e930a24627677447c3e41b5f2eab84ff4895f1b15d254 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.304@sha256:f57307946b712ecf86561b3a202053cca07ea0004b782bd8223e213b67b517cd AS dotnet-sdk-9.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net9.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.19@sha256:6d8901fae2d8f44c0e73962046a67fbdaf017bc8a6833c74ae87e9fbd0810b73 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.8@sha256:2b70ef1451cb01c2118969ad15fd0ae40fdbac158f09699fefae0dc32c4ca352 AS dotnet-aspnet-9.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
