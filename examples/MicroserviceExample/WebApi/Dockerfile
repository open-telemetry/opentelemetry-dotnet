ARG SDK_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:8.0.416@sha256:2c7794fef0107c1815193dc157ed3f8ca60eb504a439806a29b6f9bf1ce73679 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.308@sha256:51060ced0e5dc1282a587a092783132ba8b0ec25150d3bc61e99bf0ef2d62593 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.101@sha256:d1823fecac3689a2eb959e02ee3bfe1c2142392808240039097ad70644566190 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WebApi -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.22@sha256:39b1272ca9a951df21a72df91cba0344a1a01a9e85ae61c90cb4db059ac62388 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.11@sha256:f872f90df526f0282401c9be23a0b863ecd48fd452e4b413e74a5c321d2627b5 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.1@sha256:eaa79205c3ade4792a7f7bf310a3aac51fe0e1d91c44e40f70b7c6423d475fe0 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WebApi.dll"]
