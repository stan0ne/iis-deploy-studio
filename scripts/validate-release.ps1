<#
.SYNOPSIS
    IISDeploy Studio release validation gate.

.DESCRIPTION
    Runs the three release gates (build, test, publish artifact) and a structural
    checklist. Exits 0 only if every check passes. Intended to be the final
    pre-release gate alongside the manual checklist in docs/RELEASE_SMOKE_TEST.md.

.PARAMETER SkipPublish
    Skip the publish-artifact check. Useful for local quick-validation.

.EXAMPLE
    pwsh -File .\scripts\validate-release.ps1
    pwsh -File .\scripts\validate-release.ps1 -SkipPublish
#>
[CmdletBinding()]
param(
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][bool]$Passed,
        [string]$Detail = ''
    )
    $script:results.Add([pscustomobject]@{
        Name   = $Name
        Passed = $Passed
        Detail = $Detail
    })
    $icon = if ($Passed) { 'PASS' } else { 'FAIL' }
    Write-Host ("[{0}] {1} {2}" -f $icon, $Name, $Detail)
}

# 1. .NET SDK presence
$sdkPresent = $false
try {
    $null = & dotnet --version 2>&1
    if ($LASTEXITCODE -eq 0) { $sdkPresent = $true }
} catch { }
Add-Result -Name 'dotnet SDK available' -Passed $sdkPresent `
    -Detail ($(if ($sdkPresent) { "(v$(& dotnet --version))" } else { '' }))

# 2. Required file presence
$required = @(
    'IISDeployStudio.slnx',
    'README.md',
    'PROMPT.md',
    'CHANGELOG.md',
    'ROADMAP.md',
    'RELEASE_READINESS_PLAN.md',
    'scripts/publish-release.ps1',
    'src/UI/IISDeploy.UI.csproj',
    'tests/IISDeploy.Tests/IISDeploy.Tests.csproj'
)
$missing = $required | Where-Object { -not (Test-Path (Join-Path $repoRoot $_)) }
Add-Result -Name 'Required files present' -Passed ($missing.Count -eq 0) `
    -Detail ($(if ($missing.Count -gt 0) { "Missing: $($missing -join ', ')" } else { '' }))

# 3. Repo hygiene: no error.log, no inspect_tool leftover
$badArtifacts = @('error.log') | Where-Object { Test-Path (Join-Path $repoRoot $_) }
$badDirs = @('inspect_tool') | Where-Object { Test-Path (Join-Path $repoRoot $_) }
Add-Result -Name 'Repo hygiene' `
    -Passed (($badArtifacts.Count -eq 0) -and ($badDirs.Count -eq 0)) `
    -Detail ($(if ($badArtifacts.Count -gt 0 -or $badDirs.Count -gt 0) {
        ("Leftover: " + (($badArtifacts + $badDirs) -join ', '))
    } else { '' }))

# 4. Build (Debug — the gating configuration)
Write-Host '--- dotnet build (Debug) ---'
$buildLog = & dotnet build IISDeployStudio.slnx -c Debug --nologo 2>&1
$buildExit = $LASTEXITCODE
$buildOk = ($buildExit -eq 0) -and ($buildLog -notmatch 'warning CS\d+') -and ($buildLog -notmatch 'error CS\d+')
Add-Result -Name 'dotnet build (Debug) clean' -Passed $buildOk `
    -Detail ("exit=$buildExit")

# 5. Test
Write-Host '--- dotnet test ---'
$testLogPath = Join-Path $env:TEMP ("iisdeploy-validate-test-{0}.log" -f [Guid]::NewGuid().ToString('N'))
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    & dotnet test tests/IISDeploy.Tests/IISDeploy.Tests.csproj -c Debug --nologo --no-build *> $testLogPath
    $testExit = $LASTEXITCODE
    $testLog = Get-Content $testLogPath -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
} finally {
    Remove-Item $testLogPath -ErrorAction SilentlyContinue
}
$testOk = ($testExit -eq 0) -and [bool]($testLog -match 'Toplam: \d+|Total tests: \d+|Total: \d+|Passed:|Başarılı:')
$testSummaryText = ''
if ($testLog) {
    $summaryLine = $testLog -split "`n" | Select-String -Pattern 'Toplam: \d|Total tests: \d|Total: \d|Passed:|Başarılı:' | Select-Object -Last 1
    if ($summaryLine) { $testSummaryText = $summaryLine.ToString().Trim() }
}
if (-not $testSummaryText) { $testSummaryText = "exit=$testExit" }
Add-Result -Name 'dotnet test all green' -Passed $testOk -Detail $testSummaryText

# 6. Publish artifact
if (-not $SkipPublish) {
    Write-Host '--- dotnet publish (Release single-file) ---'
    $publishLog = & dotnet publish src/UI/IISDeploy.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release --nologo 2>&1
    $publishExit = $LASTEXITCODE
    $exe = Join-Path $repoRoot 'release/IISDeployStudio.exe'
    $exePresent = (Test-Path $exe) -and ((Get-Item $exe).Length -gt 1MB)
    Add-Result -Name 'Publish single-file artifact' `
        -Passed ($publishExit -eq 0 -and $exePresent) `
        -Detail ("exit=$publishExit, exe=" + (Test-Path $exe))

    if ($publishExit -eq 0) {
        Write-Host '--- generating release/build.manifest.json ---'
        $exeInfo = Get-Item $exe -ErrorAction SilentlyContinue
        $gitHash = (& git rev-parse HEAD 2>$null)
        if (-not $gitHash) { $gitHash = 'unknown' }
        $gitBranch = (& git rev-parse --abbrev-ref HEAD 2>$null)
        if (-not $gitBranch) { $gitBranch = 'unknown' }
        $dotnetVer = (& dotnet --version 2>$null)
        if (-not $dotnetVer) { $dotnetVer = 'unknown' }
        $manifestObj = [ordered]@{
            version       = '1.1.5'
            gitHash       = $gitHash.Trim()
            gitBranch     = $gitBranch.Trim()
            buildTime     = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
            dotnetVersion = $dotnetVer.Trim()
            runtime       = 'win-x64'
            selfContained = $true
            singleFile    = $true
            exePath       = 'IISDeployStudio.exe'
            exeSize       = if ($exeInfo) { [int64]$exeInfo.Length } else { 0 }
            publishedBy   = $env:USERNAME
        }
        $manifestPath = Join-Path $repoRoot 'release/build.manifest.json'
        $manifestObj | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8
    }
} else {
    Add-Result -Name 'Publish artifact' -Passed $true -Detail '(skipped)'
}

# 6b. Build manifest present + valid
$manifestPath = Join-Path $repoRoot 'release/build.manifest.json'
$manifestOk = $false
$manifestDetail = ''
if (Test-Path $manifestPath) {
    try {
        $manifestJson = Get-Content $manifestPath -Raw -Encoding UTF8 -ErrorAction Stop
        $manifest = $manifestJson | ConvertFrom-Json -ErrorAction Stop
        $required = @('version', 'gitHash', 'buildTime', 'runtime', 'exeSize')
        $missing = $required | Where-Object { -not ($manifest.PSObject.Properties.Name -contains $_) }
        if ($missing.Count -eq 0 -and $manifest.exeSize -gt 0) {
            $manifestOk = $true
            $manifestDetail = "version=$($manifest.version), exeSize=$($manifest.exeSize) B, git=$($manifest.gitHash.Substring(0, [Math]::Min(7, $manifest.gitHash.Length)))"
        } elseif ($missing.Count -gt 0) {
            $manifestDetail = "Missing fields: $($missing -join ', ')"
        } else {
            $manifestDetail = 'exeSize=0 (publisher missing?)'
        }
    } catch {
        $manifestDetail = "Invalid JSON: $($_.Exception.Message)"
    }
} else {
    $manifestDetail = 'release/build.manifest.json not found (run scripts/publish-release.ps1 or rerun validate without -SkipPublish)'
}
Add-Result -Name 'Build manifest (release/build.manifest.json)' -Passed $manifestOk -Detail $manifestDetail

# 7. .NET 10 / WPF consistency in PROMPT.md (current stack line is authoritative)
$prompt = Get-Content (Join-Path $repoRoot 'PROMPT.md') -Raw -ErrorAction SilentlyContinue
$currentStackLine = if ($prompt) {
    ($prompt -split "`n" | Select-String -Pattern 'Güncel teknoloji yığını' | Select-Object -First 1)
} else { $null }
$stackText = if ($currentStackLine) { $currentStackLine.ToString() } else { '' }
$promptOk = $prompt -and ($stackText -match '\.NET 10') -and ($stackText -match '\bWPF\b')
Add-Result -Name 'PROMPT.md baseline (.NET 10 + WPF as current stack)' -Passed $promptOk `
    -Detail ($(if (-not $promptOk) { "current-stack line missing required tokens" } else { '' }))

# Summary
Write-Host ''
Write-Host '==== Release Validation Summary ===='
$results | Format-Table -AutoSize Name, Passed, Detail
$failed = $results | Where-Object { -not $_.Passed }
if ($failed.Count -gt 0) {
    Write-Host ("RELEASE NOT READY — {0} check(s) failed." -f $failed.Count) -ForegroundColor Red
    exit 1
}
Write-Host 'RELEASE READY — all checks passed.' -ForegroundColor Green
exit 0
