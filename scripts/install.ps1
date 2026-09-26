$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\CxCell\CxCell.csproj"
$installDir = Join-Path $env:LOCALAPPDATA "CxCell"
$exe = Join-Path $installDir "CxCell.exe"

Write-Host "Stopping existing CxCell processes..."
Get-Process CxCell -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path $installDir | Out-Null

Write-Host "Publishing CxCell to $installDir ..."
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $installDir

if (-not (Test-Path $exe)) {
    throw "Publish completed but CxCell.exe was not found at $exe"
}

Write-Host "Registering the lightweight CxCell watcher for Windows sign-in..."
& $exe --install-autostart

Write-Host ""
Write-Host "CxCell installed successfully."
Write-Host "The watcher is now running. Open Codex/ChatGPT and the quota batteries will appear automatically."
Write-Host "Installed executable: $exe"
