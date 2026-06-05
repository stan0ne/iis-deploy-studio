Param(
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64",
  [string]$Output = "release",
  [string]$Version = "1.1.5"
)

$ErrorActionPreference = 'Stop'

Write-Host "==> Building solution..." -ForegroundColor Cyan
dotnet build IISDeployStudio.slnx -c $Configuration

Write-Host "==> Publishing UI to $Output..." -ForegroundColor Cyan
dotnet publish src/UI/IISDeploy.UI.csproj -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -o $Output

Write-Host "==> Generating build manifest..." -ForegroundColor Cyan
$exePath = Join-Path $Output "IISDeployStudio.exe"
$exeInfo = Get-Item $exePath -ErrorAction SilentlyContinue

$gitHash = (& git rev-parse HEAD 2>$null)
if (-not $gitHash) { $gitHash = "unknown" }
$gitBranch = (& git rev-parse --abbrev-ref HEAD 2>$null)
if (-not $gitBranch) { $gitBranch = "unknown" }

$dotnetVer = (& dotnet --version 2>$null)
if (-not $dotnetVer) { $dotnetVer = "unknown" }

$manifest = [ordered]@{
    version       = $Version
    gitHash       = $gitHash.Trim()
    gitBranch     = $gitBranch.Trim()
    buildTime     = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    dotnetVersion = $dotnetVer.Trim()
    runtime       = $Runtime
    selfContained = $true
    singleFile    = $true
    exePath       = "IISDeployStudio.exe"
    exeSize       = if ($exeInfo) { [int64]$exeInfo.Length } else { 0 }
    publishedBy   = $env:USERNAME
}

$manifestPath = Join-Path $Output "build.manifest.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8

Write-Host "==> Release output ready at ./$Output" -ForegroundColor Green
Write-Host "==> Build manifest: $manifestPath" -ForegroundColor Green
