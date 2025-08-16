ARG SDK_VERSION=9.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.413@sha256:7fd287cec03e027e39be13fb06ca1347f1338c7a6ab4ad305d94d91852693f70 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.304@sha256:ae000be75dac94fc40e00f0eee903289e985995cc06dac3937469254ce5b60b6 AS dotnet-sdk-9.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net9.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.19@sha256:6d8901fae2d8f44c0e73962046a67fbdaf017bc8a6833c74ae87e9fbd0810b73 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.8@sha256:2b70ef1451cb01c2118969ad15fd0ae40fdbac158f09699fefae0dc32c4ca352 AS dotnet-aspnet-9.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
