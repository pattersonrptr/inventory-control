# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

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
- Optional offsite backup to Google Drive via rclone (`docker compose --profile offsite up -d`)
- Unified `docker-compose.yml` with all services: `db`, `app`, `backup`, and opt-in `offsite-backup`

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

[Unreleased]: https://github.com/pattersonrptr/inventory-control/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/pattersonrptr/inventory-control/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/pattersonrptr/inventory-control/releases/tag/v1.0.0
