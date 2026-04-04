# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
