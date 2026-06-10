ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.422@sha256:c0761ac934d599d5be35e5d237aba6b08f47032a492c765e5ffa8d4bfc851e79 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.315@sha256:ff12d25929cad887fb9c83e0be843f3cb56ef961fb90623686cd018a988c3675 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.301@sha256:548d93f8a18a1acbe6cc127bc4f47281430d34a9e35c18afa80a8d6741c2adc3 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.28@sha256:0d39437fd3b598b2fd91f4d27118948b6ca1694ab90f90fef256504f822528fc AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.17@sha256:02cf9495cca48f5c39d2984c8f4a4b346edbe9fc4697b1101a02ffdc4e37a07e AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.9@sha256:ddcf70ad1ab963a4fcd41fbd722a6b660e404e87567cfbd46fd2809c21b02088 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
