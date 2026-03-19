ARG SDK_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.419@sha256:a9330090b730c2abde8f3f43b3d1e24e4b8cba028de7bab1a7fdcd50cb7a3b7e AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.312@sha256:13bd3b19e637289720c0ac75d08f0aefa6a4dd933b27a61c2289fd3d26330f34 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.201@sha256:478b9038d187e5b5c29bfa8173ded5d29e864b5ad06102a12106380ee01e2e49 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.25@sha256:d4d80bf500f4c8307e5c44bf61eb58aec027da07c4d1c40816846fe5eef3f34d AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.14@sha256:3760f76b46d7ec683786c1e653c29b1ef4bc959ef965520b814de555d26e38fe AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.5@sha256:a04d1c1d2d26119049494057d80ea6cda25bbd8aef7c444a1fc1ef874fd3955b AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
