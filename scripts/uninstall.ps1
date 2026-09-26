$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "CxCell"
$exe = Join-Path $installDir "CxCell.exe"

if (Test-Path $exe) {
    & $exe --uninstall-autostart
}

Get-Process CxCell,CxCellWatcher -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

if (Test-Path $installDir) {
    Start-Sleep -Milliseconds 300
    Remove-Item -Recurse -Force $installDir
}

Write-Host "CxCell autostart and installed files were removed."
