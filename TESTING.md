# Testing Guide

## Running Tests

```bash
# All tests (unit + integration; no E2E)
dotnet test --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Playwright"

# Unit tests only
dotnet test --filter "FullyQualifiedName~Unit"

# Integration tests only
dotnet test --filter "FullyQualifiedName~Integration"

# E2E tests (requires Playwright — see setup below)
dotnet test --filter "FullyQualifiedName~E2E"
```

## TDD Workflow

Run tests in watch mode for a fast red-green-refactor loop:

```bash
dotnet watch test --project InventoryControl.Tests \
  --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Playwright"
```

The watcher rebuilds and re-runs on every file save. Keep this running in a terminal while developing.

## Coverage

```bash
dotnet test \
  --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Playwright" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage-results

dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"./coverage-results/**/*.xml" \
  -targetdir:"./coverage-report" \
  -reporttypes:Html
# Open ./coverage-report/index.html in a browser
```

## E2E Setup (first time only)

```bash
pwsh InventoryControl.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

E2E tests require the app to be running. Start it first:

```bash
dotnet run --project InventoryControl/InventoryControl.csproj
```

## Test Naming Convention

```
MethodName_Scenario_ExpectedResult
```

Example: `HandleAuthenticateAsync_ValidApiKey_ReturnsSuccessWithAdminRole`

## Test Organization

| Folder | Purpose | Tools |
|---|---|---|
| `Unit/` | Pure logic, no I/O | xUnit, Moq |
| `Integration/` | Full HTTP stack, in-memory DB | `WebApplicationFactory`, SQLite |
| `E2E/` | Browser-driven flows | Playwright |
