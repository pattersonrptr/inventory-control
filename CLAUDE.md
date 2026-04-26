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

ASP.NET Core MVC (.NET 10) with server-rendered Razor views. Modular Monolith with Feature Folders. Database defaults to SQLite in development and PostgreSQL in production; the provider is auto-detected in `Program.cs` based on the connection string prefix (`Host=` or `postgresql://`).

**Top-level structure:**

```
Domain/           — Core entities and repository interfaces. No EF Core dependency.
  Products/       — Product (rich entity: ApplyEntry/ApplyExit/Margin), ProductImage, ProductExternalMapping, IProductRepository, InsufficientStockException
    Events/       — StockChanged, ProductWentBelowMinimum (raised by Product)
  Catalog/        — Category, CategoryExternalMapping, Supplier, ICategoryRepository, ISupplierRepository
  Stock/          — StockMovement, MovementType, ExitReason, IStockMovementRepository
  Orders/         — ProcessedOrder, IProcessedOrderRepository
  Integrations/   — SyncState
  Audit/          — AuditLog
  Identity/       — ApplicationUser
  Shared/         — PagedResult<T>, IDomainEvent, IHasDomainEvents

Features/         — Vertical slices; each owns its controllers, DTOs, validators, and event handlers.
  Products/       — ProductsController, ProductsApiController, ImportController, CsvImportService, DTOs
  Categories/     — CategoriesController, CategoriesApiController, CategoryDto
  Suppliers/      — SuppliersController, SuppliersApiController, SupplierDto
  Stock/          — StockMovementsController
  Sync/           — SyncController, StoresController, Handlers/PushStockOnStockChange
  Notifications/  — Handlers/EmailOnProductWentBelowMinimum
  Reports/        — ReportsController
  Backup/         — BackupController
  Logs/           — LogsController, AuditLogsController
  Account/        — AccountController
  Home/           — HomeController

Infrastructure/   — Technical implementations. References Domain interfaces only.
  Persistence/    — AppDbContext (drains domain events on SaveChangesAsync), AuditInterceptor, Repositories/
  Events/         — IDomainEventDispatcher, IDomainEventHandler<T>, DomainEventDispatcher
  Email/          — IEmailSender, SmtpEmailSender
  Integrations/   — Abstractions (IStoreIntegration, IPlatformFactory), Nuvemshop adapter, PlatformRegistry, SyncService
  Auth/           — ApiKeyAuthenticationHandler
  BackgroundJobs/ — OrderSyncBackgroundService, LowStockNotificationService, AuditLogCleanupService
  Backup/         — DatabaseBackupService, OffsiteBackupService, IDatabaseBackupService, IOffsiteBackupService
  IClock.cs + SystemClock.cs — testable time abstraction

Web/              — (placeholder for future DI extensions)
Validators/       — FluentValidation validators for API DTOs
```

**Multi-store support:** `Stores[]` array in `appsettings.json`. `ExternalMappings` tables link internal entities to multiple external stores. Background service syncs all stores in parallel.

**Key patterns:** Repository (Domain interfaces / Infrastructure implementations), Plugin/Registry (platform factories), Interceptor (audit trail), Feature Folder (vertical slices), Background Job, **Domain Events** (entities raise events via `IHasDomainEvents`; `AppDbContext.SaveChangesAsync` drains and dispatches via `IDomainEventDispatcher` after commit; handlers live in their own feature slice).

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
| `Api:Keys` | REST API keys array: `[{ "Key": "...", "Role": "Admin\|ReadOnly" }]` |
| `OffsiteBackup` | rclone config for Google Drive backups |

## Notable Behaviors

- **Migrations do NOT run automatically on startup.** Run `dotnet run -- migrate` (or the `db-migrate` docker-compose service) before the first start and after any upgrade with new migrations.
- **Health checks:** `/health/live` (liveness — anonymous, process-only), `/health/ready` (readiness — includes DbContext check).
- **Swagger UI** at `/swagger` (Development only).
- **Audit trail** is automatic — every entity change is logged with user, timestamp, and old/new values via `AuditInterceptor`.
- Legacy single-store `Integration` config section is auto-migrated to the `Stores[]` format on startup.
- `Api:Key` (single legacy key) was removed in v7.0.0. Use `Api:Keys` array only.
