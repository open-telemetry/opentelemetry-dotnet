ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.416@sha256:8f710c2f7f3e60392fba1d408dcd9c30c26295bd4ded326cb7cf6cc6fd66cfde AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.308@sha256:6c7713b7d107f743015b863f6bb028fca7430192140536779c0464f1ece899bc AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.101@sha256:d1823fecac3689a2eb959e02ee3bfe1c2142392808240039097ad70644566190 AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.22@sha256:86cbca9fc08f712273edef23b0dff38d81fe0d20486662946f98b164c3af6d33 AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.11@sha256:5df8845a131ee13af686c2096ac2d8b1e91b537288cb5a63c5325b2df4f7d184 AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.1@sha256:eaa79205c3ade4792a7f7bf310a3aac51fe0e1d91c44e40f70b7c6423d475fe0 AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
