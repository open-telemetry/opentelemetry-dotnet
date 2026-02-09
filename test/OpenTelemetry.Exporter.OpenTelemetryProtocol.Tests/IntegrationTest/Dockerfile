# Create a container for running the OpenTelemetry Collector integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/IntegrationTest/Dockerfile

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.417@sha256:1682f3cd0cb51464568de4fa88c719bd3be55539653d97540718bdd5b87062d4 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.310@sha256:a574e6212c98ccf9cf2c6588201b1c71d93094f6f26c21375f75aa909a9e88f0 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.102@sha256:6ba533cc61a5d8c5e7d4b3a3e33e2ddc2efef200b112e4d658303516bfd24255 AS dotnet-sdk-10.0

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
