# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
- `ControleEstoque.Tests` project with Moq and InMemory database support
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

[Unreleased]: https://github.com/pattersonrptr/inventory-control/compare/v4.3.0...HEAD
[4.3.0]: https://github.com/pattersonrptr/inventory-control/compare/v4.2.0...v4.3.0
[4.2.0]: https://github.com/pattersonrptr/inventory-control/compare/v4.1.0...v4.2.0
[4.1.0]: https://github.com/pattersonrptr/inventory-control/compare/v4.0.0...v4.1.0
[4.0.0]: https://github.com/pattersonrptr/inventory-control/compare/v3.1.0...v4.0.0
[3.1.0]: https://github.com/pattersonrptr/inventory-control/compare/v3.0.1...v3.1.0
[3.0.1]: https://github.com/pattersonrptr/inventory-control/compare/v3.0.0...v3.0.1
[3.0.0]: https://github.com/pattersonrptr/inventory-control/compare/v2.0.0...v3.0.0
[2.0.0]: https://github.com/pattersonrptr/inventory-control/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/pattersonrptr/inventory-control/releases/tag/v1.0.0
