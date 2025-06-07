ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.414@sha256:3cef19377b2ef2a0171e930a24627677447c3e41b5f2eab84ff4895f1b15d254 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.304@sha256:f57307946b712ecf86561b3a202053cca07ea0004b782bd8223e213b67b517cd AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-preview.4@sha256:0c90f5c8dfb1d4a666116cf27ea75bb081e800c6f6228a5c4a33ff32d727e254 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.20@sha256:e88f90b6d9fd7e9e0d8e231d068fccdbebd3c91892441a85ef35066aea9a4e1e AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.8@sha256:49ae5b354037606b2339b723ff9a3be7827eb53e513dfd683cf39a78cabb6a2b AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0-preview.4@sha256:898d64af8cb7d440f82432a42dfcdf7d6e87feccc4225c46736cbc3976cb9303 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
