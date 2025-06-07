ARG SDK_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.411@sha256:e6a5a8d884609907fa8d468b927df765967f6b22f890ce92bd3ae614ca4ae87e AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.300@sha256:c5b188baf837b0180a14f988815b9cc7a55b836dd6cbe1b6e6523cf3098faaa8 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-preview.4@sha256:0c90f5c8dfb1d4a666116cf27ea75bb081e800c6f6228a5c4a33ff32d727e254 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.17@sha256:d5c0d91bc8fe887684b97d5409056270ed78cd23a5123436e842a8114a64d584 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.5@sha256:d5507d8292fb2469ec0b9913f3cd4bb8d5a014bd6dc00c33fd4c4e7310229f07 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0-preview.4@sha256:898d64af8cb7d440f82432a42dfcdf7d6e87feccc4225c46736cbc3976cb9303 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
