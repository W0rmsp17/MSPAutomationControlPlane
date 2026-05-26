FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/TenantHealthCheck/TenantHealthCheck.csproj src/TenantHealthCheck/
RUN dotnet restore src/TenantHealthCheck/TenantHealthCheck.csproj

COPY src/TenantHealthCheck/ src/TenantHealthCheck/
RUN dotnet publish src/TenantHealthCheck/TenantHealthCheck.csproj \
    --configuration Release \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TenantHealthCheck.dll"]
