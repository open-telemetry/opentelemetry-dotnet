# Create a container for running the OpenTelemetry Collector integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/IntegrationTest/Dockerfile

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.423@sha256:89ce6291bde9acdf59594e79fb8277c6d84c46e4b1f5bf126a4f18766e4bd597 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.316@sha256:ee0d20fbc3dfc60ae2d9d9115a9b750c18576a47f24073119dd09d93e072cb89 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.302@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664 AS dotnet-sdk-10.0

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
