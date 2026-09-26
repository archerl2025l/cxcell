$ErrorActionPreference = "Stop"

$sourceExe = Join-Path $PSScriptRoot "CxCell.exe"
$installDir = Join-Path $env:LOCALAPPDATA "CxCell"
$installedExe = Join-Path $installDir "CxCell.exe"

if (-not (Test-Path $sourceExe)) {
    throw "CxCell.exe was not found next to this installer."
}

Get-Process CxCell,CxCellWatcher -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Force $sourceExe $installedExe

& $installedExe --install-autostart

Write-Host ""
Write-Host "CxCell v0.1.0 installed."
Write-Host "Open Codex/ChatGPT normally; CxCell will follow its lifecycle automatically."
Write-Host "Installed to: $installDir"
