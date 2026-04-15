# Plan: Unified TrueNAS App + Database Backup

## Context

- **Current**: App container on Docker Hub + separate Postgres Custom App on TrueNAS
- **Goal**: Single Custom App with `app` + `db` + `backup` + optional `offsite-backup` (Google Drive)
- TrueNAS has 2× 1TB mirrored disks (ZFS)
- Current DB has only test data — safe to start fresh
- No Kubernetes needed (TrueNAS SCALE removed k3s; Custom Apps = Docker Compose)

## Architecture: 4 Containers

| # | Service           | Image                                     | Purpose                              | Always runs? |
|---|-------------------|-------------------------------------------|--------------------------------------|--------------|
| 1 | `db`              | `postgres:16-alpine`                      | PostgreSQL database                  | Yes          |
| 2 | `app`             | `pattersonrptr/inventory-control:latest`  | ASP.NET Core MVC app                 | Yes          |
| 3 | `backup`          | `prodrigestivill/postgres-backup-local`   | pg_dump every 12h, local retention   | Yes          |
| 4 | `offsite-backup`  | `rclone/rclone`                           | Sync backups to Google Drive         | **No** — opt-in via `--profile offsite` |

### How to run

```bash
# Default (3 containers — no Google Drive config needed):
docker compose up -d

# With offsite backup (4 containers — requires rclone.conf):
docker compose --profile offsite up -d
```

## Phases

### Phase 1: Health Check Endpoint (app code)

1. Add `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` package to `ControleEstoque.csproj`
2. Register health checks in `Program.cs`:
   - `builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();`
3. Map health endpoint in `Program.cs`:
   - `app.MapHealthChecks("/health");`

**Files**: `Program.cs`, `ControleEstoque.csproj`

### Phase 2: Backup Container (docker-compose)

4. Add `backup` service to `docker-compose.yml`:
   - Image: `prodrigestivill/postgres-backup-local`
   - Schedule: `0 */12 * * *` (every 12 hours)
   - Retention: 7 daily, 4 weekly, 0 monthly
   - Volume: `backups:/backups`
   - Depends on `db` (service_healthy)
5. Add `backups` named volume

**Files**: `docker-compose.yml`

### Phase 3: Offsite Backup (optional, opt-in)

6. Add `offsite-backup` service with `profiles: ["offsite"]`
   - Image: `rclone/rclone`
   - Mounts `backups` volume (read-only) + `rclone-config` volume
   - Runs `rclone sync` on a cron loop
   - Only starts with `docker compose --profile offsite up -d`
   - Without the profile flag: doesn't exist, no errors, no config needed
7. Add `rclone-config` named volume

**Files**: `docker-compose.yml`

### Phase 4: Docker Compose + Documentation

8. Add `healthcheck` to `app` service in `docker-compose.yml`
9. Update `README.md` — backup/restore instructions, rclone setup guide
10. Update `CHANGELOG.md`
11. Commit, PR, merge

**Files**: `docker-compose.yml`, `README.md`, `CHANGELOG.md`

## Verification

1. `docker compose up -d` → 3 containers start, `docker compose ps` shows healthy
2. `curl http://localhost:8080/health` → `Healthy`
3. Trigger manual backup → `.sql.gz` file appears in backup volume
4. Test restore: `gunzip < backup.sql.gz | psql` → data intact
5. (When ready) Configure rclone → `docker compose --profile offsite up -d` → files sync to Google Drive

## Decisions

- **No Kubernetes** — TrueNAS SCALE removed k3s; Docker Compose is native
- **No DB streaming replication** — overkill for single-server home setup
- **No data migration** — current DB is test data only
- **`prodrigestivill/postgres-backup-local`** — battle-tested, handles retention automatically
- **Offsite is opt-in** — app works fully without Google Drive configured
- After verifying unified app → delete standalone Postgres Custom App on TrueNAS
