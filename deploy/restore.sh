#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# FamilyBudget - restore a backup made by deploy/backup.sh. Runs ON THE NAS.
#
# REPLACES the whole database with the content of the backup. Everything entered
# after the backup was made is lost. The web app is stopped while restoring.
#
# Usage: sudo bash deploy/restore.sh /volume1/Ekonomi/FamilyBudget/backup/2026-11-23_030000
# ---------------------------------------------------------------------------
set -euo pipefail

backup_folder="${1:-}"
if [[ -z "$backup_folder" || ! -f "$backup_folder/database.sql.gz" ]]; then
    echo "Give the backup folder to restore, e.g.:" >&2
    echo "  sudo bash deploy/restore.sh /volume1/Ekonomi/FamilyBudget/backup/2026-11-23_030000" >&2
    exit 1
fi

# Work from the repository root, wherever the script is started from.
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"
set -a
# shellcheck disable=SC1091
source .env
set +a

if [[ "$(id -u)" -eq 0 ]]; then
    sudo_prefix=""
else
    sudo_prefix="sudo"
fi
if $sudo_prefix docker compose version >/dev/null 2>&1; then
    compose_command="$sudo_prefix docker compose"
else
    compose_command="$sudo_prefix docker-compose"
fi

# The restore cannot be undone, so ask first.
echo "This replaces ALL data in FamilyBudget with the backup in:"
echo "  $backup_folder"
read -r -p "Type 'ja' to continue: " confirmation
if [[ "$confirmation" != "ja" ]]; then
    echo "Cancelled."
    exit 1
fi

echo "==> Stopping the web app so nobody writes during the restore"
$compose_command stop web

echo "==> Restoring the database"
# The dump contains DROP TABLE + CREATE TABLE for every table, so it replaces the current data.
gzip -dc "$backup_folder/database.sql.gz" | $sudo_prefix docker exec -i familybudget-database sh -c \
    'exec mariadb -u root -p"$MARIADB_ROOT_PASSWORD"'

if [[ -f "$backup_folder/keys.tar.gz" ]]; then
    echo "==> Restoring login keys"
    $sudo_prefix tar -xzf "$backup_folder/keys.tar.gz" -C "$DATA_ROOT"
    $sudo_prefix chown -R 1654:1654 "$DATA_ROOT/keys"
fi

echo "==> Starting the web app again"
$compose_command start web
echo "    Restore finished."
