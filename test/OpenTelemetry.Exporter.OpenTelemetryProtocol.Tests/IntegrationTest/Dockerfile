# Create a container for running the OpenTelemetry Collector integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/IntegrationTest/Dockerfile

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.416@sha256:2c7794fef0107c1815193dc157ed3f8ca60eb504a439806a29b6f9bf1ce73679 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.308@sha256:51060ced0e5dc1282a587a092783132ba8b0ec25150d3bc61e99bf0ef2d62593 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.101@sha256:d1823fecac3689a2eb959e02ee3bfe1c2142392808240039097ad70644566190 AS dotnet-sdk-10.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests"
RUN dotnet publish "OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true

FROM dotnet-sdk-${TEST_SDK_VERSION} AS final
WORKDIR /test
COPY --from=build /drop .

RUN apt-get update && apt-get install ca-certificates
