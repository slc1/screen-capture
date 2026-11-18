<#
Builds the team package: runs all tests, publishes a self-contained single-file
win-x64 exe, and zips it.

Run (Windows PowerShell 5.1 or pwsh), from any folder:
    pwsh ./publish.ps1
    powershell -ExecutionPolicy Bypass -File .\publish.ps1

Output: artifacts/ScreenCapture-win-x64.zip (artifacts/ is git-ignored).
Stops at the first failing step. The exe is unsigned, so SmartScreen warns on first run.
#>
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }
if (-not (Test-Path -LiteralPath $dotnet)) { throw 'dotnet was not found on PATH or in C:\Program Files\dotnet.' }

function Invoke-Dotnet {
    & $dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE" }
}

Invoke-Dotnet test ScreenCapture.sln -c Release

$publishDir = Join-Path $PSScriptRoot 'artifacts/publish'
$zipPath = Join-Path $PSScriptRoot 'artifacts/ScreenCapture-win-x64.zip'
if (Test-Path -LiteralPath $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot 'artifacts') | Out-Null

Invoke-Dotnet publish src/ScreenCapture.App -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None `
    -o $publishDir

if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force

Write-Host "Created $zipPath ($([math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)) MB)"
