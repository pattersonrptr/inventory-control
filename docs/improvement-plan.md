# Inventory Control — Improvement Plan

## Guiding Principles

1. **Security first, architecture later.** Security fixes are local; they don't get invalidated by the upcoming restructure.
2. **Never refactor untested code.** Characterization tests before every non-trivial change, TDD (red-green-refactor) for every new behavior.
3. **Restructure before domain enrichment.** Moving files and changing entity behavior at the same time multiplies risk.
4. **Each phase ends in a working, deployable app.** No long-lived "broken main".
5. **Dev pushes for checkpoints; PRs only at release boundaries.** Feature branches can take multiple commits and pushes; main only receives a merged PR when a release is cut.
6. **CHANGELOG updated in the same commit that introduces the change.** No end-of-phase "remember to update the changelog".

---

## Workflow Conventions

**Branching:**
- `main` — protected; only receives merged release PRs.
- `work/<phase>-<short>` — long-lived work branches for each phase (e.g., `work/fase1-critical-security`). Multiple dev pushes allowed.
- CI must be green before merging to `main`.

**Release ritual (end of each phase):**
1. Ensure all tests green on work branch.
2. Bump version in `InventoryControl.csproj`.
3. Finalize `CHANGELOG.md` release section (move "Unreleased" → `[X.Y.Z] - YYYY-MM-DD`).
4. Open PR `work/...` → `main`.
5. Wait for CI green.
6. Merge.
7. Tag: `git tag vX.Y.Z && git push --tags`.
8. Verify Docker Hub image published (existing automation).

**TDD discipline:**
- **New behavior:** Red → Green → Refactor. Test first, always.
- **Changes to existing code without tests:** Write characterization test covering current behavior → verify it passes → then change code → verify new expected behavior.
- **Refactor (no behavior change):** Existing tests must remain green throughout.
- Dev push = one logical TDD cycle (failing test + implementation + refactor commits, or one squashed commit per cycle).

**Migration strategy decision (locked in):**
- A separate service in `docker-compose.yml` named `db-migrate` runs `dotnet ef database update` and exits. App service `depends_on: db-migrate: condition: service_completed_successfully`. Same pattern in both the regular compose and the TrueNAS compose.

---

## Target Architecture (End State)

```
InventoryControl/
├── Domain/
│   ├── Products/               Product entity + value objects (Sku, Money)
│   ├── Stock/                  StockMovement entity + invariants
│   ├── Orders/                 ProcessedOrder + related
│   ├── Catalog/                Category, Supplier
│   ├── Integrations/           ExternalMapping, SyncState
│   ├── Audit/                  AuditLog
│   └── Common/                 base types, shared value objects
├── Features/
│   ├── Products/               Controller + Service + DTOs + Validators
│   ├── StockMovements/
│   ├── Categories/
│   ├── Suppliers/
│   ├── Sync/
│   ├── Reports/
│   ├── Backup/
│   ├── Logs/
│   └── Account/                (login, user management)
├── Infrastructure/
│   ├── Persistence/            AppDbContext, EF configs, repositories
│   ├── Integrations/           Nuvemshop + future platforms
│   ├── Email/                  SMTP sender
│   ├── Backup/                 pg_dump service, rclone service
│   ├── Auth/                   ApiKeyAuthenticationHandler
│   └── BackgroundJobs/         Hosted services
├── Web/
│   ├── Program.cs
│   ├── Middleware/
│   └── Filters/
├── Views/                      (unchanged)
├── wwwroot/                    (unchanged)
└── Migrations/                 (unchanged)
```

**Dependency rules:**
- `Domain` depends on nothing outside the BCL.
- `Features` depend on `Domain` + abstractions defined in `Domain` (repositories).
- `Infrastructure` depends on `Domain` (implements its abstractions).
- `Web` depends on `Features` + `Infrastructure` (composition root in `Program.cs`).

Enforced via a lightweight architecture test (NetArchTest or ArchUnitNET) in the test project.

---

## Phases Overview

| Etapa | Fase | Version | Theme |
|---|---|---|---|
| 1 | 0 | 6.1.1 | CI foundation & characterization tests |
| 1 | 1 | 6.1.2 | Critical security (auth, authorization) |
| 1 | 2 | 6.2.0 | Input validation & hardening (FluentValidation) |
| 1 | 3 | 6.3.0 | Operational hardening (migrations, backoff, disposal) |
| 2 | 4 | 6.4.0 | Performance basics (indexes, over-fetching) |
| 3 | 5 | 7.0.0 | Restructure to Modular Monolith + feature folders |
| 4 | 6 | 7.1.0 | Selective domain enrichment (rich entities) |
| 5 | 7 | 7.2.0 | Read models per feature |
| 6 | 8 | 7.3.x | Comprehensive test coverage + coverage gate |

---

## Detailed Plan

### ETAPA 1 — Foundation, Security, Hardening

---

#### **Fase 0 — CI Foundation & Characterization Tests** → `v6.1.1`
**Risk:** Very low. Additive only.
**Goal:** Make subsequent phases safe.

**Sub-phase 0.1 — CI hardening**
- 0.1.1 Verify `.github/workflows/*.yml` runs `dotnet build` + `dotnet test` on every push and PR. Add or fix if missing.
- 0.1.2 Add branch protection rule on `main` (manual step documented in README): require PR + passing CI.
- 0.1.3 Add `dotnet format --verify-no-changes` as a CI step (enforces formatting).

**Sub-phase 0.2 — Coverage visibility**
- 0.2.1 Add `coverlet.collector` to test project (if not present).
- 0.2.2 Configure `dotnet test --collect:"XPlat Code Coverage"`.
- 0.2.3 Upload coverage artifact in CI; generate HTML report (ReportGenerator). No gate yet — visibility only.

**Sub-phase 0.3 — TDD workflow documentation**
- 0.3.1 Add a `TESTING.md` section explaining `dotnet watch test --project InventoryControl.Tests` for TDD loop.
- 0.3.2 Add `dotnet watch test` note to `CLAUDE.md` / `AGENTS.md`.

**Sub-phase 0.4 — Characterization tests for hot spots about to change**
- 0.4.1 Tests for `ApiKeyAuthenticationHandler` (what roles/claims current implementation produces, success/failure paths).
- 0.4.2 Tests for `HomeController` dashboard endpoints (current behavior & responses).
- 0.4.3 Tests for `SyncController` key methods (current auth state, sync dispatch).
- 0.4.4 Tests for `ProductsController.Edit` direct-DbContext path (current image save behavior).
- 0.4.5 Tests for `CsvImportService` happy path + malformed line handling.

**CHANGELOG:**
```
### Added
- CI enforces build, tests, and formatting on every PR.
- Code coverage reporting (visibility only, no gate yet).
- Characterization tests for controllers and services scheduled for refactor.
```

**Dev pushes expected:** ~6-8 (one per sub-sub-step).

---

#### **Fase 1 — Critical Security** → `v6.1.2`
**Risk:** Low. Localized, well-covered by Fase 0 tests.
**Goal:** Close the three critical/high-severity auth gaps.

**Sub-phase 1.1 — Missing `[Authorize]` attributes**
- 1.1.1 (TDD) Add failing tests: `GET /api/dashboard/*` returns 401 when unauthenticated. Currently passes anonymously — test goes red.
- 1.1.2 Add `[Authorize]` to `HomeController.MovementsByMonth`, `TopSellers`, `StockByCategory`, `RecentMovements`.
- 1.1.3 (TDD) Add failing test: `POST /api/sync/*` returns 401 when unauthenticated.
- 1.1.4 Add `[Authorize]` to `SyncController` (class-level).
- 1.1.5 Verify all tests green.

**Sub-phase 1.2 — API Key auth refactor**
Legacy config supported, new config preferred. Not a breaking change yet — breaking change comes in v7.0.0 when we drop legacy.

- 1.2.1 (TDD) Test: new `Api:Keys` array format in config is read correctly; each entry has `Key` + `Role`.
- 1.2.2 Introduce new config model `ApiKeyOptions { List<ApiKeyEntry> Keys }` alongside existing `Api:Key`.
- 1.2.3 (TDD) Test: if request uses a key from `Api:Keys`, claims include the configured role (not hardcoded `Admin`).
- 1.2.4 Implement new lookup logic in `ApiKeyAuthenticationHandler`.
- 1.2.5 (TDD) Test: legacy single `Api:Key` still works and still assigns `Admin` (backward compat), but emits a warning log.
- 1.2.6 Update `appsettings.example.json` showing both formats, preferring new.
- 1.2.7 Document in `CHANGELOG.md` deprecation of `Api:Key`.

**Sub-phase 1.3 — Swagger guard**
- 1.3.1 (TDD) Test: in Production env, `/swagger` requires authentication.
- 1.3.2 Wrap `app.UseSwaggerUI()` with auth requirement or `IWebHostEnvironment.IsDevelopment()` check.
- 1.3.3 Decide: Dev = open, Prod = requires login. Implement.

**CHANGELOG:**
```
### Security
- Added `[Authorize]` to previously anonymous dashboard and sync API endpoints.
- API key authentication now supports per-key roles via new `Api:Keys` array.
- Swagger UI now requires authentication in production.

### Deprecated
- Single `Api:Key` config value; use `Api:Keys` array instead. Legacy format still works in v6.x, will be removed in v7.0.0.
```

**Dev pushes expected:** ~10.

---

#### **Fase 2 — Input Validation & Hardening** → `v6.2.0`
**Risk:** Low-medium. Additive; existing callers of valid inputs keep working.
**Goal:** Close validation gaps, normalize error handling, introduce FluentValidation.

**Sub-phase 2.1 — FluentValidation bootstrap**
- 2.1.1 Add packages: `FluentValidation.AspNetCore`, `FluentValidation.DependencyInjectionExtensions`.
- 2.1.2 Register `AddValidatorsFromAssemblyContaining<Program>()` in `Program.cs`.
- 2.1.3 Add `[ApiController]` attribute to all API controllers (automatic 400 on validation failures).

**Sub-phase 2.2 — Validators for API DTOs**
For each DTO, TDD cycle: write validator tests first, then validator.
- 2.2.1 `ProductCreateDto` validator: Name required + max length, prices ≥ 0, CategoryId > 0, MinimumStock ≥ 0, Sku format if provided.
- 2.2.2 `ProductUpdateDto` validator.
- 2.2.3 `CategoryDto` validator.
- 2.2.4 `SupplierDto` validator (if exists).
- 2.2.5 `StockMovementDto` validator: quantity != 0, reason required, etc.

**Sub-phase 2.3 — File upload hardening**
- 2.3.1 (TDD) Test: uploading `.exe` returns 400 (not silently renamed to .jpg).
- 2.3.2 Change `SaveImagesAsync` to reject invalid extensions instead of renaming.
- 2.3.3 (TDD) Test: uploading a file with image extension but executable MIME returns 400.
- 2.3.4 Validate `IFormFile.ContentType` against whitelist.
- 2.3.5 (TDD) Test: max file size enforced.
- 2.3.6 Add configurable max upload size.

**Sub-phase 2.4 — Error response sanitization**
- 2.4.1 (TDD) Test: thrown exception in controller returns generic error message to user, full details in logs.
- 2.4.2 Add global exception handler middleware in `Program.cs`.
- 2.4.3 Replace inline `ex.Message` leaks in `BackupController`, `ImportController`, etc. with generic messages.
- 2.4.4 Ensure `TempData["Error"]` never contains exception text.

**Sub-phase 2.5 — DateTime.UtcNow everywhere**
- 2.5.1 Grep for `DateTime.Now`. Replace with `DateTime.UtcNow` in: `AuditInterceptor`, `AuditLog`, `LowStockNotificationService`, any other hits.
- 2.5.2 Views that format dates: convert UTC → local at display time using `TimeZoneInfo`.
- 2.5.3 Add `IClock` abstraction with `ISystemClock` implementation (supports mocking in tests).
- 2.5.4 (TDD) Test: audit log timestamps are UTC.

**Sub-phase 2.6 — Password policy tightening**
- 2.6.1 (TDD) Test: new user with 6-char password is rejected.
- 2.6.2 Update Identity password options: `RequiredLength = 10`, `RequireDigit = true`, `RequireNonAlphanumeric = true`, `RequireUppercase = true`.
- 2.6.3 Document in CHANGELOG: existing users keep their passwords; policy applies to new/changed only.

**CHANGELOG:**
```
### Added
- FluentValidation on all API DTOs.
- Global exception handler; sanitized user-facing error messages.
- `IClock` abstraction for testable timestamps.

### Changed
- Password policy: minimum 10 characters with digit, uppercase, and non-alphanumeric.
- File uploads: invalid extensions/MIME types now rejected instead of silently renamed.
- All timestamps stored in UTC.

### Security
- Hardened file upload validation (extension + MIME + size).
```

**Dev pushes expected:** ~15.

---

#### **Fase 3 — Operational Hardening** → `v6.3.0`
**Risk:** Medium. Deployment workflow changes.
**Goal:** Production-grade runtime behavior.

**Sub-phase 3.1 — Remove auto-migration from startup**
- 3.1.1 (TDD) Test: app startup does not call `db.Database.Migrate()`.
- 3.1.2 Remove migration block from `Program.cs`.
- 3.1.3 Add `Tools/migrate.csproj` or CLI argument: `dotnet run -- migrate` runs migrations and exits.
- 3.1.4 Update `docker-compose.yml`: add `db-migrate` service using same image, command `dotnet InventoryControl.dll migrate`. Set `restart: "no"` and `depends_on: db: condition: service_healthy`. App service has `depends_on: db-migrate: condition: service_completed_successfully`.
- 3.1.5 Update TrueNAS compose.
- 3.1.6 Document in `README.md` and `CHANGELOG.md` the new deploy flow.

**Sub-phase 3.2 — Background service resilience**
- 3.2.1 (TDD) Test: after N consecutive failures, `OrderSyncBackgroundService` increases delay with exponential backoff.
- 3.2.2 Implement failure counter + exponential backoff (cap at e.g. 30 min).
- 3.2.3 (TDD) Test: `LowStockNotificationService` disposes `MailMessage` on exception.
- 3.2.4 Wrap `MailMessage` in `using`.
- 3.2.5 Review all `IHostedService` implementations for similar resource leaks.

**Sub-phase 3.3 — Healthcheck upgrade**
- 3.3.1 Add `AddDbContextCheck<AppDbContext>()`.
- 3.3.2 (TDD) Integration test: `/health` returns healthy when DB is reachable, unhealthy otherwise.
- 3.3.3 Add health endpoint to `[Authorize]` scope? → No, keep anonymous for liveness probes, but split `/health/ready` (full) from `/health/live` (basic). Only `/health/live` is anonymous.

**Sub-phase 3.4 — Secret handling cleanups**
- 3.4.1 `DatabaseBackupService`: minimize lifetime of the password variable (pass to `ProcessStartInfo.Environment` immediately, don't retain).
- 3.4.2 Document rclone config file permission requirement more prominently; add startup warning if file is world-readable.

**CHANGELOG:**
```
### Changed
- **BREAKING (deploy):** Migrations no longer run on app startup. Use the `db-migrate` service in docker-compose or run `dotnet InventoryControl.dll migrate` before starting the app.
- Background services now use exponential backoff on repeated failures.

### Fixed
- `MailMessage` properly disposed on SMTP send failures.

### Security
- Database password no longer retained in memory longer than necessary.
- Startup warns when rclone config has overly permissive file permissions.
```

**Dev pushes expected:** ~12.

---

### ETAPA 2 — Performance Basics

---

#### **Fase 4 — Queries & Indexes** → `v6.4.0`
**Risk:** Low-medium. Pure optimization.
**Goal:** Fix O(n) disguised as O(1), eliminate full-table scans on hot queries.

**Sub-phase 4.1 — Database indexes**
- 4.1.1 Generate migration `AddPerformanceIndexes`: index on `Products.CurrentStock`, composite index `(CurrentStock, MinimumStock)`, index on `StockMovements.CreatedAt`, any other filter columns identified.
- 4.1.2 Verify via `EXPLAIN` (PostgreSQL) that low-stock and recent-movements queries use indexes.

**Sub-phase 4.2 — Split list vs detail repository methods**
TDD cycle per method pair.
- 4.2.1 `IProductRepository.GetAllForListAsync()` — no `Include`, returns projected DTO or minimal entity.
- 4.2.2 `IProductRepository.GetByIdWithDetailsAsync(int id)` — with `Include`.
- 4.2.3 Similarly for `CategoryRepository`, `SupplierRepository`.
- 4.2.4 Update callers to use appropriate method.
- 4.2.5 Remove or deprecate the old "load everything" method.

**Sub-phase 4.3 — DB-level pagination**
- 4.3.1 (TDD) Test: `GetAllAsync(page, pageSize)` only materializes `pageSize` rows.
- 4.3.2 Ensure `.Skip().Take()` comes before `Include` where possible; use subquery or `.AsSplitQuery()` where needed.
- 4.3.3 Verify generated SQL includes `LIMIT` / `OFFSET`.

**CHANGELOG:**
```
### Changed
- Product and Category repositories split into list (lightweight) and detail (with includes) methods.
- Pagination now happens at the database level.

### Performance
- New indexes on Products(CurrentStock, MinimumStock) and StockMovements(CreatedAt).
- Low-stock query no longer requires table scan.
```

**Dev pushes expected:** ~8.

---

### ETAPA 3 — Restructure to Modular Monolith

---

#### **Fase 5 — Feature Folders Restructure** → `v7.0.0`
**Risk:** High in scope, but each sub-phase is low-risk if done with tests green throughout.
**Goal:** Move to the target architecture. **No behavior changes in this phase.** If tests go red, we stop and fix before proceeding.

**Critical discipline:** Every sub-phase below must end with all tests green and the app running. No "works in progress" crossing sub-phase boundaries.

**Sub-phase 5.1 — Architecture test harness**
- 5.1.1 Add `NetArchTest.Rules` package to test project.
- 5.1.2 Write the future dependency rules as tests that currently **skip** (will flip to active as we move code):
  - `Domain` namespace has no reference to `Microsoft.EntityFrameworkCore`.
  - `Features` namespaces don't reference `Infrastructure.Persistence.*` implementation types.
  - `Infrastructure` references `Domain` interfaces only.

**Sub-phase 5.2 — Create empty target structure**
- 5.2.1 Create folders: `Domain/`, `Features/`, `Infrastructure/`, `Web/`.
- 5.2.2 Add `README.md` in each top-level folder explaining its purpose (short — 3-5 lines).
- 5.2.3 No code moved yet.

**Sub-phase 5.3 — Move Domain (entities only)**
Per entity/aggregate: move file, update namespace, update all references. Tests green after each move.
- 5.3.1 Move `Product`, `ProductImage` → `Domain/Products/`.
- 5.3.2 Move `Category` → `Domain/Catalog/`.
- 5.3.3 Move `Supplier` → `Domain/Catalog/`.
- 5.3.4 Move `StockMovement` → `Domain/Stock/`.
- 5.3.5 Move `ProcessedOrder` → `Domain/Orders/`.
- 5.3.6 Move `ExternalMapping`, `SyncState` → `Domain/Integrations/`.
- 5.3.7 Move `AuditLog` → `Domain/Audit/`.
- 5.3.8 Move `ApplicationUser` → `Domain/Identity/`.
- 5.3.9 Delete empty `Models/` folder.
- 5.3.10 Activate the first architecture test (`Domain` has no EF references). Expected to fail if any entity has `[NotMapped]` or EF attributes — resolve by moving those to EF configurations in 5.4.

**Sub-phase 5.4 — Move Infrastructure/Persistence**
- 5.4.1 Move `Data/AppDbContext.cs`, `Data/AuditInterceptor.cs` → `Infrastructure/Persistence/`.
- 5.4.2 Extract Fluent API configurations from `AppDbContext.OnModelCreating` into `IEntityTypeConfiguration<T>` classes in `Infrastructure/Persistence/Configurations/` (one per entity).
- 5.4.3 Move each `Repositories/*Repository.cs` → `Infrastructure/Persistence/Repositories/`.
- 5.4.4 Move `IRepository` interfaces → `Domain/<Aggregate>/` (each interface lives next to the aggregate it serves).
- 5.4.5 Activate dependency-direction architecture tests.

**Sub-phase 5.5 — Move Infrastructure/Integrations, Email, Backup, Auth**
- 5.5.1 `Integrations/` → `Infrastructure/Integrations/`.
- 5.5.2 `Services/DatabaseBackupService.cs`, `OffsiteBackupService.cs` → `Infrastructure/Backup/`.
- 5.5.3 SMTP logic (currently inline in `LowStockNotificationService`) → extract to `Infrastructure/Email/IEmailSender` + `SmtpEmailSender`. Service depends on the interface.
- 5.5.4 `Authentication/ApiKeyAuthenticationHandler.cs` → `Infrastructure/Auth/`.
- 5.5.5 `BackgroundServices/` → `Infrastructure/BackgroundJobs/`.

**Sub-phase 5.6 — Move Features (one vertical slice at a time)**
Per slice: move controller, extract use-case logic into a `*Service` class in the same folder, move DTOs and validators. Tests green after each slice.
- 5.6.1 **Products slice**: `ProductsController` + extract `ProductService` (handles create/edit/delete orchestration). Image upload logic moves to a `ProductImageService` in same folder.
- 5.6.2 **Categories slice**.
- 5.6.3 **Suppliers slice**.
- 5.6.4 **StockMovements slice**.
- 5.6.5 **Sync slice**: `SyncController` + `SyncService` lives here (orchestration); integration adapters stay in `Infrastructure/Integrations/`.
- 5.6.6 **Reports slice**.
- 5.6.7 **Backup slice**: `BackupController` + thin service calling `Infrastructure/Backup`.
- 5.6.8 **Logs slice** (log viewer, audit logs).
- 5.6.9 **Account slice** (login, register, user management).
- 5.6.10 **Api slice**: move API controllers into the feature they belong to (e.g., `ProductsApiController` lives alongside `ProductsController` under `Features/Products/`).
- 5.6.11 **Home/Dashboard slice**.

**Sub-phase 5.7 — Move Web concerns**
- 5.7.1 Keep `Program.cs` at project root (framework convention).
- 5.7.2 Create `Web/DependencyInjection.cs` with extension method `AddInventoryControl(this IServiceCollection)` that wires everything. `Program.cs` calls this.
- 5.7.3 Move middleware, filters to `Web/`.

**Sub-phase 5.8 — Drop legacy `Api:Key`**
This is where we cash in the v7 breaking-change check.
- 5.8.1 Remove legacy `Api:Key` read path from `ApiKeyAuthenticationHandler`.
- 5.8.2 Update `appsettings.example.json` to remove the legacy format.
- 5.8.3 Document in CHANGELOG under Breaking.

**Sub-phase 5.9 — Cleanup and final verification**
- 5.9.1 Delete all old folders (`Controllers/`, `Repositories/`, `Services/`, `Integrations/`, `Data/`, `Models/`, `BackgroundServices/`, `Authentication/`).
- 5.9.2 Ensure all architecture tests are active and green.
- 5.9.3 Full manual smoke test of UI and API.
- 5.9.4 Update `CLAUDE.md` architecture section to reflect new structure.

**CHANGELOG:**
```
## [7.0.0] - YYYY-MM-DD

### Breaking
- **Deployment:** Auto-migration no longer runs on startup (first introduced in 6.3.0; now strictly required).
- **Config:** Legacy `Api:Key` removed. Use `Api:Keys` array.
- **Internal structure:** Project reorganized into Modular Monolith with feature folders. Source directory layout changed.

### Added
- Architecture tests enforce dependency rules (Domain has no infra deps, etc.).
- `IEmailSender` abstraction.
- Per-feature service classes (`ProductService`, `StockMovementService`, etc.).

### Changed
- Source organized by feature, not by technical layer.
- EF configurations extracted from `AppDbContext` into per-entity `IEntityTypeConfiguration` classes.
```

**Dev pushes expected:** ~40-50 (high granularity for safety).

---

### ETAPA 4 — Selective Domain Enrichment

---

#### **Fase 6 — Rich Entities & Value Objects** → `v7.1.0`
**Risk:** Medium. Changes entity public surface; callers need updating.
**Goal:** Move invariants from controllers/services into entities. Only where it pays off — no DDD-for-DDD's-sake.

**Sub-phase 6.1 — `Product` as rich entity**
- 6.1.1 (TDD) Test: `product.AdjustStock(-10, reason)` fails if result would be negative.
- 6.1.2 Add `AdjustStock(int delta, string reason)` method; make `CurrentStock` setter private.
- 6.1.3 Refactor `StockMovementService` to call `product.AdjustStock(...)` instead of setting `CurrentStock` directly.
- 6.1.4 (TDD) Test: `product.UpdatePricing(cost, selling)` validates cost ≥ 0 and selling ≥ cost (or whatever rule we define).
- 6.1.5 Add `UpdatePricing`; make price setters private.

**Sub-phase 6.2 — `Sku` value object**
- 6.2.1 (TDD) Tests: Sku normalization (uppercase), validation (format/length), equality by value.
- 6.2.2 Create `Domain/Products/Sku.cs` as `readonly record struct`.
- 6.2.3 Change `Product.Sku` to `Sku?` type. Add EF Core value converter.
- 6.2.4 Migration for schema — should be no-op (underlying string column unchanged).
- 6.2.5 Update forms/views to accept/display string and convert at boundary.

**Sub-phase 6.3 — `Money` value object**
- 6.3.1 (TDD) Tests: `Money` with amount + currency; arithmetic; currency mismatch throws.
- 6.3.2 Create `Domain/Common/Money.cs`.
- 6.3.3 Evaluate: is currency actually variable in this system, or always BRL? If always BRL, skip currency and just wrap decimal with domain meaning ("Price"). Decide based on the data.
- 6.3.4 Apply selectively: `Product.CostPrice`, `Product.SellingPrice`. Not every decimal.

**Sub-phase 6.4 — `StockMovement` invariants**
- 6.4.1 (TDD) Test: `StockMovement.Create(...)` with Quantity = 0 throws.
- 6.4.2 Make `StockMovement` constructor private; expose `Create(Product, int quantity, MovementType, string reason)` factory.
- 6.4.3 Factory enforces: quantity ≠ 0, reason required for outbound movements.

**Sub-phase 6.5 — `ProcessedOrder` aggregation**
- 6.5.1 Encapsulate order processing: `ProcessedOrder.MarkProcessed(...)` with state-transition rules.
- 6.5.2 Move idempotency check logic into aggregate if it fits.

**CHANGELOG:**
```
### Added
- `Product.AdjustStock()` enforces non-negative stock invariant.
- `Sku` and `Money` value objects.
- `StockMovement.Create()` factory with invariants.

### Changed
- `Product.CurrentStock`, `CostPrice`, `SellingPrice` setters are now private. Use domain methods.
```

**Dev pushes expected:** ~15.

---

### ETAPA 5 — Read Models

---

#### **Fase 7 — Per-feature Read Models** → `v7.2.0`
**Risk:** Low. Additive; old repo methods retained until replaced.
**Goal:** For hot read paths, project directly to DTOs in EF query — no entity loading.

**Sub-phase 7.1 — Identify hot read paths**
- 7.1.1 List top endpoints by likely frequency: dashboard (3 endpoints), product list, low-stock report, recent movements, reports pages.
- 7.1.2 Baseline with a quick benchmark (optional: BenchmarkDotNet, or just manual timing).

**Sub-phase 7.2 — Queries folder per feature**
For each hot path, TDD cycle.
- 7.2.1 `Features/Products/Queries/GetProductListQuery.cs` — returns `ProductListItem` DTOs via `.Select()`.
- 7.2.2 `Features/Products/Queries/GetLowStockQuery.cs`.
- 7.2.3 `Features/Dashboard/Queries/GetMovementsByMonthQuery.cs` and siblings.
- 7.2.4 `Features/Reports/Queries/*`.

**Sub-phase 7.3 — Swap callers**
- 7.3.1 Update controllers to call queries instead of repositories for read operations.
- 7.3.2 Remove now-unused `GetAllForListAsync` methods where replaced.

**CHANGELOG:**
```
### Performance
- Dashboard, product list, low-stock, and report endpoints use DB-projected DTOs (no entity materialization).

### Added
- `Queries/` folder per feature for read-side DTOs.
```

**Dev pushes expected:** ~10.

---

### ETAPA 6 — Test Coverage Maturity

---

#### **Fase 8 — Comprehensive Tests + Coverage Gate** → `v7.3.0`, `v7.3.1`, ...
**Risk:** None (tests only).
**Goal:** Reach ≥75% coverage with meaningful tests, then enforce.

Split into multiple releases; each adds coverage to one area.

**Sub-phase 8.1 — Service layer tests (v7.3.0)**
- 8.1.1 `ProductService`, `CategoryService`, `SupplierService`, `StockMovementService` — all paths.
- 8.1.2 Mock repositories via interfaces; focus on orchestration + invariant enforcement.

**Sub-phase 8.2 — Controller integration tests (v7.3.1)**
- 8.2.1 Using `WebApplicationFactory<Program>` + in-memory SQLite.
- 8.2.2 Cover all MVC controllers' happy paths + auth paths.
- 8.2.3 Cover all API controllers' auth + validation paths.

**Sub-phase 8.3 — Background service tests (v7.3.2)**
- 8.3.1 `OrderSyncBackgroundService` — exponential backoff, failure escalation.
- 8.3.2 `LowStockNotificationService` — sends email when below threshold, respects schedule.
- 8.3.3 `AuditLogCleanupService` — deletes logs older than retention.

**Sub-phase 8.4 — Domain tests (v7.3.3)**
- 8.4.1 All invariants in rich entities and value objects.
- 8.4.2 Domain-only; no mocks, no infrastructure.

**Sub-phase 8.5 — E2E expansion (v7.3.4)**
- 8.5.1 Playwright flows: full CSV import, order sync end-to-end with mocked Nuvemshop API, stock adjustment flow.

**Sub-phase 8.6 — Coverage gate (v7.3.5)**
- 8.6.1 Add CI step: fail if coverage < 75%.
- 8.6.2 Document in `CLAUDE.md` that new code requires tests.

**CHANGELOG (each release):**
```
### Added
- Unit tests for [area].
- [eventually] CI coverage gate at 75%.
```

**Dev pushes expected:** ~25 across all sub-phases.

---

## What's NOT in This Plan

These are deliberate omissions. Open for discussion but not planned:

- **MediatR / explicit CQRS pipeline** — services do the job at this scale.
- **Domain events bus** — useful when we have multiple reactions to a domain change; not yet.
- **Separate Core/Infrastructure projects** — revisit if and when the codebase doubles in size.
- **Caching layer** — measure before optimizing.
- **Multi-tenancy** — no indication the project needs it.
- **Message queue / background worker separation** — overkill for current load.

---

## Adjustment Protocol

If something unexpected surfaces mid-phase (a dependency we missed, a failing test that reveals a deeper bug, etc.):

1. Stop current sub-phase.
2. Either: resolve in-place if trivial (<1 hour), or open a new sub-phase 0.X or current-phase.X as a detour.
3. Update this plan (in the work branch; don't amend it in the repo until the next phase's PR).
4. Never push half-fixed state to `main`.
