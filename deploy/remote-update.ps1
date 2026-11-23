<#
.SYNOPSIS
    FamilyBudget - run the update on the NAS over SSH (from Windows / PowerShell).

.EXAMPLE
    .\deploy\remote-update.ps1 -NasSshTarget fredrik@192.168.1.10
#>
param(
    # SSH user and host of the NAS, e.g. "fredrik@nas.local".
    [string]$NasSshTarget = $(if ($env:NAS_SSH_TARGET) { $env:NAS_SSH_TARGET } else { "admin@nas.local" }),

    # Where the repository is cloned on the NAS.
    [string]$NasRepositoryPath = $(if ($env:NAS_REPOSITORY_PATH) { $env:NAS_REPOSITORY_PATH } else { "/volume1/docker/familybudget" })
)

$ErrorActionPreference = "Stop"

# -t gives a terminal so sudo on the NAS can ask for a password if needed.
ssh -t $NasSshTarget "cd '$NasRepositoryPath' && ./deploy/update.sh"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Update on the NAS failed (exit code $LASTEXITCODE)."
}
