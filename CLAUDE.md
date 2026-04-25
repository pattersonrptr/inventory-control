# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

**Build:**
```bash
dotnet build InventoryControl/InventoryControl.csproj
```

**Run locally:**
```bash
dotnet restore InventoryControl/InventoryControl.csproj
dotnet ef database update --project InventoryControl/InventoryControl.csproj
dotnet run --project InventoryControl/InventoryControl.csproj
# Access at https://localhost:5001
```

**Test:**
```bash
dotnet test                        # all tests
dotnet test --filter "FullyQualifiedName~Unit"         # unit only
dotnet test --filter "FullyQualifiedName~Integration"  # integration only
dotnet test --filter "FullyQualifiedName~E2E"          # E2E only (requires Playwright)

# First-time Playwright setup:
pwsh InventoryControl.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

**Docker:**
```bash
docker compose up -d                          # app + PostgreSQL + local backup
docker compose --profile offsite up -d        # + Google Drive offsite backup
docker compose down
```

**EF Core migrations:**
```bash
dotnet ef migrations add <Name> --project InventoryControl/InventoryControl.csproj
dotnet ef database update --project InventoryControl/InventoryControl.csproj
```

## Architecture

ASP.NET Core MVC (.NET 10) with server-rendered Razor views. Database defaults to SQLite in development and PostgreSQL in production; the provider is auto-detected in `Program.cs` based on the connection string prefix (`Host=` or `postgresql://`).

**Layers:**

- **Controllers** (`Controllers/`) — thin; delegate entirely to repositories and services. REST API endpoints live in `Controllers/Api/` and use X-Api-Key header authentication (`Authentication/ApiKeyAuthenticationHandler.cs`).
- **Repositories** (`Repositories/`) — all data access behind `IRepository` interfaces; no direct `DbContext` usage in controllers.
- **Services** (`Services/`) — business logic (CSV import, database backup, offsite backup).
- **Background Services** (`BackgroundServices/`) — `IHostedService` implementations for order sync polling, low-stock email alerts, and audit log cleanup.
- **Integration Layer** (`Integrations/`) — plugin architecture for e-commerce platforms:
  - `IStoreIntegration` is the platform contract (products, stock, orders, categories).
  - `IPlatformFactory` creates integration instances per platform type.
  - `PlatformRegistry` is the service locator that discovers factories and resolves stores at runtime.
  - `SyncService` pulls products/orders from platforms, pushes stock, processes orders.
  - Currently implements **Nuvemshop**. See `docs/adding-a-platform.md` to add new platforms.
- **Data** (`Data/`) — `AppDbContext` + `AuditInterceptor` (auto-logs all entity creates/updates/deletes via `SaveChanges` hook).

**Multi-store support:** `Stores[]` array in `appsettings.json`. `ExternalMappings` tables link internal entities to multiple external stores. Background service syncs all stores in parallel.

**Key patterns:** Repository, Plugin/Registry (platform factories), Interceptor (audit trail), Factory (sync service), Background Job.

## Conventions

- All code, comments, and commits in **English**.
- **Commits**: Conventional Commits format (`feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`, `ci:`, `perf:`).
- **Versioning**: SemVer; update `CHANGELOG.md` before every release commit.
- **Naming**: PascalCase for public members, camelCase for locals; use `var` when type is obvious.
- **LINQ**: Method syntax preferred over query syntax.
- **Views**: Razor with Tag Helpers only — never `@Html.Raw`.
- **Migrations**: Always use EF Core LINQ; never raw SQL. Quote all identifiers with `""` in any raw SQL (PostgreSQL lowercases unquoted names).
- **Secrets**: `appsettings.json` is gitignored; use `dotnet user-secrets` locally. Copy `appsettings.example.json` to get started.
- **Tests**: xUnit, named `MethodName_Scenario_ExpectedResult`. Moq for mocking, `Microsoft.AspNetCore.Mvc.Testing` for integration tests.

## Configuration Keys

`appsettings.json` (copy from `appsettings.example.json`):

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:DefaultConnection` | SQLite path or PostgreSQL connection string |
| `Stores[]` | Array of store configs: Name, Platform, StoreId, AccessToken, OrderSyncIntervalMinutes |
| `DefaultAdmin` | Seed admin account on first run |
| `AuditLog:RetentionDays` | Audit log retention (default 90) |
| `EmailNotifications` | SMTP config for low-stock alerts |
| `Api:Key` | REST API authentication token |
| `OffsiteBackup` | rclone config for Google Drive backups |

## Notable Behaviors

- **Migrations run automatically on startup** for relational providers.
- **Health check** at `/health` — used by Docker/TrueNAS liveness probes.
- **Swagger UI** at `/swagger`.
- **Audit trail** is automatic — every entity change is logged with user, timestamp, and old/new values via `AuditInterceptor`.
- Legacy single-store `Integration` config section is auto-migrated to the `Stores[]` format on startup.
