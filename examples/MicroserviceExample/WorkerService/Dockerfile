ARG SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.426@sha256:f7e28a0a3f6f9235929ec19274fd0ddf8534e9159f99fac602ac73304f1ce99f AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.319@sha256:2f5eb32870b4c0074a3d5b5191a03553bcab7b74463a76a7c0a5f3ef65c90fe5 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.402@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS dotnet-sdk-10.0

FROM dotnet-sdk-${SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/MicroserviceExample/WorkerService -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0.32@sha256:1601380775673021a23ea5985669f1daf03719faaa684f839da1752fb48cfc7f AS dotnet-aspnet-8.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0.21@sha256:4d94cf3204df98d321e8dbf8d6d06e0b34fd136aa75c945abf4390ee07eb30dd AS dotnet-aspnet-9.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac AS dotnet-aspnet-10.0

FROM dotnet-aspnet-${SDK_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "WorkerService.dll"]
