<div align="center">

# Inventory Control

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/aspnet/core/)
[![EF Core](https://img.shields.io/badge/EF%20Core-10.0-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/ef/core/)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![GitHub release](https://img.shields.io/github/v/release/pattersonrptr/inventory-control)](https://github.com/pattersonrptr/inventory-control/releases)

A web-based inventory management system built with **ASP.NET Core MVC (.NET 10)** and **Entity Framework Core**.
Integrates with e-commerce platforms (currently [Nuvemshop](https://www.nuvemshop.com.br/)) for real-time stock synchronization.

</div>

---

## Features

- **Products** — register products with cost/selling prices, current stock, and minimum stock thresholds
- **Categories & Suppliers** — organize products and track supplier information
- **Stock Movements** — record entries (with supplier, unit cost, date) and exits (sale, loss, return)
- **Automatic stock updates** on every movement
- **Low-stock alerts** for products below the minimum threshold
- **Monthly reports** with entry/exit breakdown per product
- **E-commerce sync** — pull products, push stock, and process orders from Nuvemshop

## Tech Stack

| Layer | Technology |
|---|---|
| Web framework | ASP.NET Core MVC (.NET 10) |
| ORM | Entity Framework Core 10 |
| Database (dev) | SQLite |
| Database (prod) | Configurable (PostgreSQL, Oracle, etc.) |
| Front-end | Bootstrap 5.3 + Bootstrap Icons |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Setup

```bash
# Clone the repository
git clone https://github.com/pattersonrptr/inventory-control.git
cd inventory-control

# Copy the example config and fill in your values
cp ControleEstoque/appsettings.example.json ControleEstoque/appsettings.json
# Edit appsettings.json with your settings (connection string, integration tokens, etc.)

# Restore packages
dotnet restore ControleEstoque/ControleEstoque.csproj

# Apply database migrations (creates the SQLite database automatically)
dotnet ef database update --project ControleEstoque/ControleEstoque.csproj

# Run the application
dotnet run --project ControleEstoque/ControleEstoque.csproj
```

Open your browser at **https://localhost:5001** (or the URL shown in the terminal).

### Configuration

Copy `appsettings.example.json` to `appsettings.json` and update the values:

| Key | Description |
|---|---|
| `ConnectionStrings:DefaultConnection` | Database connection string (default: SQLite) |
| `Integration:Enabled` | Enable/disable e-commerce integration |
| `Integration:Platform` | Integration platform (`nuvemshop`) |
| `Integration:StoreId` | Your store ID |
| `Integration:AccessToken` | API access token (keep secret!) |
| `Integration:OrderSyncIntervalMinutes` | Auto-sync orders interval in minutes (default: `15`) |

> **Never commit `appsettings.json`** — it is listed in `.gitignore`. Use `appsettings.example.json` as a template.
>
> **Docker / TrueNAS**: use environment variables with `__` instead of `:` (e.g. `Integration__OrderSyncIntervalMinutes=15`).

### Docker

```bash
# Build and run with Docker Compose
docker compose up -d

# The app will be available at http://localhost:8080

# Stop the container
docker compose down
```

The SQLite database is persisted in a Docker volume (`app-data`), so data survives container restarts.

## Architecture

```
Controllers  →  Repositories (interfaces)  →  AppDbContext  →  Database
     ↑                                              ↑
   Views (Razor)                              Integrations (Nuvemshop)
```

- **Repository Pattern** — all data access through `Repositories/Interfaces/I*Repository.cs`
- **Thin controllers** — business logic lives in services and repositories
- **Integration layer** — `Integrations/` abstracts e-commerce platforms behind `IStoreIntegration`

Switching database providers requires changes in only two places:
1. `appsettings.json` → connection string
2. `Program.cs` → swap `UseSqlite(...)` with the desired provider (e.g., `UseNpgsql(...)`)

## Project Structure

```
inventory-control/
├── ControleEstoque/
│   ├── BackgroundServices/    # Hosted services (order sync polling)
│   ├── Controllers/           # MVC + API controllers
│   ├── Data/                  # AppDbContext and EF Core config
│   ├── Integrations/          # E-commerce platform abstractions + Nuvemshop
│   ├── Migrations/            # EF Core migrations
│   ├── Models/                # Domain entities (Product, Category, Supplier, StockMovement)
│   ├── Repositories/          # Data access layer (interfaces + implementations)
│   ├── ViewModels/            # View-specific models
│   ├── Views/                 # Razor views
│   └── wwwroot/               # Static assets (CSS, JS)
├── CHANGELOG.md
├── LICENSE
└── README.md
```

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feat/my-feature`
3. Commit using [Conventional Commits](https://www.conventionalcommits.org/): `git commit -m "feat: add new feature"`
4. Push and open a Pull Request

## License

This project is licensed under the **GNU General Public License v3.0** — see [LICENSE](LICENSE) for details.
