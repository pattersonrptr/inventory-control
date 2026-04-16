# TrueNAS Catalog App — Inventory Control

This directory contains the TrueNAS catalog app definition for **Inventory Control**.
It follows the [TrueNAS Apps Catalog](https://github.com/truenas/apps) structure
and uses the `ix_lib` rendering library.

## Directory Structure

```
ix-dev/community/inventory-control/
├── app.yaml                          # App metadata (version, capabilities, etc.)
├── app_migrations.yaml               # Migration definitions (empty for v1.0.0)
├── ix_values.yaml                    # Static default values (images, constants)
├── questions.yaml                    # UI form schema for TrueNAS configuration
├── README.md                         # Short app description
└── templates/
    ├── docker-compose.yaml           # Jinja2 template (uses ix_lib)
    └── test_values/
        ├── basic-values.yaml         # Basic test scenario
        └── full-values.yaml          # Full scenario (store + offsite backup)
```

## Services

| Container             | Image                                    | Purpose                         |
|-----------------------|------------------------------------------|---------------------------------|
| inventory-control     | pattersonrptr/inventory-control:v6.0.1   | Main ASP.NET Core MVC app       |
| inventory-db          | postgres:16-alpine                       | PostgreSQL database             |
| inventory-backup      | prodrigestivill/postgres-backup-local    | Scheduled DB backups (optional) |
| inventory-offsite     | rclone/rclone                            | Offsite backup sync (optional)  |

## Setup Options

### Option A: Submit to Official TrueNAS Catalog (recommended)

This makes the app available to all TrueNAS users via the Discover tab.

1. Fork `https://github.com/truenas/apps`

2. Copy the app definition into the fork:
   ```bash
   cp -r ix-dev/community/inventory-control /path/to/truenas-apps-fork/ix-dev/community/
   ```

3. Install dependencies and run the CI script to test locally:
   ```bash
   cd /path/to/truenas-apps-fork
   pip install pyyaml psutil pytest pytest-cov bcrypt pydantic

   # Render only (see generated compose file)
   ./.github/scripts/ci.py --app inventory-control --train community \
     --test-file basic-values.yaml --render-only=true

   # Full test (deploys containers, waits for healthy)
   ./.github/scripts/ci.py --app inventory-control --train community \
     --test-file basic-values.yaml --wait=true
   ```

4. Generate metadata:
   ```bash
   ./.github/scripts/generate_metadata.py --app inventory-control --train community
   ```

5. Open a PR to `truenas/apps` targeting the `community` train.

### Option B: Use as Custom Catalog (immediate use)

This lets you install the app on your TrueNAS immediately from a private catalog.

1. Fork `https://github.com/truenas/apps`

2. Copy the app definition:
   ```bash
   cp -r ix-dev/community/inventory-control /path/to/fork/ix-dev/community/
   ```

3. Build the catalog locally using the CI script:
   ```bash
   cd /path/to/fork
   ./.github/scripts/ci.py --app inventory-control --train community \
     --test-file basic-values.yaml --render-only=true
   ```
   This generates the `trains/` directory and copies the library files.

4. Commit and push to your fork.

5. In TrueNAS Web UI:
   - Go to **Apps** → **Discover** → **Manage Catalogs**
   - Click **Add Catalog**
   - Name: `inventory-catalog`
   - Repository: `https://github.com/<your-username>/apps`
   - Branch: `master`
   - Preferred Train: `community`

6. After syncing, **Inventory Control** appears in the Discover tab with full UI
   features (Web UI button, Roll Back, Notes, Edit form, Application Metadata).

## Configuration Groups

The TrueNAS UI form (`questions.yaml`) exposes these configuration groups:

### Inventory Configuration
- **Timezone** — System timezone
- **Admin Email/Password/Full Name** — Default administrator account
- **API Key** — Key for webhook endpoints
- **Database User/Password** — PostgreSQL credentials
- **Store Integration** — Toggle Nuvemshop integration with Store ID, Access Token, and sync interval
- **Backup Settings** — Toggle automatic backups with schedule and retention
- **Offsite Backup** — Toggle rclone offsite sync with remote and interval

### Network Configuration
- **WebUI Port** — Port number, bind mode, and host IPs

### Storage Configuration
- **PostgreSQL Data** — Database storage (ixVolume or Host Path)
- **Backups** — Backup storage (ixVolume or Host Path)
- **Rclone Config** — Rclone configuration storage (ixVolume or Host Path)
- **Additional Storage** — Extra volume mounts

### Labels Configuration
- Docker labels for containers

### Resources Configuration
- CPU and memory limits

## Updating the App

When a new version of Inventory Control is released:

1. Update `app_version` in `app.yaml` to the new upstream version (e.g., `6.1.0`)
2. Update the image tag in `ix_values.yaml` (e.g., `v6.1.0`)
3. Increment `version` in `app.yaml` (e.g., `1.1.0`)
4. If configuration schema changed, add a migration to `app_migrations.yaml`
5. Test with the CI script
6. Commit and push
