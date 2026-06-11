# Create a container for running the OpenTelemetry W3C Trace Context tests https://github.com/w3c/trace-context/tree/master/test.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=11.0
ARG TEST_SDK_VERSION=11.0

FROM mcr.microsoft.com/dotnet/sdk:10.0.301-azurelinux3.0@sha256:b9ba3498cd0ea65e665bbc921f91215309d007a5c2e407a79ba61aa627975b5e AS dotnet-sdk-10.0
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.5-azurelinux3.0@sha256:9e2543d8246f5530a1d17d9fa46d266826a8f6b7a402daaf56e2b9d084a53d8b AS dotnet-sdk-11.0
FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net11.0

# renovate: datasource=github-digest depName=w3c/trace-context packageName=w3c/trace-context
ENV W3C_TRACE_CONTEXT_SHA="e37efba66396bff25b737b33bce3c117cc6eb287"

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
