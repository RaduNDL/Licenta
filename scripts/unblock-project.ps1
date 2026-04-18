param(
    [Parameter(Mandatory = $true)][string]$AppDir,
    [Parameter(Mandatory = $true)][string]$PyDir
)

$ErrorActionPreference = "SilentlyContinue"

function Unblock-Tree {
    param([string]$Root)

    if (-not (Test-Path $Root)) {
        Write-Host "Path not found: $Root"
        return
    }

    Write-Host "Unblocking tree: $Root"

    try {
        Unblock-File -Path $Root
    } catch {
    }

    Get-ChildItem -Path $Root -Recurse -Force -File | ForEach-Object {
        try {
            Unblock-File -Path $_.FullName
        } catch {
        }

        try {
            Remove-Item -LiteralPath "$($_.FullName):Zone.Identifier" -Force
        } catch {
        }
    }

    Get-ChildItem -Path $Root -Recurse -Force -Directory | ForEach-Object {
        try {
            Unblock-File -Path $_.FullName
        } catch {
        }
    }
}

Unblock-Tree -Root $AppDir
Unblock-Tree -Root $PyDir

Write-Host "Unblock completed."
exit 0