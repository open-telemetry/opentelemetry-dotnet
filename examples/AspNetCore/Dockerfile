FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

WORKDIR /app
COPY . ./
RUN dotnet publish ./examples/AspNetCore -c Release -o /out -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "Examples.AspNetCore.dll"]
