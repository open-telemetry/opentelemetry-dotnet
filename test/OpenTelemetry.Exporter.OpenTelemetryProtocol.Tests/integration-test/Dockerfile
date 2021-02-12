# Create a container for running the OpenTelemetry Collector integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/Dockerfile

ARG SDK_VERSION=5.0
FROM mcr.microsoft.com/dotnet/sdk:5.0-focal AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net5.0
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests"
RUN dotnet publish "OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true -p:TARGET_FRAMEWORK=${PUBLISH_FRAMEWORK}

FROM mcr.microsoft.com/dotnet/sdk:${SDK_VERSION} AS final
WORKDIR /test
COPY --from=build /drop .

RUN apt-get update && apt-get install ca-certificates

# This is for testing https when using the Google gRPC library (apps prior to .NET Core 3.0).
ENV GRPC_DEFAULT_SSL_ROOTS_FILE_PATH /usr/local/share/ca-certificates/otel-collector.crt
