# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY ControleEstoque.sln .
COPY ControleEstoque/ControleEstoque.csproj ControleEstoque/

# Restore dependencies (cached unless .csproj changes)
RUN dotnet restore ControleEstoque/ControleEstoque.csproj

# Copy the rest of the source code
COPY ControleEstoque/ ControleEstoque/

# Publish the application
RUN dotnet publish ControleEstoque/ControleEstoque.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser -d /app appuser

# Install curl for Docker healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Copy published output
COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app

USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ControleEstoque.dll"]
