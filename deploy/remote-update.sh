#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# FamilyBudget - run the update on the NAS over SSH (from Linux/macOS/Git Bash).
#
# Usage:  ./deploy/remote-update.sh [user@nas] [path-on-nas]
# Defaults can also be set with the environment variables NAS_SSH_TARGET and NAS_REPOSITORY_PATH.
# ---------------------------------------------------------------------------
set -euo pipefail

nas_ssh_target="${1:-${NAS_SSH_TARGET:-admin@nas.local}}"
nas_repository_path="${2:-${NAS_REPOSITORY_PATH:-/volume1/docker/familybudget}}"

# -t gives a terminal so sudo on the NAS can ask for a password if needed.
ssh -t "$nas_ssh_target" "cd '$nas_repository_path' && ./deploy/update.sh"
