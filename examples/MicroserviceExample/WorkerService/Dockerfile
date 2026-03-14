ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.419@sha256:9e9690cacf04e7e37722dfd0c93ad99129df850db79c2947704be0752f3e62ac AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.312@sha256:6416205d95f47e66d4292310710f7d738a4ad8f499c8d2370cf22d29a3c7d4b3 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.201@sha256:478b9038d187e5b5c29bfa8173ded5d29e864b5ad06102a12106380ee01e2e49 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.25@sha256:c5674b4fea451913485b342435baaf560ff8b24175667093b735aa8dc12afeec AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.14@sha256:7e6c1e89b21c6ae239d95993afb108c153246154ff80a0c0c186331ad30b0cd5 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.5@sha256:a04d1c1d2d26119049494057d80ea6cda25bbd8aef7c444a1fc1ef874fd3955b AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
