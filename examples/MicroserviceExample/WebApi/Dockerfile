ARG SDK_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.424@sha256:bb32ba3ba3ea36e38572d9d8db76fa15f7cbf722f3f886e06bca6d528bd4fba8 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.317@sha256:f190d2dd9eef2899c91ac323caa0bd2b39334a5400ba93013e5199da39dad940 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.400@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.30@sha256:787c228ea85457bec43c8b084e6ac360b26ea43b5c2fcbe861f721f2e8670dd3 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.19@sha256:b1201ee0ccf9a22c06844982296c1be40d5ff9c7685dc002729f204e63fb1730 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.11@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
