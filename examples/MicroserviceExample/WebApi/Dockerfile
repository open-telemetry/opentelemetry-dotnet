ARG SDK_VERSION=11.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.301@sha256:548d93f8a18a1acbe6cc127bc4f47281430d34a9e35c18afa80a8d6741c2adc3 AS dotnet-sdk-10.0
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.5@sha256:322b57a006b21737287a3680fa860562d9a9fe3373e1d670fc4d5b8ad519de44 AS dotnet-sdk-11.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net11.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.9@sha256:ddcf70ad1ab963a4fcd41fbd722a6b660e404e87567cfbd46fd2809c21b02088 AS dotnet-aspnet-10.0
FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-preview.5@sha256:9fbcd84b456367cb86db9fc4f34aded6c8c68e803b1d11f37bd3619782e4762b AS dotnet-aspnet-11.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
