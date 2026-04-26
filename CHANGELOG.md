# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [7.4.0] - 2026-04-26

### Added

- **Domain events infrastructure**:
  - `IDomainEvent` marker interface and `IHasDomainEvents` interface in `Domain/Shared/`.
  - `IDomainEventDispatcher` + reflection-based `DomainEventDispatcher` resolves handlers via DI; handler exceptions are logged but never propagate.
  - `AppDbContext.SaveChangesAsync` collects events from tracked entities, commits, then dispatches — handler failures cannot roll back the data change.
- **Product domain events**:
  - `StockChanged(ProductId, NewStock)` raised on every `ApplyEntry`/`ApplyExit`.
  - `ProductWentBelowMinimum(ProductId, ProductName, CurrentStock, MinimumStock)` raised only on the threshold crossing (above → below); does not re-raise while the product is already below minimum.
- **Event handlers**:
  - `Features/Sync/Handlers/PushStockOnStockChange` — replaces the controller-side stock push.
  - `Features/Notifications/Handlers/EmailOnProductWentBelowMinimum` — immediate per-product alert.
- **`IEmailSender` / `SmtpEmailSender`** in `Infrastructure/Email/` — extracted from `LowStockNotificationService` for reuse.

### Changed

- **`StockMovementsController` no longer depends on `SyncService`** and no longer manages a manual transaction. A single `SaveChangesAsync` atomically persists the movement plus the product update and triggers the `StockChanged` event.
- `LowStockNotificationService` (periodic batch summary) now delegates SMTP send to `IEmailSender`; it coexists with the immediate event-driven alert.

## [7.3.0] - 2026-04-26

### Fixed

- **Admin seed password**: default `Admin123!` (8 chars) failed silently against the `RequiredLength=10` policy introduced in v6.2.0. Changed to `Admin1234!@` in both `Program.cs` fallback and `appsettings.example.json`. Seed failures are now logged as errors instead of silently discarded.

### Added

- **`.env.example`**: documents `DB_PASSWORD`, `RCLONE_REMOTE`, and `SYNC_INTERVAL` environment variables for Docker deployments.
- **CI: vulnerability scan job** (`dotnet list package --vulnerable --include-transitive`): fails the build if any known-vulnerable NuGet package is detected; uploads a report artifact.
- **Dockerfile: OCI image labels** (`title`, `description`, `source`, `licenses`).

### Changed

- **docker-compose**: app `start_period` increased from 40s to 60s to avoid spurious unhealthy marks on slow startup; `db` and `app` services now have a 512 MB memory limit.

## [7.2.0] - 2026-04-26

### Added

- **33 integration tests** for `ProductsApiController`, `CategoriesApiController`, and `SuppliersApiController` — full CRUD coverage including 201/404/400 paths, pagination, below-minimum endpoint, stock patch, and Margin in response.
- **`Product.Margin` exposed in REST API response** (`GET /api/v1/products` and `GET /api/v1/products/{id}`).

### Changed

- Total non-E2E test count: 189 → 222.

## [7.1.0] - 2026-04-26

### Added

- **`Product.ApplyEntry(int qty)`**: domain method for stock entries — guards positive quantity, increments `CurrentStock`.
- **`Product.ApplyExit(int qty)`**: domain method for stock exits — guards positive quantity and sufficient stock; throws `InsufficientStockException` with `ProductName`, `Available`, and `Requested` details.
- **`Product.Margin`**: computed gross margin percentage `(SellingPrice - CostPrice) / SellingPrice * 100`.
- **`InsufficientStockException`**: domain exception replacing the in-controller stock check.
- **Unit tests** for all new domain behaviors (11 tests in `Unit/Domain/ProductDomainTests`).

### Changed

- `StockMovementsController`: Entry and Exit POST actions now delegate stock arithmetic to domain methods instead of computing inline. The insufficient-stock validation is handled by `InsufficientStockException` from the domain.

## [7.0.0] - 2026-04-26

### Breaking

- **Deploy:** Migrations no longer run on app startup (first introduced 6.3.0; now strictly required). Run `dotnet InventoryControl.dll migrate` or the `db-migrate` docker-compose service before starting the app.
- **Config:** Legacy `Api:Key` removed. Migrate to the `Api:Keys` array format (see `appsettings.example.json`).
- **Internal structure:** Project reorganized into Modular Monolith with Feature Folders. Source directory layout changed: `Domain/`, `Features/`, `Infrastructure/`, `Web/`.

### Added

- **Architecture tests** (`NetArchTest.Rules`): `Domain_HasNoDependencyOn_EntityFrameworkCore` and `Infrastructure_MustNotDependOn_Features` are active and green. `Features_HaveNoDependencyOn_InfrastructurePersistence` is skipped pending service extraction (sub-phase 5.6 debt, documented in test).
- **`GlobalUsings.cs`** in main and test projects: all type moves are backward-compatible at compile time without touching every file.

### Changed

- Source organized by feature/responsibility, not by technical layer:
  - `Domain/<aggregate>/` — entities + repository interfaces (no EF Core dependency)
  - `Features/<slice>/` — controllers, DTOs, validators per vertical
  - `Infrastructure/` — persistence, integrations, auth, background jobs, backup
- EF Core context and interceptor moved: `Data/` → `Infrastructure/Persistence/`
- Repositories moved: `Repositories/` → `Infrastructure/Persistence/Repositories/`
- Repository interfaces moved: `Repositories/Interfaces/` → `Domain/<aggregate>/`
- Authentication moved: `Authentication/` → `Infrastructure/Auth/`
- Background services moved: `BackgroundServices/` → `Infrastructure/BackgroundJobs/`
- Integrations moved: `Integrations/` → `Infrastructure/Integrations/`
- Backup services moved: `Services/` → `Infrastructure/Backup/` + `Features/Products/`
- All controllers moved: `Controllers/` → `Features/<slice>/`
- `IClock` / `SystemClock` moved: `Services/` → `Infrastructure/`
- `CLAUDE.md` architecture section updated to reflect new structure.

## [6.4.0] - 2026-04-25

### Performance

- **Migration `AddPerformanceIndexes`:** composite index on `Products(CurrentStock, MinimumStock)` (covers the low-stock query); index on `StockMovements(Date)` (covers date-range and month/year queries).
- **`GetAllForListAsync(page, pageSize)`** added to `IProductRepository` and `ICategoryRepository` — loads only the navigation property needed for display (`Category` / `Parent`), skipping heavy collections (`Images`, `ExternalMappings`, `Products`). Used by all list views and list API endpoints.
- **`AsSplitQuery()`** added to the full `GetAllAsync(page, pageSize)` in `ProductRepository` and `CategoryRepository` — prevents Cartesian explosion when multiple collection-type includes are combined with pagination.
- Count query in paginataed methods now runs against the base query (no includes) — EF Core no longer generates a COUNT with unnecessary JOINs.

### Changed

- `ProductsController.Index`, `CategoriesController.Index`, `ProductsApiController.GetAll`, `CategoriesApiController.GetAll` — switched to `GetAllForListAsync` (lightweight query).

## [6.3.0] - 2026-04-25

### Changed

- **BREAKING (deploy):** Migrations no longer run on app startup. Run `dotnet InventoryControl.dll migrate` (or the `db-migrate` docker-compose service) before starting the app for the first time or after any upgrade that includes new migrations.
- **Health check endpoints split:** `/health/live` (liveness — anonymous, process-only) and `/health/ready` (readiness — includes DbContext check). Both are anonymous. Docker/TrueNAS healthchecks updated to use `/health/live`.
- **Background service exponential backoff:** `OrderSyncBackgroundService` now doubles the delay after each consecutive sync cycle failure, capped at 30 minutes.

### Fixed

- `MailMessage` is now properly disposed via `using` in `LowStockNotificationService.SendEmailAsync`, preventing resource leaks on SMTP failures.
- `PGPASSWORD` environment variable cleared from `ProcessStartInfo` immediately after `pg_dump` starts.

### Security

- Startup logs a warning when the rclone config file is world-readable on Linux deployments.

## [6.2.0] - 2026-04-25

### Added

- **FluentValidation** (`FluentValidation.AspNetCore` 11.3.1) — auto-validation wired via `AddFluentValidationAutoValidation`; validators discovered by assembly scan.
- **API DTO validators** — `ProductCreateDtoValidator`, `ProductUpdateDtoValidator`, `StockUpdateDtoValidator`, `CategoryDtoValidator`, `SupplierDtoValidator` enforce required fields, max lengths, non-negative numerics, valid email, and positive `CategoryId`.
- **`IClock` / `SystemClock`** — testable time abstraction registered as a singleton; replaces all `DateTime.Now` usages in services and background jobs.
- **`ImageUploadValidator`** — centralised image validation (allowed extensions: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`; max 10 MB); returns structured error list.

### Fixed

- **Silent image extension rename** — `ProductsController.SaveImagesAsync` no longer silently renames files with unsupported extensions to `.jpg`; invalid types and oversized files now surface as `ModelState` errors.
- **`ex.Message` information leakage** — `BackupController` catch blocks now return a generic Portuguese error message; full exception details remain in the structured log only.
- **`DateTime.Now` in UTC contexts** — replaced with `DateTime.UtcNow` in `AuditInterceptor`, `AuditLog`, `ProcessedOrder`, `SyncService`, `BackupController`, `AuditLogCleanupService`, and `LowStockNotificationService`.

### Security

- **Password policy hardened** — `RequireDigit`, `RequireUppercase`, `RequireNonAlphanumeric` set to `true`; minimum length raised from 6 to 10.
- **Global exception handler for API routes** — unhandled exceptions on `/api/*` paths now return `{ "error": "An unexpected error occurred." }` with HTTP 500 instead of leaking stack traces or redirect responses.

## [6.1.2] - 2026-04-25

### Security

- **Unauthenticated dashboard API access** — dashboard and recent-movements API endpoints (`/api/dashboard/*`, `/api/movements/recent`) now return `401` instead of silently redirecting to the login page when called without authentication.
- **SyncController explicit authorization** — added class-level `[Authorize]` to `SyncController` for defense in depth (endpoints were already protected by the global filter; now intent is explicit).
- **API key per-key roles** — `ApiKeyAuthenticationHandler` now supports an `Api:Keys` array where each entry carries its own `Role`. A key configured as `ReadOnly` no longer silently receives `Admin` privileges.
- **Swagger disabled in production** — Swagger UI and JSON schema are only registered in the Development environment; `/swagger/*` returns 404 in production.

### Deprecated

- `Api:Key` (single key, always grants `Admin`) — still works but emits a startup warning. Migrate to the `Api:Keys` array format (see `appsettings.example.json`). Will be removed in v7.0.0.

## [6.1.1] - 2026-04-25

### Added

- CI workflow (`.github/workflows/ci.yml`): runs `dotnet build`, non-E2E tests, and format check on every push and PR.
- Code coverage collection via `coverlet`; HTML report and TRX results uploaded as CI artifacts.
- `TESTING.md`: TDD watch-mode workflow, coverage commands, test organization reference.
- Characterization tests for `ApiKeyAuthenticationHandler`, dashboard API endpoints, `SyncController`, and `CsvImportService` (100 tests total).

## [6.1.0] - 2026-04-20

### Added

- **Serilog file logging** — rolling daily log files under `logs/`, configurable retention (default 30 days prod / 7 days dev), 50 MB per-file limit; all HTTP requests logged via `UseSerilogRequestLogging()`
- **Audit log cleanup** — `AuditLogCleanupService` background service purges audit log entries older than `AuditLog:RetentionDays` (default 90) every night at 02:00
- **Database backup from UI** — Admin → Backup page lets admins download a full database dump (`pg_dump` for PostgreSQL, file copy for SQLite); every download is recorded in the audit log
- **System log viewer** — Admin → Logs page displays rolling Serilog log files with date picker, level filter (ALL/ERR/WRN/INF), and text search; newest entries first, up to 2000 lines
- **Google Drive offsite backup** — Admin → Backup page can upload a fresh backup to Google Drive via rclone; shows configuration status badge and setup instructions when rclone is not yet configured; every upload is recorded in the audit log
- `IOffsiteBackupService` / `OffsiteBackupService` — thin wrapper around the `rclone copyto` CLI; config paths driven by `OffsiteBackup:RcloneConfigPath` and `OffsiteBackup:RemotePath` settings
- `rclone` added to the Docker runtime image; `rclone-config` volume mounted read-only into the app container

### Fixed

- **Serilog file sink in production** — added `createDirectory: true` to File sink config and a code-level fallback that enables file logging when no `File` sink is found in configuration, ensuring log files are always written regardless of environment
- **Migration SQL on PostgreSQL** — quoted all table and column names with double-quotes in the `ExternalIdMappingTables` migration raw SQL; unquoted identifiers were being silently lowercased by PostgreSQL, causing "relation not found" errors at startup

### Changed

- **Multi-store external ID mapping** — replaced single `ExternalId`/`ExternalIdSource` columns on `Product` and `Category` with dedicated `ProductExternalMappings` and `CategoryExternalMappings` junction tables, enabling products and categories to be linked to multiple e-commerce stores simultaneously. Each mapping stores `StoreName`, `ExternalId`, and `Platform` with a composite unique index.

### Removed

- **Product–Supplier direct relationship** — removed `SupplierId` foreign key and `Supplier` navigation property from `Product` model; suppliers are now linked to products only through stock movements (entries record the supplier). Removed supplier column from product views, product CSV import, and supplier product count from supplier views.

### Added

- **Multiple images per product** — upload multiple images on create/edit, image gallery with primary image selection and individual delete via AJAX
- **Product brand field** — new `Brand` property on Product model with index for fast filtering
- **Category hierarchy** — categories can now have a parent category (`ParentId` self-referencing FK), displayed as "Parent > Child" throughout the UI
- **Supplier enriched fields** — `ContactName`, `LeadTimeDays`, and `Notes` fields on Supplier model
- **Inline creation modals** — create categories and suppliers directly from the product form via Bootstrap modals + AJAX (endpoints `CreateInline`)
- **Development seed data** — 5 categories (hierarchical), 6 suppliers, and 20 products from real spreadsheet data seeded automatically in Development environment
- `ProductImage` model with `ImagePath`, `AltText`, `DisplayOrder`, and `IsPrimary` fields
- `_CategoryModal.cshtml` and `_SupplierModal.cshtml` shared partials
- `inline-create.js` for AJAX-based inline entity creation
- EF Core migration `EnrichedModels` with automatic data migration from single `ImagePath` to `ProductImages` table

### Changed

- Product image storage migrated from single `ImagePath` column to `ProductImages` table (one-to-many relationship)
- Product create/edit forms now use `<input type="file" multiple>` for multi-image upload
- Product details view shows image carousel (Bootstrap) when multiple images exist
- Product index shows thumbnail column
- Category dropdowns throughout the app now display `FullName` (hierarchical path)
- Category create/edit forms include parent category dropdown
- Category index shows parent category column
- Supplier create/edit forms include ContactName, LeadTimeDays, and Notes fields
- Supplier index shows contact column
- API `MapProduct` returns `PrimaryImagePath` and `Brand` instead of `ImagePath`

### Removed

- `ImagePath` column from Product table (migrated to ProductImages table)

- TrueNAS catalog app definition (`truenas-catalog/`) — full `ix_lib`-based catalog app with `questions.yaml` UI form, Jinja2 docker-compose template, and test values for the official TrueNAS Apps structure
- Integration tests for form POST submissions (Categories, Suppliers, Products, StockMovements Entry/Exit) — catches database and model binding errors that GET-only tests miss

### Fixed

- PostgreSQL startup fix: corrected `SyncCursors` → `SyncStates` table name in DateTime TEXT→timestamp conversion SQL (fixes OrderSync background service crash)
- PostgreSQL auto-increment sequences: added missing `CREATE SEQUENCE` / `SET DEFAULT nextval` for Categories, Suppliers, Products, StockMovements, ProcessedOrders, and SyncStates tables (fixes 500 errors on form submissions in production)
- Test infrastructure: fixed `WebAppFactory` InMemory database name evaluated per scope (`Guid.NewGuid()` inside lambda) causing each `AppDbContext` to use a different database; added `InMemoryEventId.TransactionIgnoredWarning` suppression so StockMovement transaction-based tests work

## [6.0.0] - 2026-04-16

### Changed

- **BREAKING**: Renamed project from `ControleEstoque` to `InventoryControl` — solution, csproj files, folders, namespaces, Docker config, and all documentation updated

### Added

- Playwright browser-based E2E test suite (30 tests) covering authentication, CRUD operations for categories/suppliers/products, stock movements, reports, admin features, and a full user workflow
- `PlaywrightFixture` with TestServer + Kestrel reverse-proxy architecture for real browser testing
- `PageHelpers` utility class with reusable page interaction methods

### Fixed

- PostgreSQL: convert DateTime TEXT columns to proper `timestamp` types at startup (fixes 500 errors on Reports/Monthly and AuditLogs pages when running with SQLite-generated migrations)

## [5.0.0] - 2026-04-17

### Added

- **Multi-platform architecture** — support for N e-commerce platform integrations simultaneously via platform registry pattern
- `PlatformRegistry` service that discovers and manages platform factories, resolves stores by name or platform store ID
- `IPlatformFactory` interface for creating `IStoreIntegration` instances per platform
- `NuvemshopPlatformFactory` — factory implementation for the Nuvemshop platform
- `SyncServiceFactory` for creating per-store `SyncService` instances with the correct integration
- Multi-store configuration via `Stores` JSON array in `appsettings.json` (replaces single `Integration` object)
- Backward compatibility: legacy `Integration` config section is auto-migrated to the new `Stores` format
- Store management page at Admin → Lojas — shows all configured stores with status, platform, and sync action buttons
- `GET /api/sync/stores` endpoint to list all configured stores
- `?store=<name>` query parameter on all sync API endpoints to target a specific store
- Per-store order sync timestamps in `OrderSyncBackgroundService` (each store tracks its own `lastProcessedAt`)
- Per-store `OrderSyncIntervalMinutes` configuration
- "How to Add a New Platform" contributor guide at `docs/adding-a-platform.md`

### Changed

- **BREAKING**: Store configuration moved from `Integration` object to `Stores` array in `appsettings.json`
- `SyncController` now uses `PlatformRegistry` and `SyncServiceFactory` instead of directly injecting `IStoreIntegration` and `SyncService`
- `OrderSyncBackgroundService` now iterates over all enabled stores instead of syncing a single store
- `NuvemshopWebhookController` matches incoming webhooks to the correct store by `store_id` from the payload
- Nuvemshop HTTP client registered as named client (`Platform_nuvemshop`) via `IHttpClientFactory` instead of typed client

## [4.3.0] - 2026-04-17

### Added

- CSV import for Products, Categories, and Suppliers — upload CSV file, preview parsed data with validation, then confirm import (Admin only)
- Product images — upload images on create/edit, view on product details; stored in `wwwroot/images/products/`
- Full REST API at `/api/v1/` with CRUD endpoints for Products, Categories, and Suppliers
- Swagger UI available at `/swagger` with interactive API documentation
- API key authentication via `X-Api-Key` header for all REST API endpoints
- Import links in Admin dropdown menu (Products, Categories, Suppliers)
- `Api:Key` configuration for API key authentication
- `ImagePath` column on Products table (via EF Core migration)

### Changed

- Product Create and Edit forms now support image upload (`enctype="multipart/form-data"`)
- Product Details view shows product image when available
- Admin dropdown in navbar now includes CSV import links

## [4.2.0] - 2026-04-17

### Added

- Interactive Chart.js dashboard on the home page: movements per month (line chart), stock by category (doughnut chart), and top 10 sellers (horizontal bar chart) with dedicated API endpoints
- Profitability report: cost vs selling price analysis per product with revenue, cost, profit, and margin percentage breakdown
- Email notifications for low stock via configurable SMTP — background service checks products below minimum stock at a configurable interval and sends alert emails
- `EmailNotifications` configuration section in `appsettings.example.json` (SmtpHost, SmtpPort, SmtpUser, SmtpPassword, FromEmail, ToEmail, EnableSsl, CheckIntervalHours)

### Changed

- Home page now includes interactive charts alongside the existing summary cards and recent movements table
- Reports dropdown in navbar now includes "Lucratividade" (Profitability) link

## [4.1.0] - 2026-04-16

### Added

- Pagination in all list views: Products, Categories, Suppliers, Stock Movements, and Audit Logs (configurable page size via query parameter)
- Shared `_Pagination.cshtml` partial view with page navigation controls (first/prev/next/last, ellipsis for large ranges)
- `PagedResult<T>` model for type-safe paginated query results
- `SyncState` model to persist order sync timestamps in the database
- IP rate limiting on API endpoints via AspNetCoreRateLimit (30 POST/min, 60 GET/min)
- Polly retry (3 attempts, 1s delay) and circuit breaker (50% failure threshold, 15s break) for Nuvemshop HTTP client via `Microsoft.Extensions.Http.Resilience`

### Changed

- Repository interfaces and implementations now include paginated `GetAllAsync(int page, int pageSize)` overloads alongside the existing non-paginated versions
- `OrderSyncBackgroundService` now tracks last processed time in the database instead of using a fixed interval×2 lookback window
- All controller Index actions accept `page` and `pageSize` query parameters
- Audit logs viewer paginated with 50 items per page (previously hardcoded to last 200)

## [4.0.0] - 2026-04-16

### Added

- ASP.NET Core Identity authentication with email/password login
- Role-based authorization: Admin (full access) and Operator (day-to-day ops) roles
- Global authentication filter — all routes require login (webhook endpoint exempt via `[AllowAnonymous]`)
- User management UI for Admin: create, edit, delete users and assign roles
- Default admin account seeded on first startup (configurable via `DefaultAdmin` settings)
- Audit trail: `AuditLog` table with EF Core `SaveChanges` interceptor tracking all entity changes (who, what, when, old/new values)
- Audit log viewer for Admin at `/AuditLogs`
- Login page with Bootstrap styling and lockout protection
- Access denied page for unauthorized role access
- Admin menu in navbar: Users and Audit links (visible only to Admin role)
- User dropdown in navbar with logout option

### Changed

- `AppDbContext` now extends `IdentityDbContext<ApplicationUser>` instead of `DbContext`
- `appsettings.example.json` updated with `DefaultAdmin` configuration section
- Test infrastructure updated with `TestAuthHandler` to auto-authenticate integration tests as Admin

## [3.1.0] - 2026-04-16

### Added

- Database transactions wrapping stock entry/exit operations and order sync processing to ensure data consistency
- Unique constraint on `Product.Sku` (filtered index allowing NULLs) with EF Core migration
- Structured API error responses in sync and webhook endpoints, differentiating external API errors (502) from internal errors (500)
- Error handling in Nuvemshop webhook controller with proper HTTP status codes

### Changed

- Test infrastructure: switched from EF Core InMemory provider to SQLite in-memory for transaction support

## [3.0.1] - 2026-04-16

### Fixed

- `docker-compose.truenas.yml` (with real credentials) removed from git tracking and added to `.gitignore`

## [3.0.0] - 2026-04-15

### Added

- TrueNAS-specific Docker Compose deployment files:
  - `docker-compose.truenas.yml` (production with real credentials)
  - `docker-compose.truenas.example.yml` (template with placeholders)
  - Uses ZFS host paths, port `9080`, all Nuvemshop integration vars
- Offsite backup to Google Drive via rclone: syncs local backups every 12h (now always-on, no profiles needed)
- Documentation for TrueNAS deployment and rclone configuration in README
- Full pt-BR localization of the entire user interface (all views, models, controllers)
- Brazilian date format (`dd/MM/yyyy`) and currency format (`R$` with comma decimal separator)
- jQuery Validation override to accept comma as decimal separator in form inputs
- Sticky footer using CSS flexbox so the footer always stays at the bottom of the page
- Dashboard "Movimentações Recentes" table auto-refreshes after any sync button click (new `/api/movements/recent` endpoint)
- `ProcessedOrders` table to track which external orders have already been synced, preventing duplicate stock deductions
- Refund/return stock reversal: when a previously paid order is refunded or voided on the external store, stock is automatically restored via entry movements
- Documented `Integration:OrderSyncIntervalMinutes` in `appsettings.example.json` and README (env var: `Integration__OrderSyncIntervalMinutes`)
- Health check endpoint (`/health`) with database connectivity probe for Docker and TrueNAS monitoring
- Automated PostgreSQL backup container (`prodrigestivill/postgres-backup-local`) running every 12 hours with 7-day daily and 4-week weekly retention
- Unified `docker-compose.yml` with all services: `db`, `app`, `backup`, and `offsite-backup`

### Changed

- Database credentials updated: database `caramelo_inventory`, user `caramelo`
- Offsite backup now always runs (not optional profile) for consistent deployment

### Fixed

- Stock entry/exit returning 500 error on PostgreSQL due to `DateTime` with `Kind=Unspecified` being rejected by Npgsql; enabled legacy timestamp behavior to accept local dates from HTML form inputs
- Added EF Core migration (`LegacyTimestampBehavior`) to change `StockMovements.Date` column from `timestamp with time zone` to `timestamp without time zone`, matching the new legacy timestamp behavior
- Order sync now filters by `payment_status` field (`paid`, `authorized`) instead of the generic `status` field, and rejects cancelled orders — fixes orders with `status: "open"` but valid payment being incorrectly skipped
- Order sync deduplication: the same external order is never processed twice, even across polling cycles or manual syncs
- Order sync returning 502 due to Nuvemshop `created_at` datetime format (`-0300`) not being parseable by `System.Text.Json`; added custom `NuvemshopDateTimeConverter`

## [2.0.0] - 2026-04-12

### Added

- PR template at `.github/PULL_REQUEST_TEMPLATE.md` with standardized description and checklist
- Workflow rules in copilot instructions: pre-commit documentation checks, branch protection, release process
- Semantic versioning release process documented in copilot instructions
- Docker support: `Dockerfile` (multi-stage build), `docker-compose.yml`, `.dockerignore`
- Test suite with xUnit: unit tests (models, repositories, services, view models), integration tests (controllers via WebApplicationFactory), and E2E workflow tests
- `InventoryControl.Tests` project with Moq and InMemory database support
- GitHub Actions workflow `.github/workflows/docker-publish.yml`: builds and pushes the Docker image to Docker Hub on every merge to `main` and on semver tags (`v*.*.*`); uses GitHub Actions cache for faster builds

### Changed

- Switched database from SQLite to **PostgreSQL** as the primary production/Docker provider
- `docker-compose.yml` now includes a `postgres:16-alpine` service with health check; app waits for the DB to be healthy before starting
- `Program.cs` selects the EF Core provider at runtime based on the connection string format (`Host=` → Npgsql, `Data Source=` → SQLite fallback for local dev)
- Migrations automatically applied on startup via `db.Database.Migrate()`
- Regenerated EF Core migrations for PostgreSQL; old SQLite migrations removed
- `appsettings.example.json` updated with PostgreSQL connection string template
- Upgraded `Microsoft.EntityFrameworkCore.*` packages from 10.0.3 to 10.0.4
- Added `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1

## [1.0.0] - 2026-04-03

### Added

- Product management (CRUD) with cost/selling prices, current stock, and minimum stock
- Category and supplier management (CRUD)
- Stock movement tracking: entries (with supplier, unit cost, date) and exits (sale, loss, return)
- Automatic stock level updates on every movement
- Low-stock alert page for products below minimum threshold
- Monthly report with entry/exit breakdown per product
- Nuvemshop e-commerce integration: product sync, stock push, order processing
- Webhook endpoint for real-time order processing from Nuvemshop
- Background service for periodic order synchronization
- SQLite database for local development (zero config)
- Repository pattern for data access layer
- Responsive UI with Bootstrap 5.3 and Bootstrap Icons

### Security

- Sensitive configuration (`appsettings.json`) excluded from version control
- Example config template (`appsettings.example.json`) provided
- CSRF protection on all form-based POST/PUT/DELETE actions
- No raw SQL — all queries through EF Core with LINQ

[Unreleased]: https://github.com/pattersonrptr/inventory-control/compare/v6.1.0...HEAD
[6.1.0]: https://github.com/pattersonrptr/inventory-control/compare/v6.0.0...v6.1.0
[6.0.0]: https://github.com/pattersonrptr/inventory-control/compare/v5.0.0...v6.0.0
[5.0.0]: https://github.com/pattersonrptr/inventory-control/compare/v4.3.0...v5.0.0
[4.3.0]: https://github.com/pattersonrptr/inventory-control/compare/v4.2.0...v4.3.0
[4.2.0]: https://github.com/pattersonrptr/inventory-control/compare/v4.1.0...v4.2.0
[4.1.0]: https://github.com/pattersonrptr/inventory-control/compare/v4.0.0...v4.1.0
[4.0.0]: https://github.com/pattersonrptr/inventory-control/compare/v3.1.0...v4.0.0
[3.1.0]: https://github.com/pattersonrptr/inventory-control/compare/v3.0.1...v3.1.0
[3.0.1]: https://github.com/pattersonrptr/inventory-control/compare/v3.0.0...v3.0.1
[3.0.0]: https://github.com/pattersonrptr/inventory-control/compare/v2.0.0...v3.0.0
[2.0.0]: https://github.com/pattersonrptr/inventory-control/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/pattersonrptr/inventory-control/releases/tag/v1.0.0
