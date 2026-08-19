ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.424@sha256:306301580fcaa5b445180e759db59309979002d1000669cb4cf58a567d0014bc AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.317@sha256:35048e3a81e6a07c316e7bbbd80d80d2ba705fe5f23a8ed42b6638c8f4c20d30 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.400@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.30@sha256:b0beb9cc1dee1c1b0749796110d4734292071b814207ad0d4f40611f7db04f7b AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.19@sha256:4e376dd15bbc8437d4892367ab0ea06a3ac9fea482d10f92f3c493fe1a2219ad AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.11@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
