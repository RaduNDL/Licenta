param(
    [Parameter(Mandatory = $true)][string]$AppDir,
    [Parameter(Mandatory = $true)][string]$PyDir
)

$ErrorActionPreference = "SilentlyContinue"

function Unblock-Tree {
    param([string]$Root)

    if (-not (Test-Path $Root)) {
        Write-Host "  [SKIP] Path not found: $Root"
        return
    }

    Write-Host "  [UNBLOCK] $Root"

    # Incearca sa deblocheze root-ul
    try { Unblock-File -Path $Root } catch {}

    # Deblocheaza toate fisierele recursiv
    Get-ChildItem -Path $Root -Recurse -Force -File | ForEach-Object {
        try { Unblock-File -Path $_.FullName } catch {}
        try { Remove-Item -LiteralPath "$($_.FullName):Zone.Identifier" -Force } catch {}
    }

    # Deblocheaza directoarele recursiv
    Get-ChildItem -Path $Root -Recurse -Force -Directory | ForEach-Object {
        try { Unblock-File -Path $_.FullName } catch {}
    }
}

Write-Host ""
Write-Host "=== Unblock Project ==="
Write-Host ""

Unblock-Tree -Root $AppDir
Unblock-Tree -Root $PyDir

Write-Host ""
Write-Host "=== Unblock completed ==="
Write-Host ""

exit 0