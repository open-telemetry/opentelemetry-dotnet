ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.413@sha256:45e41fe52eb60f42bd75c83b7e8bfff0523e031e042b4c1fc7ddb9c348898c64 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.304@sha256:840f3b62b9742dde4461a3c31e38ffd34d41d7d33afd39c378cfcfd5dcb82bd5 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-rc.1@sha256:32cd7d97e0814307e594f9f3e33aa039ddcf125191fd7add32c7370d2a6118b9 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.19@sha256:6d8901fae2d8f44c0e73962046a67fbdaf017bc8a6833c74ae87e9fbd0810b73 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.8@sha256:2b70ef1451cb01c2118969ad15fd0ae40fdbac158f09699fefae0dc32c4ca352 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0-rc.1@sha256:68088c4403831e452c826a751484ac5d1aaa495eddccea5068b8bf5b6dcd515c AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
