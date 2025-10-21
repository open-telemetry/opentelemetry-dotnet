ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.415@sha256:df1aebc5fd72a1315f34eda24206f195d5ca00ccf2e3009947a74c5a67166cbb AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.306@sha256:a5dd7352c0c058a6f847c95a1147b060e95a532444f14b34d8fa9aaa0a76702f AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-rc.2@sha256:ed46c853a4f100ba7aab2394e9fb9f9b8989e93346b04632dbce11e5da7623d8 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.21@sha256:ebdd28e9ee54ea5032a390500d37bb1b6d45c36c6ba51e10f3ddfcdc746f3e28 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.10@sha256:ad4acf911eada1bcbfd0dd9f3fa514143fba8654ca8844df9032471adc2f9bd9 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0-rc.2@sha256:5b201bc57156a9f70d58a86112dc7a8217cf3a885740a8fe914b0782ee09647e AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
