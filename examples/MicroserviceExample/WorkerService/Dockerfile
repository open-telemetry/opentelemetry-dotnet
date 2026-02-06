ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.417@sha256:1682f3cd0cb51464568de4fa88c719bd3be55539653d97540718bdd5b87062d4 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.310@sha256:a574e6212c98ccf9cf2c6588201b1c71d93094f6f26c21375f75aa909a9e88f0 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.102@sha256:6ba533cc61a5d8c5e7d4b3a3e33e2ddc2efef200b112e4d658303516bfd24255 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.23@sha256:0d4a76c0a692c7419acf59529ba6dae8552f4fd59e8453538b5291710a29fb9d AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.12@sha256:cd8732b16dbb84d3eae80753a892f9f7cf4aafe922bf466d2f62f036be558699 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.2@sha256:7feda9a96737a8c268cc7636c92d0efda3512ef019e3c409dd97c7c9ae9e2bdb AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
