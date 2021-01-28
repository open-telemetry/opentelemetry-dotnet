# Create a container for running the OpenTelemetry Collector integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/Dockerfile

ARG SDK_VERSION=3.1
FROM mcr.microsoft.com/dotnet/core/sdk:${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=netcoreapp3.1
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests"
RUN dotnet publish "OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true -p:TARGET_FRAMEWORK=${PUBLISH_FRAMEWORK}

FROM mcr.microsoft.com/dotnet/core/sdk:${SDK_VERSION} AS final
WORKDIR /test
COPY --from=build /drop .
ENTRYPOINT ["dotnet", "vstest", "OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.dll"]
