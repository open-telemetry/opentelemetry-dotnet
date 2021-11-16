# Create a container for running the OpenTelemetry SQL Client integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.SqlClient.Tests/Dockerfile .

ARG SDK_VERSION=6.0
FROM mcr.microsoft.com/dotnet/sdk:6.0-focal AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net6.0
WORKDIR /repo
COPY . ./
RUN ls -la /repo
WORKDIR "/repo/test/OpenTelemetry.Instrumentation.SqlClient.Tests"
RUN dotnet publish "OpenTelemetry.Instrumentation.SqlClient.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true -p:TARGET_FRAMEWORK=${PUBLISH_FRAMEWORK}

FROM mcr.microsoft.com/dotnet/sdk:${SDK_VERSION} AS final
ADD https://github.com/ufoscout/docker-compose-wait/releases/download/2.7.3/wait /wait
RUN chmod +x /wait
WORKDIR /test
COPY --from=build /drop .
ENTRYPOINT ["dotnet", "vstest", "OpenTelemetry.Instrumentation.SqlClient.Tests.dll"]
