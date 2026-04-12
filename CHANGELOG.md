# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/pattersonrptr/inventory-control/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/pattersonrptr/inventory-control/releases/tag/v1.0.0
