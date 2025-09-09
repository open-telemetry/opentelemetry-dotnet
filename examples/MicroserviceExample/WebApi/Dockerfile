ARG SDK_VERSION=9.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.413@sha256:7fd287cec03e027e39be13fb06ca1347f1338c7a6ab4ad305d94d91852693f70 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.304@sha256:ae000be75dac94fc40e00f0eee903289e985995cc06dac3937469254ce5b60b6 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-rc.1@sha256:32cd7d97e0814307e594f9f3e33aa039ddcf125191fd7add32c7370d2a6118b9 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.19@sha256:6d40ae71165cb8a4427477ae3b447b05c080330cabae08b0dae672552bcd34d8 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.8@sha256:515c08e27cf8d933af442f0e1b6c92cc728acb71f113ca529f56c79cb597adb5 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0-rc.1@sha256:68088c4403831e452c826a751484ac5d1aaa495eddccea5068b8bf5b6dcd515c AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
