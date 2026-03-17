# Create a container for running the OpenTelemetry W3C Trace Context tests https://github.com/w3c/trace-context/tree/master/test.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=11.0
ARG TEST_SDK_VERSION=11.0

FROM ubuntu:24.04@sha256:d1e2e92c075e5ca139d51a140fff46f84315c0fdce203eab2807c7e495eff4f9 AS w3c

# HACK Workaround the python tests not working with Python 3.14
# See https://github.com/w3c/trace-context/issues/581
# renovate: datasource=github-digest depName=w3c/trace-context packageName=w3c/trace-context
ENV W3C_TRACE_CONTEXT_SHA="e37efba66396bff25b737b33bce3c117cc6eb287"

# Install git
WORKDIR /w3c
RUN apt-get update \
  && apt-get install -y --no-install-recommends ca-certificates git \
  && rm -rf /var/lib/apt/lists/*
RUN git init trace-context \
  && cd trace-context \
  && git remote add origin https://github.com/martincostello/trace-context.git \
  && git fetch --depth 1 origin "${W3C_TRACE_CONTEXT_SHA}" \
  && git checkout FETCH_HEAD \
  && rm -rf .git

FROM mcr.microsoft.com/dotnet/sdk:10.0.201@sha256:478b9038d187e5b5c29bfa8173ded5d29e864b5ad06102a12106380ee01e2e49 AS dotnet-sdk-10.0
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.2@sha256:98c96a6331e1ab4eb73abe6ba341807d686d2ab24a915c617497a8b5995eefe9 AS dotnet-sdk-11.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net11.0
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
