ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.423@sha256:89ce6291bde9acdf59594e79fb8277c6d84c46e4b1f5bf126a4f18766e4bd597 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.316@sha256:ee0d20fbc3dfc60ae2d9d9115a9b750c18576a47f24073119dd09d93e072cb89 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.302@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.29@sha256:9822a6201c64c9f38b8a190832a7ae21cd30150d509b3ea170fd83b4f4e6166a AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.18@sha256:8b9f90e53d41154d42e537517aef22e8fe497baabd43e423d5f2edafd06f04f6 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.10@sha256:1fa23fc4872d95fd71c2833ebe65d7e84a43b2d51a31d119516852f13d9505a7 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
