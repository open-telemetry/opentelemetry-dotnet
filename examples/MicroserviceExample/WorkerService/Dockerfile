ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.417@sha256:aa05b91be697b83229cb000b90120f0783604ad74ed92a0b45cdf3d1a9c873de AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.310@sha256:0d84f05256dec37a5d1739158fd5ea197b8ad3b4e8d0e32be47b754db5963a9e AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.102@sha256:25d14b400b75fa4e89d5bd4487a92a604a4e409ab65becb91821e7dc4ac7f81f AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.23@sha256:4b8f0b08534833b39bb662fb19a65e78cb086f5ca8dd35de3f87026de8885be4 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.12@sha256:8bbb7b9045f04a32d2eaad43b351e67f07c1f9604811dd505fc3654b4bec2176 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.2@sha256:1aacc8154bc3071349907dae26849df301188be1a2e1f4560b903fb6275e481a AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
