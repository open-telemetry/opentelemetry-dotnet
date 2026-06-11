ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.422@sha256:ff435ddea88d0d2812a4b8d3b4ab833a8e75256aea5c444a841a84847ba8c8a4 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.315@sha256:06da3f01ec505b4f365497e1952c7ffcb748e536ac60da463ae6766814cfd9c6 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.301@sha256:548d93f8a18a1acbe6cc127bc4f47281430d34a9e35c18afa80a8d6741c2adc3 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.28@sha256:73ea85f9dad66f0a4560f06e3d1d179f627630814988e38dd3639831022b932d AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.17@sha256:01e1395406727f16de474ea093fb90c9493077f33428a079d16c4fc5b5433389 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.9@sha256:ddcf70ad1ab963a4fcd41fbd722a6b660e404e87567cfbd46fd2809c21b02088 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
