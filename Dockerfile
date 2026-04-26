# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY InventoryControl.sln .
COPY InventoryControl/InventoryControl.csproj InventoryControl/

# Restore dependencies (cached unless .csproj changes)
RUN dotnet restore InventoryControl/InventoryControl.csproj

# Copy the rest of the source code
COPY InventoryControl/ InventoryControl/

# Publish the application
RUN dotnet publish InventoryControl/InventoryControl.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser -d /app appuser

# Install curl (healthcheck), postgresql-client (pg_dump), and rclone (offsite backup)
RUN apt-get update && apt-get install -y --no-install-recommends curl postgresql-client rclone && rm -rf /var/lib/apt/lists/*

# Copy published output
COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app

USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

LABEL org.opencontainers.image.title="inventory-control" \
      org.opencontainers.image.description="ASP.NET Core inventory management system with Nuvemshop integration" \
      org.opencontainers.image.source="https://github.com/pattersonrptr/inventory-control" \
      org.opencontainers.image.licenses="MIT"

ENTRYPOINT ["dotnet", "InventoryControl.dll"]
