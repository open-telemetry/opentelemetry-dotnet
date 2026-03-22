# Create a container for running the OpenTelemetry W3C Trace Context tests https://github.com/w3c/trace-context/tree/master/test.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0

FROM ubuntu:24.04@sha256:186072bba1b2f436cbb91ef2567abca677337cfc786c86e107d25b7072feef0c AS w3c

# renovate: datasource=github-digest depName=w3c/trace-context packageName=w3c/trace-context
ENV W3C_TRACE_CONTEXT_SHA="34b10ac5af7f0caeb28efe35fe51cd4763ec5771"

# Install git
WORKDIR /w3c
RUN apt-get update \
  && apt-get install -y --no-install-recommends ca-certificates git \
  && rm -rf /var/lib/apt/lists/*
RUN git init trace-context \
  && cd trace-context \
  && git remote add origin https://github.com/w3c/trace-context.git \
  && git fetch --depth 1 origin "${W3C_TRACE_CONTEXT_SHA}" \
  && git checkout FETCH_HEAD \
  && rm -rf .git

FROM mcr.microsoft.com/dotnet/sdk:8.0.419@sha256:a9330090b730c2abde8f3f43b3d1e24e4b8cba028de7bab1a7fdcd50cb7a3b7e AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.312@sha256:13bd3b19e637289720c0ac75d08f0aefa6a4dd933b27a61c2289fd3d26330f34 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.201@sha256:478b9038d187e5b5c29bfa8173ded5d29e864b5ad06102a12106380ee01e2e49 AS dotnet-sdk-10.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests"
RUN dotnet publish "OpenTelemetry.Instrumentation.W3cTraceContext.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true

FROM dotnet-sdk-${TEST_SDK_VERSION} AS final
WORKDIR /test
COPY /test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/requirements.txt .
COPY --from=build /drop .
COPY --from=w3c /w3c/trace-context ./trace-context
RUN apt-get update \
  && apt-get install -y --no-install-recommends python3-pip python3-dev \
  && cd /usr/local/bin \
  && ln -s /usr/bin/python3 python \
  && python3 --version \
  && rm -rf /var/lib/apt/lists/*

ENV SPEC_LEVEL=1
ENV STRICT_LEVEL=1

RUN pip3 install --requirement requirements.txt --require-hashes --break-system-packages
ENTRYPOINT ["dotnet", "vstest", "OpenTelemetry.Instrumentation.W3cTraceContext.Tests.dll", "--logger:console;verbosity=detailed"]
