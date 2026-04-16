# Plan: Improvements & Features Roadmap

## Context

Inventory Control is a production ASP.NET Core MVC (.NET 10) system running on TrueNAS SCALE
with PostgreSQL, automated local backups, and offsite sync to Google Drive (v3.0.0).

This plan addresses **12 verified improvement points** and **10 new features**, organized
into 6 phases following Semantic Versioning.

## Users

- **Admin** (owner): Full access, can manage users and roles, promote operators to admin.
- **Operator** (sister): Day-to-day operations — register movements, view reports, sync.

---

## Verified Improvement Points

| #  | Severity | Issue |Status |
|----|----------|-------|-------|
| 1  | CRITICAL | No transactions in multi-step operations (SyncService + StockMovementsController) | ✅ |
| 2  | CRITICAL | No uniqueness constraint on Product.Sku | ✅ |
| 3  | HIGH     | No pagination in list views / repositories | ✅ |
| 4  | HIGH     | OrderSync fetches all orders in interval×2 window | ✅ |
| 5  | HIGH     | No rate limiting on API endpoints | ✅ |
| 6  | MEDIUM   | ProcessOrderAsync partial success — no rollback on item failure | ✅ |
| 7  | MEDIUM   | No retry / circuit breaker on Nuvemshop API calls | ✅ |
| 8  | MEDIUM   | API returns generic 502 — no useful error details | ✅ |
| 10 | LOW      | No audit trail (who / what / when / how) | ✅ |
| 11 | LOW      | No CSV/Excel import for bulk data entry | 🔲 |
| 12 | LOW      | No product images (upload or pull from store) | 🔲 |
| 13 | LOW      | No authentication / authorization | ✅ |

> Note: #9 was removed (category sync button already exists in UI).

## Planned Features

| ID  | Feature | Priority |
|-----|---------|----------|
| F1  | Authentication with ASP.NET Core Identity + Roles (Admin / Operator) | HIGH |
| F2  | Pagination in all list views | HIGH |
| F3  | Dashboard with Chart.js (movements/period, top sellers, stock/category, profitability) | ✅ |
| F4  | Email notifications for low stock (SMTP) | ✅ |
| F5  | CSV import for Products, Suppliers, Categories | MEDIUM |
| F6  | Audit trail — logs who changed what, when, and how | MEDIUM |
| F7  | Product images — upload local + push/pull from Nuvemshop | MEDIUM |
| F8  | Public REST API with Swagger, API key auth, rate limiting | MEDIUM |
| F9  | Profitability report (cost vs selling price × quantity sold) | ✅ |
| F10 | Multi-platform preparation — architecture for N e-commerce platforms | LOW |

---

## Execution Phases

### Phase 1 — Data Integrity & Foundation (`v3.1.0` PATCH)

> Critical fixes that prevent **data inconsistency in production**.

| Item | What |
|------|------|
| #1 + #6 | Wrap multi-step operations in `IDbContextTransaction` (SyncService + StockMovementsController) |
| #2 | Migration: add unique filtered index on `Product.Sku` (nullable) |
| #8 | Improve API error responses with structured problem details |

**Branch**: `fix/data-integrity` → PR → merge → release `v3.1.0`

### Phase 2 — Authentication & Audit Trail (`v4.0.0` MAJOR)

> Breaking change: all routes require login.

| Item | What |
|------|------|
| #13 / F1 | ASP.NET Core Identity — login, register, roles (Admin, Operator) |
| — | Admin can manage users, assign roles, promote operators |
| #10 / F6 | `AuditLog` model + EF Core SaveChanges interceptor |
| — | Seed default admin user |

**Branch**: `feat/authentication` → PR → release `v4.0.0`

### Phase 3 — Performance & Resilience (`v4.1.0` MINOR)

> Scale and resilience for growing order volume.

| Item | What |
|------|------|
| #3 / F2 | Pagination in all repositories + views (skip/take + page controls) |
| #4 | OrderSync: track `lastProcessedAt`, fetch only since last run |
| #5 | Rate limiting middleware (`AspNetCoreRateLimit`) |
| #7 | Polly retry + circuit breaker for `NuvemshopClient` |

**Branch**: `feat/performance` → PR → release `v4.1.0`

### Phase 4 — Dashboard & Notifications (`v4.2.0` MINOR)

> Visibility and proactive alerts.

| Item | What |
|------|------|
| F3 | Chart.js dashboard: movements/period, top sellers, stock/category, profitability |
| F9 | Profitability report (CostPrice vs SellingPrice × quantity sold) |
| F4 | Email alerts for stock below minimum (configurable SMTP) |

**Branch**: `feat/dashboard-notifications` → PR → release `v4.2.0`

### Phase 5 — Import, Images & API (`v4.3.0` MINOR)

> Productivity and extensibility.

| Item | What |
|------|------|
| F5 | CSV import for Products, Suppliers, Categories (upload → preview → confirm) |
| F7 | Product images: upload local + push/pull from Nuvemshop |
| F8 | Full REST API + Swagger UI + API key authentication |

**Branch**: `feat/import-images-api` → PR → release `v4.3.0`

### Phase 6 — Multi-platform Preparation (`v5.0.0` MAJOR)

> Open-source readiness: support N e-commerce platforms.

| Item | What |
|------|------|
| F10 | Multi-store config, platform registry pattern, store selector in UI |
| — | Documentation: "How to add a new platform" contributor guide |

**Branch**: `feat/multi-platform` → PR → release `v5.0.0`

---

## Execution Strategy

1. **One phase at a time** — branch → implement → tests → PR → release → deploy to TrueNAS.
2. **Full project re-read** at the start of each phase to maintain context.
3. **Tests required** before every merge (unit + integration + E2E).
4. **CHANGELOG + README** updated with every release.
5. **Divide and conquer** — large phases are split into focused commits.

## Decisions

- **Identity over custom auth** — built-in, battle-tested, supports roles out of the box.
- **Chart.js over paid libraries** — lightweight, MIT license, widely used.
- **Polly for resilience** — standard .NET retry/circuit breaker library.
- **AspNetCoreRateLimit** — simple middleware, no external dependencies.
- **CSV over Excel** — simpler parsing, universal format. Can add Excel later.
- **Multi-platform last** — architecture is already platform-agnostic via `IStoreIntegration`.
