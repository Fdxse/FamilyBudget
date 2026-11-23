#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# FamilyBudget - backup script that runs ON THE NAS.
#
# Saves, in one dated folder under BACKUP_ROOT:
#   - database.sql.gz   a complete dump of the MariaDB database
#   - keys.tar.gz       the keys that protect login cookies
#   - env.txt           a copy of .env (passwords!), needed to restore on a new NAS
# Backups older than BACKUP_KEEP_DAYS days are removed.
#
# Run it daily from DSM: Kontrollpanelen > Aktivitetsschemaläggare (see README).
# Usage:   sudo bash deploy/backup.sh
# Restore: see deploy/restore.sh
# ---------------------------------------------------------------------------
set -euo pipefail

# Work from the repository root, wherever the script is started from.
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

if [[ ! -f .env ]]; then
    echo "Missing .env next to docker-compose.yml." >&2
    exit 1
fi

# Read settings from .env (DATA_ROOT, BACKUP_ROOT, BACKUP_KEEP_DAYS ...).
set -a
# shellcheck disable=SC1091
source .env
set +a
backup_root="${BACKUP_ROOT:-/volume1/Ekonomi/FamilyBudget/backup}"
keep_days="${BACKUP_KEEP_DAYS:-60}"
database_container="familybudget-database"

# Docker on Synology requires root. Use sudo when we are not already root.
if [[ "$(id -u)" -eq 0 ]]; then
    sudo_prefix=""
else
    sudo_prefix="sudo"
fi

timestamp="$(date +%Y-%m-%d_%H%M%S)"
backup_folder="$backup_root/$timestamp"
# Write to a temporary folder first, so a half-finished backup never looks complete.
work_folder="$backup_root/.in-progress-$timestamp"
$sudo_prefix mkdir -p "$work_folder"
# If anything below fails, remove the half-finished backup. After the final move it no longer exists.
trap '$sudo_prefix rm -rf "$work_folder"' EXIT
# Only root may read the backups: they contain all finances and the .env passwords.
$sudo_prefix chmod 700 "$backup_root" "$work_folder"

echo "==> Dumping database to $backup_folder"
# The root password is read inside the container, so it never shows up in the process list.
# --single-transaction gives a consistent snapshot without stopping the app.
$sudo_prefix docker exec "$database_container" sh -c \
    'exec mariadb-dump -u root -p"$MARIADB_ROOT_PASSWORD" --single-transaction --routines --triggers --databases "$MARIADB_DATABASE"' \
    | gzip -9 | $sudo_prefix tee "$work_folder/database.sql.gz" >/dev/null

# A dump that does not end with "Dump completed" was cut off.
if ! $sudo_prefix gzip -dc "$work_folder/database.sql.gz" | tail -n 1 | grep -q "Dump completed"; then
    echo "The database dump looks incomplete; keeping the previous backups untouched." >&2
    exit 1
fi

echo "==> Saving login keys and settings"
$sudo_prefix tar -czf "$work_folder/keys.tar.gz" -C "$DATA_ROOT" keys
$sudo_prefix cp .env "$work_folder/env.txt"
# Name the files explicitly: a wildcard would be expanded by the normal user, who cannot
# look inside the root-only folder, so it would not match anything.
$sudo_prefix chmod 600 "$work_folder/database.sql.gz" "$work_folder/keys.tar.gz" "$work_folder/env.txt"

$sudo_prefix mv "$work_folder" "$backup_folder"
backup_size="$($sudo_prefix du -sh "$backup_folder" | cut -f1)"
echo "    Done: $backup_folder ($backup_size)"

echo "==> Removing backups older than $keep_days days"
# Only dated folders created by this script are touched.
$sudo_prefix find "$backup_root" -mindepth 1 -maxdepth 1 -type d -name '20??-??-??_*' -mtime +"$keep_days" \
    -print -exec rm -rf {} +
# Leftovers from runs that failed halfway.
$sudo_prefix find "$backup_root" -mindepth 1 -maxdepth 1 -type d -name '.in-progress-*' -mtime +1 -exec rm -rf {} +
