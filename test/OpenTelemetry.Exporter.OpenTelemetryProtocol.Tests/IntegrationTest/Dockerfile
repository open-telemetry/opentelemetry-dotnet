# Create a container for running the OpenTelemetry Collector integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/IntegrationTest/Dockerfile

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.422@sha256:ff435ddea88d0d2812a4b8d3b4ab833a8e75256aea5c444a841a84847ba8c8a4 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.315@sha256:06da3f01ec505b4f365497e1952c7ffcb748e536ac60da463ae6766814cfd9c6 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.301@sha256:548d93f8a18a1acbe6cc127bc4f47281430d34a9e35c18afa80a8d6741c2adc3 AS dotnet-sdk-10.0

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
