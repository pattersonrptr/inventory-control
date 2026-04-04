# Database Migrations

This directory is created automatically by Entity Framework Core after you run
`dotnet ef migrations add <MigrationName>`.

## How to Generate and Apply Migrations

### Prerequisites
- .NET 10 SDK installed
- Target database available (SQLite for dev, or your chosen provider)
- Connection string configured in `appsettings.json`

### Steps

```bash
# 1. Restore NuGet packages
dotnet restore

# 2. Generate the initial migration
dotnet ef migrations add Initial

# 3. Apply to the database
dotnet ef database update
```

### Switching Database Providers

To use PostgreSQL instead of SQLite, for example:

1. Add the provider package to `.csproj`:
   ```xml
   <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.*" />
   ```

2. In `Program.cs`, replace `UseSqlite` with `UseNpgsql`.

3. Update the connection string in `appsettings.json`:
   ```
   "DefaultConnection": "Host=localhost;Database=estoque;Username=<YOUR_USER>;Password=<YOUR_PASSWORD>"
   ```

4. Regenerate the migrations (old ones are provider-specific).
