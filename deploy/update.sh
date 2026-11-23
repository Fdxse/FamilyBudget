#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# FamilyBudget - update script that runs ON THE NAS.
#
# 1. Fetches the latest code from GitHub (branch from DEPLOY_BRANCH in .env).
# 2. Builds a new image of the web app.
# 3. Restarts the containers.
# 4. Waits until /health reports Healthy.
#
# Usage on the NAS:   ./deploy/update.sh
# Usage from your PC: see deploy/remote-update.ps1 or deploy/remote-update.sh
# ---------------------------------------------------------------------------
set -euo pipefail

# Work from the repository root, wherever the script is started from.
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

if [[ ! -f .env ]]; then
    echo "Missing .env. Copy .env.example to .env and fill in the values first." >&2
    exit 1
fi

# Read settings from .env (DATA_ROOT, APP_PORT, DEPLOY_BRANCH ...).
set -a
# shellcheck disable=SC1091
source .env
set +a
deploy_branch="${DEPLOY_BRANCH:-main}"
application_port="${APP_PORT:-5080}"

# Docker on Synology requires root. Use sudo when we are not already root.
if [[ "$(id -u)" -eq 0 ]]; then
    sudo_prefix=""
else
    sudo_prefix="sudo"
fi

# Synology ships either "docker compose" (v2 plugin) or the older "docker-compose".
if $sudo_prefix docker compose version >/dev/null 2>&1; then
    compose_command="$sudo_prefix docker compose"
else
    compose_command="$sudo_prefix docker-compose"
fi

echo "==> Fetching latest code (branch: $deploy_branch)"
git fetch --prune origin "$deploy_branch"
# Hard reset so the NAS copy always matches GitHub exactly (no local edits expected).
git checkout --quiet "$deploy_branch" 2>/dev/null || git checkout --quiet -b "$deploy_branch" "origin/$deploy_branch"
git reset --hard "origin/$deploy_branch"
current_commit="$(git rev-parse --short HEAD)"
echo "    Now at commit $current_commit"

echo "==> Making sure data folders exist under $DATA_ROOT"
$sudo_prefix mkdir -p "$DATA_ROOT/mariadb" "$DATA_ROOT/keys"
# The web app runs as the non-root user 1654 from the official .NET image.
$sudo_prefix chown 1654:1654 "$DATA_ROOT/keys"
# MariaDB in its container runs as user 999.
$sudo_prefix chown 999:999 "$DATA_ROOT/mariadb"

# Synology shared folders carry ACLs that are inherited by new folders. The ACLs take
# precedence over normal Unix permissions, so the container users (999 and 1654) get
# "Permission denied" even on rwx folders. Strip the ACLs from our two data folders only.
synology_acl_tool="$(command -v synoacltool || echo /usr/syno/bin/synoacltool)"
if [[ -x "$synology_acl_tool" ]]; then
    for data_folder in "$DATA_ROOT/mariadb" "$DATA_ROOT/keys"; do
        # Ignore errors: a folder without ACL makes the tool exit non-zero.
        $sudo_prefix "$synology_acl_tool" -del "$data_folder" >/dev/null 2>&1 || true
    done
fi

# Removing the ACL leaves the folder with mode 000 (d---------), so always set
# plain Unix permissions afterwards: owner (the container user) may read/write.
$sudo_prefix chmod 750 "$DATA_ROOT/mariadb" "$DATA_ROOT/keys"

# Take a backup before a new version can change the database (migrations run at start-up).
# Skipped on the very first install, when the database container does not exist yet.
# Stops the update if the backup fails; run with SKIP_BACKUP=1 to update anyway.
if [[ "${SKIP_BACKUP:-0}" != "1" ]] && $sudo_prefix docker ps --format '{{.Names}}' | grep -qx familybudget-database; then
    echo "==> Backing up before the update"
    bash "$repository_root/deploy/backup.sh"
fi

echo "==> Building web image"
APP_GIT_COMMIT="$current_commit" $compose_command build --pull web

echo "==> Restarting containers"
APP_GIT_COMMIT="$current_commit" $compose_command up -d --remove-orphans

echo "==> Waiting for the app to become healthy on port $application_port"
health_url="http://localhost:$application_port/health"
maximum_wait_seconds=120
for (( waited_seconds = 0; waited_seconds < maximum_wait_seconds; waited_seconds += 3 )); do
    health_status="$(curl --silent --max-time 3 "$health_url" || true)"
    if [[ "$health_status" == "Healthy" ]]; then
        echo "    Healthy after ${waited_seconds}s. Running commit $current_commit."
        # Remove old, untagged images so the NAS disk does not fill up.
        $sudo_prefix docker image prune --force >/dev/null
        exit 0
    fi
    sleep 3
done

echo "The app did not become healthy within ${maximum_wait_seconds}s. Last web log lines:" >&2
$compose_command logs --tail 50 web >&2
exit 1
