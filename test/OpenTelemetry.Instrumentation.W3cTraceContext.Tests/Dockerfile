# Create a container for running the OpenTelemetry W3C Trace Context tests https://github.com/w3c/trace-context/tree/master/test.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0

FROM ubuntu:24.04@sha256:66460d557b25769b102175144d538d88219c077c678a49af4afca6fbfc1b5252 AS w3c
#Install git
WORKDIR /w3c
RUN apt-get update && apt-get install -y git
RUN git clone --branch level-1 https://github.com/w3c/trace-context.git

FROM mcr.microsoft.com/dotnet/sdk:8.0.415@sha256:f2f0cb3af991eb6959c8a20551b0152f10cce61354c089dd863a7b72c0f00fea AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.306@sha256:ca77338a19f87a7d24494a3656cb7d878a040c158621337b9cd3ab811c5eb057 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-rc.2@sha256:ed46c853a4f100ba7aab2394e9fb9f9b8989e93346b04632dbce11e5da7623d8 AS dotnet-sdk-10.0

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
