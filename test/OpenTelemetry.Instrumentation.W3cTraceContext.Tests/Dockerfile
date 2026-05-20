# Create a container for running the OpenTelemetry W3C Trace Context tests https://github.com/w3c/trace-context/tree/master/test.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.421-azurelinux3.0@sha256:bdde9ef66129ae834f7a2ef33c819a033ddee0fd621b91a82e5f32ffad5c5c38 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.314-azurelinux3.0@sha256:403607a1996f8c4009cbf536793f5b061ff12736028a6f95511155cf221b4ec1 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.300-azurelinux3.0@sha256:28e3d18f3e172d554d7002ee1af97c0a24b31b26ae3b5dd8d2e40a9a6c5760b1 AS dotnet-sdk-10.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0

# renovate: datasource=github-digest depName=w3c/trace-context packageName=w3c/trace-context
ENV W3C_TRACE_CONTEXT_SHA="34b10ac5af7f0caeb28efe35fe51cd4763ec5771"

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
ENV STRICT_LEVEL=1

ENTRYPOINT ["dotnet", "vstest", "OpenTelemetry.Instrumentation.W3cTraceContext.Tests.dll", "--logger:console;verbosity=detailed"]
