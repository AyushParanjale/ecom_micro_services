# syntax=docker/dockerfile:1
# Build from the solution root so ECom.Shared and Directory.Build.* are available:
#   docker build -f src/ECom.Auth.Api/Dockerfile -t ecom.auth.api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props Directory.Build.targets ./
COPY src/ECom.Shared/ECom.Shared.csproj src/ECom.Shared/
COPY src/ECom.Auth.Api/ECom.Auth.Api.csproj src/ECom.Auth.Api/
RUN dotnet restore src/ECom.Auth.Api/ECom.Auth.Api.csproj

COPY src/ECom.Shared/ src/ECom.Shared/
COPY src/ECom.Auth.Api/ src/ECom.Auth.Api/
RUN dotnet publish src/ECom.Auth.Api/ECom.Auth.Api.csproj \
    --configuration Release --no-restore --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
RUN mkdir -p /app/logs && chown "$APP_UID" /app/logs
USER $APP_UID
ENTRYPOINT ["dotnet", "ECom.Auth.Api.dll"]
