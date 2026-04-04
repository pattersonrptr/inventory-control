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

- **GitHub Flow**: `main` + short-lived feature branches (`feat/`, `fix/`, `chore/`).
- Semantic Versioning (SemVer): `MAJOR.MINOR.PATCH`.
- Keep `CHANGELOG.md` up to date with every notable change.

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
- Place tests in a separate `ControleEstoque.Tests` project.
- Name test methods: `MethodName_Scenario_ExpectedResult`.

## Dependencies

- Prefer NuGet packages already in the project before adding new ones.
- Keep package versions aligned with the .NET 10 ecosystem.
