# Create a container for running the OpenTelemetry W3C Trace Context tests https://github.com/w3c/trace-context/tree/main/test.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.424-azurelinux3.0@sha256:072fecca7ce6215cb7d6c8c5ae0bf3ac7135adb642123035f529d0fea8ca73eb AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.317-azurelinux3.0@sha256:c9c558d6730a709144f578823ac0a3934eb7715bc8e18c5c1e5072bf42b3e0de AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.400-azurelinux3.0@sha256:d1348cda1563f5dc1a1d419023a2067389a3dbe7a1e0686913d9839f85053f00 AS dotnet-sdk-10.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0

# renovate: datasource=github-digest depName=w3c/trace-context packageName=w3c/trace-context currentValue=main
ENV W3C_TRACE_CONTEXT_SHA="acab820be9db7b3433668baa5cdd43f57f4c4be0"

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
