ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.423@sha256:3c0edbfe1549dd93fb789dc96299a40df865ad7bffefcaf38e8c05940686d641 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.316@sha256:cb9d975bf57fd1b0915858d1db1184bea20f7f746f0536323fcab49673144e8c AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.302@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.29@sha256:8f6a307ae32fb393a4b4bcde0a81f0ce3f0a715de0e5575df71f3030448f2dde AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.18@sha256:86085bc68dde4a8cdfd8c2342acc3bf843eade855092879a85e3f3adb56e55c7 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.10@sha256:1fa23fc4872d95fd71c2833ebe65d7e84a43b2d51a31d119516852f13d9505a7 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
