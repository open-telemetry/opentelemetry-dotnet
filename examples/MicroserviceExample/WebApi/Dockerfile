ARG SDK_VERSION=9.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.415@sha256:df1aebc5fd72a1315f34eda24206f195d5ca00ccf2e3009947a74c5a67166cbb AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.306@sha256:a5dd7352c0c058a6f847c95a1147b060e95a532444f14b34d8fa9aaa0a76702f AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-rc.1@sha256:eee11b0bf11715710bbe8339b9641f0ef8b5d8a8e07f2d6ff3cd4361c1a4e5a7 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.21@sha256:ebdd28e9ee54ea5032a390500d37bb1b6d45c36c6ba51e10f3ddfcdc746f3e28 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.10@sha256:ad4acf911eada1bcbfd0dd9f3fa514143fba8654ca8844df9032471adc2f9bd9 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0-rc.1@sha256:d9a0d4006dbbc14d877b00c5e7113b432bac6bca6d12816a6e8fd999bac72797 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
