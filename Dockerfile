# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (layer-cached)
COPY src/Datide.Replenishment.Domain/Datide.Replenishment.Domain.csproj src/Datide.Replenishment.Domain/
COPY src/Datide.Replenishment.Application/Datide.Replenishment.Application.csproj src/Datide.Replenishment.Application/
COPY src/Datide.Replenishment.Infrastructure/Datide.Replenishment.Infrastructure.csproj src/Datide.Replenishment.Infrastructure/
COPY src/Datide.Replenishment.Web/Datide.Replenishment.Web.csproj src/Datide.Replenishment.Web/
RUN dotnet restore src/Datide.Replenishment.Web/Datide.Replenishment.Web.csproj

# Build + publish
COPY . .
RUN dotnet publish src/Datide.Replenishment.Web/Datide.Replenishment.Web.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0
EXPOSE 8080

ENTRYPOINT ["dotnet", "Datide.Replenishment.Web.dll"]
