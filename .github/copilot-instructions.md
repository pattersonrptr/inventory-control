# Copilot Instructions — Inventory Control

## Project Overview

ASP.NET Core MVC (.NET 10) inventory management system with Entity Framework Core.
Integrates with e-commerce platforms (currently Nuvemshop) for stock synchronization.

## Language & Style

- **All code, comments, commits, and documentation must be in English.**
- Use Conventional Commits: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`, `ci:`, `perf:`.
- Follow C# naming conventions: PascalCase for public members, camelCase for locals/parameters.
- Use `var` when the type is obvious from the right side.
- Prefer LINQ method syntax over query syntax.

## Architecture

- **Repository Pattern**: all data access goes through `Repositories/Interfaces/I*Repository.cs` → `Repositories/*Repository.cs`.
- **Controllers** should be thin — business logic belongs in services or repositories.
- **Views** use Razor with Tag Helpers (no `@Html.Raw` — always encode output).
- **Integration layer** (`Integrations/`) abstracts e-commerce platforms behind `IStoreIntegration`.

## Branching & Versioning

- **GitHub Flow**: `main` + short-lived feature branches (`feat/`, `fix/`, `chore/`, `docs/`).
- **Never push directly to `main`** — always create a branch and open a PR.
- Semantic Versioning (SemVer): `MAJOR.MINOR.PATCH`.
- Keep `CHANGELOG.md` up to date with every notable change.

## Workflow — Before Every Commit

Before committing, always review and update the relevant documentation:

1. **`CHANGELOG.md`** — add new entries under `[Unreleased]` using the appropriate section (`Added`, `Changed`, `Fixed`, `Removed`, `Security`).
2. **`README.md`** — update if the change affects setup, features, architecture, configuration, or project structure.
3. **Other `.md` files** — update any documentation affected by the change (e.g., `Migrations/README.md`).
4. **`appsettings.example.json`** — update if new configuration keys are added.

## Pull Requests

- Every PR must use the template at `.github/PULL_REQUEST_TEMPLATE.md`.
- PR title must follow Conventional Commits format (e.g., `feat: add product export`).
- PR body must include: **What** changed, **Why** it changed, and a **Checklist** of quality gates.
- Link related issues when applicable.

## Releases

Releases follow [Semantic Versioning](https://semver.org/):

- **PATCH** (`x.y.Z`): bug fixes, documentation, minor tweaks — no new features, no breaking changes.
- **MINOR** (`x.Y.0`): new features, backwards-compatible — may include patches.
- **MAJOR** (`X.0.0`): breaking changes — API changes, database schema changes requiring migration, removed features.

### Release Process

1. Ensure `main` is up to date and all PRs are merged.
2. Update `CHANGELOG.md`: move items from `[Unreleased]` into a new `[X.Y.Z] - YYYY-MM-DD` section.
3. Add the comparison link at the bottom of `CHANGELOG.md`.
4. Commit: `chore: prepare release vX.Y.Z`.
5. Create a PR, merge to `main`.
6. Create a GitHub Release via `gh release create vX.Y.Z` with the changelog section as release notes.
7. The release automatically creates the git tag.

## Security

- **Never commit secrets** (API tokens, passwords, connection strings with credentials).
- `appsettings.json` is in `.gitignore` — use `appsettings.example.json` as template.
- Use `dotnet user-secrets` for local development secrets.
- All POST/PUT/DELETE form actions must use `[ValidateAntiForgeryToken]`.
- API endpoints that bypass CSRF (`[IgnoreAntiforgeryToken]`) must have a clear justification comment.

## Database

- Development: SQLite (zero config).
- Production: configurable (Oracle, PostgreSQL, etc.) — swap the EF Core provider in `Program.cs`.
- Always use EF Core with LINQ — never write raw SQL.

## Testing

- Framework: **xUnit**.
- Place tests in a separate `InventoryControl.Tests` project.
- Name test methods: `MethodName_Scenario_ExpectedResult`.

## Dependencies

- Prefer NuGet packages already in the project before adding new ones.
- Keep package versions aligned with the .NET 10 ecosystem.
