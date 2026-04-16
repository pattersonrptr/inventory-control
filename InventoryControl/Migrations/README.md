# Database Migrations

This directory is created automatically by Entity Framework Core after you run
`dotnet ef migrations add <MigrationName>`.

The project uses **PostgreSQL** in production/Docker and SQLite as a fallback for
local development without Docker.

## Applying Migrations

Migrations are applied automatically on startup via `db.Database.Migrate()` in
`Program.cs`. No manual `dotnet ef database update` is needed when running via
`docker-compose up`.

## Generating New Migrations

The provider is selected at runtime based on the connection string. To generate
PostgreSQL-compatible migrations, set the connection string env var before running
`dotnet ef migrations add`:

### Windows (PowerShell)

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=estoque;Username=estoque;Password=estoque"
dotnet ef migrations add <MigrationName> --project InventoryControl/InventoryControl.csproj
```

### Linux / macOS

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=estoque;Username=estoque;Password=estoque" \
  dotnet ef migrations add <MigrationName> --project InventoryControl/InventoryControl.csproj
```

`dotnet ef migrations add` does **not** require a running database; it only needs
the provider to be configured to generate the correct SQL. Use
`dotnet ef database update` (or `docker-compose up`) to apply migrations to an
actual database.

## Local Development Without Docker

For quick local development you can still use SQLite by keeping the default
`appsettings.Development.json` connection string (`Data Source=estoque.db`).
The app will detect the format and use the SQLite provider automatically.

> Note: SQLite migrations must be regenerated separately if needed. Migrations
> in this folder are generated for PostgreSQL.
