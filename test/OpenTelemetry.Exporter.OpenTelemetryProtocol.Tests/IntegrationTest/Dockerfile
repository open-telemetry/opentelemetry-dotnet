# Create a container for running the OpenTelemetry Collector integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/IntegrationTest/Dockerfile

ARG BUILD_SDK_VERSION=9.0
ARG TEST_SDK_VERSION=9.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.410@sha256:b56053d0a8f4627047740941396e76cd9e7a9421c83b1d81b68f10e5019862d7 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.300@sha256:c5b188baf837b0180a14f988815b9cc7a55b836dd6cbe1b6e6523cf3098faaa8 AS dotnet-sdk-9.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net9.0
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests"
RUN dotnet publish "OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true

FROM dotnet-sdk-${TEST_SDK_VERSION} AS final
WORKDIR /test
COPY --from=build /drop .

RUN apt-get update && apt-get install ca-certificates
