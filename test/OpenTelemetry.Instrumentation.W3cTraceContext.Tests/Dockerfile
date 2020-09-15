# Create a container for running the OpenTelemetry W3C Trace Context tests https://github.com/w3c/trace-context/tree/master/test.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/Dockerfile .

ARG SDK_VERSION=3.1
FROM ubuntu AS w3c
#Install git
WORKDIR /w3c
RUN apt-get update && apt-get install -y git
RUN git clone https://github.com/w3c/trace-context.git

FROM mcr.microsoft.com/dotnet/core/sdk:${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=netcoreapp3.1
RUN if [ "$PUBLISH_FRAMEWORK" = "netcoreapp2.1" ] ; then wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod g+rx,o+rx dotnet-install.sh \
    && ./dotnet-install.sh -c 3.1 --install-dir /usr/share/dotnet \
    && ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet ; fi
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests"
RUN dotnet publish "OpenTelemetry.Instrumentation.W3cTraceContext.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true

ARG SDK_VERSION=3.1
FROM mcr.microsoft.com/dotnet/core/sdk:${SDK_VERSION} AS final
WORKDIR /test
COPY --from=build /drop .
COPY --from=w3c /w3c .
RUN apt-get update \
  && apt-get install -y python3-pip python3-dev \
  && cd /usr/local/bin \
  && ln -s /usr/bin/python3 python \
  && pip3 install --upgrade pip \
  && pip3 install aiohttp
ENTRYPOINT ["dotnet", "test", "OpenTelemetry.Instrumentation.W3cTraceContext.Tests.dll"]
