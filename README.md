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
- **Profitability report** — cost vs selling price analysis per product with margins
- **Interactive dashboard** — Chart.js charts for movements/month, top sellers, and stock by category
- **E-commerce sync** — pull products, push stock, and process orders from Nuvemshop
- **Email notifications** — configurable SMTP alerts when products drop below minimum stock
- **CSV import** — bulk import Products, Categories, and Suppliers via CSV files (Admin only)
- **Product images** — upload images for products, displayed on details and edit pages
- **REST API** — full CRUD API at `/api/v1/` for Products, Categories, and Suppliers with Swagger UI
- **API key authentication** — secure API access via `X-Api-Key` header
- **Authentication** — ASP.NET Core Identity with email/password login and role-based access (Admin / Operator)
- **User management** — admins can create, edit, and delete users and assign roles
- **Audit trail** — automatic logging of every data change (who, what, when, old/new values)

## Tech Stack

| Layer | Technology |
|---|---|
| Web framework | ASP.NET Core MVC (.NET 10) |
| ORM | Entity Framework Core 10 |
| Authentication | ASP.NET Core Identity |
| Database (dev) | SQLite |
| Database (prod) | Configurable (PostgreSQL, Oracle, etc.) |
| Front-end | Bootstrap 5.3 + Bootstrap Icons + Chart.js 4 |
| API docs | Swagger UI (Swashbuckle) |

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

A default admin user is created on first run using the `DefaultAdmin` settings in `appsettings.json`.

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
| `DefaultAdmin:Email` | Default admin email (seeded on first run) |
| `DefaultAdmin:Password` | Default admin password (min 6 characters) |
| `DefaultAdmin:FullName` | Default admin display name |
| `EmailNotifications:Enabled` | Enable/disable low stock email alerts |
| `EmailNotifications:SmtpHost` | SMTP server hostname |
| `EmailNotifications:SmtpPort` | SMTP server port (default: `587`) |
| `EmailNotifications:SmtpUser` | SMTP authentication username |
| `EmailNotifications:SmtpPassword` | SMTP authentication password (keep secret!) |
| `EmailNotifications:FromEmail` | Sender email address |
| `EmailNotifications:ToEmail` | Recipient email address for alerts |
| `EmailNotifications:EnableSsl` | Use SSL/TLS for SMTP (default: `true`) |
| `EmailNotifications:CheckIntervalHours` | How often to check for low stock (default: `24`) |
| `Api:Key` | API key for REST API authentication (used via `X-Api-Key` header) |

> **Never commit `appsettings.json`** — it is listed in `.gitignore`. Use `appsettings.example.json` as a template.
>
> **Docker / TrueNAS**: use environment variables with `__` instead of `:` (e.g. `Integration__OrderSyncIntervalMinutes=15`).

### Docker

```bash
# Build and run with Docker Compose (app + db + local backup)
docker compose up -d

# The app will be available at http://localhost:8080
# Health check: http://localhost:8080/health

# With offsite backup to Google Drive (requires rclone config):
docker compose --profile offsite up -d

# Stop all containers
docker compose down
```

| Container | Purpose | Always runs? |
|---|---|---|
| `db` | PostgreSQL 16 | Yes |
| `app` | Inventory Control | Yes |
| `backup` | pg_dump every 12h (7 daily + 4 weekly retention) | Yes |
| `offsite-backup` | rclone sync to Google Drive | Only with `--profile offsite` |

Environment variables (use `__` instead of `:` for nested keys):

| Variable | Description | Default |
|---|---|---|
| `DB_PASSWORD` | PostgreSQL password | `$Caramelo123` |
| `RCLONE_REMOTE` | rclone remote destination | `gdrive:inventory-control-backups` |
| `SYNC_INTERVAL` | Offsite sync interval | `12h` |

### TrueNAS SCALE

**Deployment via "Install via YAML":**

1. Copy `docker-compose.truenas.example.yml` and replace placeholders:
   - `<YOUR_DB_PASSWORD>` — secure password (no special shell chars: avoid `$`, use `#` or alphanumeric)
   - `<YOUR_NUVEMSHOP_STORE_ID>` — from Nuvemshop admin
   - `<YOUR_NUVEMSHOP_ACCESS_TOKEN>` — from Nuvemshop API credentials

2. In TrueNAS UI → Apps → Discover → Custom App → **Install via YAML**:
   - **Name**: `inventory-control`
   - **Custom Config**: paste the edited YAML

3. Create ZFS directories first (via TrueNAS Shell):
   ```bash
   mkdir -p /mnt/Storage/apps/inventory_control/db-data
   mkdir -p /mnt/Storage/apps/inventory_control/backups
   ```

**Features:**
- Uses host paths on ZFS (`/mnt/Storage/apps/inventory_control/`) for snapshots/backups
- Port `9080` → app port `8080` (avoids conflicts with other TrueNAS apps)
- All 3 services: PostgreSQL, app, local backup
- Offsite backup (rclone) disabled by default; add as separate app when needed

**Access:** `http://<truenas-ip>:9080`

### Backup & Restore

**Local backups** run automatically every 12 hours via `prodrigestivill/postgres-backup-local`. Backups are stored in the `backups` Docker volume as compressed `.sql.gz` files.

```bash
# List available backups
docker compose exec backup ls -la /backups/caramelo_inventory

# Trigger a manual backup
docker compose exec backup /backup.sh

# Restore from a backup (replace filename with the desired backup)
docker compose exec -T db psql -U caramelo -d caramelo_inventory < backup.sql
```

**Offsite backups** (on TrueNAS) sync daily to Google Drive via rclone:

1. **Set up rclone config** (one-time only):
   ```bash
   docker run --rm -it -v /mnt/Storage/apps/inventory_control/rclone-config:/config/rclone rclone/rclone config
   
   # Follow the wizard:
   # - "Create new remote" → y
   # - Remote name → gdrive
   # - Storage type → Google Drive (number option)
   # - Client ID → leave blank (uses default)
   # - OAuth authentication → opens browser for authorization
   ```

2. **Restart the app** — offsite-backup container auto-syncs `/backups` to Google Drive every 12 hours

For **local development** (docker compose): rclone is optional. Add manually if needed.

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
│   ├── Data/                  # AppDbContext, EF Core config, audit interceptor
│   ├── Integrations/          # E-commerce platform abstractions + Nuvemshop
│   ├── Migrations/            # EF Core migrations
│   ├── Models/                # Domain entities + Identity user + audit log
│   ├── Repositories/          # Data access layer (interfaces + implementations)
│   ├── ViewModels/            # View-specific models (reports, auth, user management)
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
