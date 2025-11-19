ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.416@sha256:874c4613d5ebf8b328ad920a90640c8dea9758bdbe61dc191dbcbed03721fc79 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.307@sha256:cdb198f8fc1dfd714c798f609582dedfe397c03318b820eecbefdea06934da14 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.100@sha256:c7445f141c04f1a6b454181bd098dcfa606c61ba0bd213d0a702489e5bd4cd71 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.22@sha256:47091f7cee02e448630df85542579e09b7bbe3b10bd4e1991ff59d3adbddd720 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.11@sha256:925a86ec775fe737f1d65371ad7b2e01e81390c3c327ce3c06866a8ce8a4e1a2 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0@sha256:7c4246c1c384319346d45b3e24a10a21d5b6fc9b36a04790e1588148ff8055b0 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
