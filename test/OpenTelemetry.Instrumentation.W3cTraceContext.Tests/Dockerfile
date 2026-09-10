# Create a container for running the OpenTelemetry W3C Trace Context tests https://github.com/w3c/trace-context/tree/main/test.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=11.0
ARG TEST_SDK_VERSION=11.0

FROM mcr.microsoft.com/dotnet/sdk:10.0.401-azurelinux3.0@sha256:40195d80f8c217195a16f65c1be7e8acad646317e9bda2801d91df681c60b3f4 AS dotnet-sdk-10.0
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1-azurelinux4.0@sha256:a18d91296416f70a57a58c4871b2e07b610739648b418a1d8d53f763c757e0e8 AS dotnet-sdk-11.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net11.0

# renovate: datasource=github-digest depName=w3c/trace-context packageName=w3c/trace-context currentValue=main
ENV W3C_TRACE_CONTEXT_SHA="acab820be9db7b3433668baa5cdd43f57f4c4be0"

# HACK Workaround for https://github.com/dotnet/dotnet-docker/issues/7296
ENV OPENSSL_ENABLE_SHA1_SIGNATURES=1

WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests"
RUN dotnet publish "OpenTelemetry.Instrumentation.W3cTraceContext.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true

RUN git init /w3c/trace-context \
  && cd /w3c/trace-context \
  && git remote add origin https://github.com/w3c/trace-context.git \
  && git fetch --depth 1 origin "${W3C_TRACE_CONTEXT_SHA}" \
  && git checkout FETCH_HEAD \
  && rm -rf /w3c/trace-context/.git

FROM dotnet-sdk-${TEST_SDK_VERSION} AS final
WORKDIR /test
COPY /test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/requirements.txt .
COPY --from=build /drop .
COPY --from=build /w3c/trace-context ./trace-context
RUN tdnf install -y python3-pip \
  && ln -sf /usr/bin/python3 /usr/bin/python \
  && python3 -m pip install --requirement requirements.txt --require-hashes --break-system-packages \
  && tdnf clean all
ENV SPEC_LEVEL=2
ENV STRICT_LEVEL=2

ENTRYPOINT ["dotnet", "vstest", "OpenTelemetry.Instrumentation.W3cTraceContext.Tests.dll"]
