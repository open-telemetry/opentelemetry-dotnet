# Create a container for running the OpenTelemetry Collector integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/IntegrationTest/Dockerfile

ARG BUILD_SDK_VERSION=11.0
ARG TEST_SDK_VERSION=11.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.202@sha256:adc02be8b87957d07208a4a3e51775935b33bad3317de8c45b1e67357b4c073b AS dotnet-sdk-10.0
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.2@sha256:98c96a6331e1ab4eb73abe6ba341807d686d2ab24a915c617497a8b5995eefe9 AS dotnet-sdk-11.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net11.0
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests"
RUN dotnet publish "OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true

FROM dotnet-sdk-${TEST_SDK_VERSION} AS final
WORKDIR /test
COPY --from=build /drop .

RUN apt-get update && apt-get install ca-certificates
