# Create a container for running the OpenTelemetry W3C Trace Context tests https://github.com/w3c/trace-context/tree/master/test.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0

FROM ubuntu:24.04@sha256:cd1dba651b3080c3686ecf4e3c4220f026b521fb76978881737d24f200828b2b AS w3c
#Install git
WORKDIR /w3c
RUN apt-get update && apt-get install -y git
RUN git clone --branch level-1 https://github.com/w3c/trace-context.git

FROM mcr.microsoft.com/dotnet/sdk:8.0.418@sha256:58359d0b8fe8237be1d63ac335ca378e2857f976c7f92791f7c84b3c117037f5 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.311@sha256:03a7d87512a743b3d53416adae79eb89d2a7163de78bb58e7bc7b7e262e25750 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.103@sha256:0a506ab0c8aa077361af42f82569d364ab1b8741e967955d883e3f23683d473a AS dotnet-sdk-10.0

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
COPY --from=w3c /w3c .
RUN apt-get update \
  && apt-get install -y python3-pip python3-dev \
  && cd /usr/local/bin \
  && ln -s /usr/bin/python3 python

RUN pip3 install --requirement requirements.txt --require-hashes --break-system-packages
ENTRYPOINT ["dotnet", "vstest", "OpenTelemetry.Instrumentation.W3cTraceContext.Tests.dll", "--logger:console;verbosity=detailed"]
