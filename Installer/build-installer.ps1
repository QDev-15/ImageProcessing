# Builds the MSI: self-contained win-x64 publish, then the WiX package.
# Usage (from repo root or this folder):  powershell -ExecutionPolicy Bypass -File Installer\build-installer.ps1 [-Version 1.0.0]
param([string]$Version = "1.0.0")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "artifacts\publish\"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet publish (Join-Path $root "Source\ImageOptimizerTool\ImageOptimizerTool.csproj") `
    -c Release -r win-x64 --self-contained true -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

dotnet build (Join-Path $PSScriptRoot "ImageOptimizerTool.Installer.wixproj") -c Release `
    -p:PublishDir=$publish -p:ProductVersion=$Version -o (Join-Path $root "artifacts\installer")
if ($LASTEXITCODE -ne 0) { throw "WiX build failed" }

Get-ChildItem (Join-Path $root "artifacts\installer\*.msi") | ForEach-Object { "{0}  {1:N1} MB" -f $_.FullName, ($_.Length / 1MB) }
