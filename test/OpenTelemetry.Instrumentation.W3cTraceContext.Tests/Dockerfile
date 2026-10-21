# Create a container for running the OpenTelemetry W3C Trace Context tests https://github.com/w3c/trace-context/tree/main/test.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.426-azurelinux3.0@sha256:88659ec9a57b3e15d2a0fce769d8cd7071e5ee470b4dde0f301417e2d81d4238 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.319-azurelinux3.0@sha256:18f5f0f1c8a81689880383c95816da71d74f4a66757ba85ed9e79cb804fdbb53 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.402-azurelinux3.0@sha256:5798415b0662aa6443c87d5dfcddb4e7a189431310365442a0246ade7707893d AS dotnet-sdk-10.0

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

ENTRYPOINT ["dotnet", "OpenTelemetry.Instrumentation.W3cTraceContext.Tests.dll"]
