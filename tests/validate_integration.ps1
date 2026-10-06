<#Requires -Version 5.1>
<#
.SYNOPSIS
  DEV4 integration validator. Runs WITHOUT Unity (CI-friendly).
.DESCRIPTION
  Checks DEV4-owned files as ERRORS and DEV1-3-owned files as WARNINGS
  (parallel devs may not have landed yet). Validates the real contracts:
  backend origin http://localhost:3000 with GET /health at root, canonical
  PlayerPrefs key AR_API_BASE_URL, seed at Assets/Resources/seed_assets.json
  mirroring database/seed.sql. See docs/TEST_PLAN.md.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File ./tests/validate_integration.ps1
#>
param(
  [switch]$Strict,
  [switch]$ProbeApi,
  [string]$ApiOrigin = "http://localhost:3000",
  [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Continue'
$global:Err = 0; $global:Warn = 0

function Fail($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; $global:Err++ }
function Ok($m)   { Write-Host "  [ OK ] $m" -ForegroundColor Green }
function Warn($m) { Write-Host "  [WARN] $m" -ForegroundColor Yellow; $global:Warn++ }
function Need([string]$rel) {
  $p = Join-Path $RepoRoot $rel
  if (Test-Path $p) { Ok $rel } else { Fail "missing required file: $rel" }
  return $p
}
function Want([string]$rel, [string]$owner) {
  $p = Join-Path $RepoRoot $rel
  if (Test-Path $p) { Ok "$rel ($owner)" } else { Warn "missing (owner: $owner): $rel" }
  return $p
}
function ReadText([string]$rel) {
  $p = Join-Path $RepoRoot $rel
  if (Test-Path $p) { return [System.IO.File]::ReadAllText($p) }
  return $null
}

Write-Host "`n== [1/6] DEV4-owned files (errors if missing) =="
$manifest = Need "Packages/manifest.json"
Need "ProjectSettings/ProjectVersion.txt" | Out-Null
Need ".gitignore" | Out-Null
Need ".env.example" | Out-Null
Need "Assets/Scripts/Utils/AppBootstrapper.cs" | Out-Null
Need "Assets/Scripts/Utils/SceneNavigator.cs" | Out-Null
Need "Assets/Tests/IntegrationCheck.cs" | Out-Null
Need "docs/TEST_PLAN.md" | Out-Null
Need "docs/DEPLOYMENT.md" | Out-Null
Need "README.md" | Out-Null
Need "tests/validate_integration.ps1" | Out-Null

Write-Host "`n== [2/6] Packages/manifest.json =="
try {
  $man = Get-Content $manifest -Raw | ConvertFrom-Json
  foreach ($dep in @("com.unity.xr.arfoundation","com.unity.xr.arcore",
                     "com.unity.xr.management","com.unity.nuget.newtonsoft-json",
                     "com.unity.test-framework")) {
    if ($man.dependencies.PSObject.Properties.Name -contains $dep) { Ok "$dep = $($man.dependencies.$dep)" }
    else { Fail "manifest missing dependency: $dep" }
  }
  if ($man.dependencies."com.unity.xr.arfoundation" -notmatch '^5\.0\.') {
    Fail "arfoundation must be 5.0.x for Unity 2022.3 (got $($man.dependencies.'com.unity.xr.arfoundation'))"
  }
} catch { Fail "manifest.json is not valid JSON: $_" }

Write-Host "`n== [3/6] ProjectVersion =="
$pv = ReadText "ProjectSettings/ProjectVersion.txt"
if ($pv -match 'm_EditorVersion:\s*2022\.3\.9f1') { Ok "m_EditorVersion: 2022.3.9f1" }
else { Fail "ProjectVersion.txt must pin 2022.3.9f1" }

Write-Host "`n== [4/6] DEV1-3 contract files (warnings if missing) =="
Want "Assets/Scenes/Boot.unity" "DEV4/DEV3 (boot scene)" | Out-Null
Want "Assets/Scenes/Dashboard.unity" "DEV3" | Out-Null
Want "Assets/Scenes/ARScanner.unity" "DEV1" | Out-Null
Want "Backend/package.json" "DEV2" | Out-Null
Want "Backend/server.js" "DEV2 (entry point)" | Out-Null
Want "Backend/db.js" "DEV2" | Out-Null
Want "Backend/routes/assets.js" "DEV2" | Out-Null
Want "Backend/routes/issues.js" "DEV2" | Out-Null
Want "database/schema.sql" "DEV2" | Out-Null
Want "database/seed.sql" "DEV2" | Out-Null
Want "Assets/Resources/seed_assets.json" "DEV2 (offline seed)" | Out-Null
Want "Assets/Resources/api_config.json" "DEV2 (URL config)" | Out-Null
Want "Assets/Scripts/Core/ApiClient.cs" "DEV2" | Out-Null
Want "Assets/Scripts/Core/ApiConfig.cs" "DEV2" | Out-Null
Want "Assets/Scripts/Core/Models.cs" "DEV2" | Out-Null
Want "Assets/Scripts/Core/LocalSeedProvider.cs" "DEV2" | Out-Null
Want "Assets/Scripts/AR/AssetProvider.cs" "DEV1" | Out-Null
Want "Assets/StreamingAssets/markers/MARK-001.png" "DEV1 (or .jpg)" | Out-Null

Write-Host "`n== [5/6] seed schema: Assets/Resources/seed_assets.json (if present) =="
$seedPath = Join-Path $RepoRoot "Assets/Resources/seed_assets.json"
if (Test-Path $seedPath) {
  try {
    $seed = Get-Content $seedPath -Raw | ConvertFrom-Json
    if (-not $seed.assets -or $seed.assets.Count -eq 0) { Fail "seed has no assets[]" }
    else {
      Ok "seed has $($seed.assets.Count) asset(s)"
      foreach ($a in $seed.assets) {
        foreach ($f in @("id","markerId","name")) {
          if ([string]::IsNullOrWhiteSpace("$($a.$f)")) { Fail "seed asset missing field '$f': $($a | ConvertTo-Json -Compress)" }
        }
      }
      $ids = @($seed.assets | ForEach-Object { "$($_.markerId)" })
      if ($ids -contains "MARK-001") { Ok "seed contains MARK-001 (demo marker)" }
      else { Warn "seed lacks MARK-001 (README demo script uses it)" }
      $dupes = $ids | Group-Object | Where-Object { $_.Count -gt 1 }
      if ($dupes) { Fail ("duplicate markerIds in seed: " + ($dupes.Name -join ", ")) } else { Ok "markerIds unique" }
      $assetIds = @($seed.assets | ForEach-Object { "$($_.id)" })
      $locIds = @($seed.locations | ForEach-Object { "$($_.id)" })
      $badMaint = @($seed.maintenanceRecords | Where-Object { $assetIds -notcontains "$($_.assetId)" })
      if ($badMaint.Count -gt 0) { Fail "$($badMaint.Count) maintenance record(s) reference unknown assetId" } else { Ok "maintenance assetIds resolve" }
      $badIssues = @($seed.issues | Where-Object { $assetIds -notcontains "$($_.assetId)" })
      if ($badIssues.Count -gt 0) { Fail "$($badIssues.Count) issue(s) reference unknown assetId" } else { Ok "issue assetIds resolve" }
      $badStatus = @($seed.issues | Where-Object { @('open','in_progress','resolved','rejected','closed') -notcontains "$($_.status)" })
      if ($badStatus.Count -gt 0) { Fail "issue(s) with invalid status (want open|in_progress|resolved|rejected|closed)" } else { Ok "issue statuses valid" }
      $badLoc = @($seed.assets | Where-Object { $locIds -notcontains "$($_.location)" })
      if ($badLoc.Count -gt 0) { Fail "asset(s) reference unknown location" } else { Ok "asset locations resolve" }
    }
  } catch { Fail "seed_assets.json invalid JSON: $_" }
} else { Warn "seed_assets.json absent (DEV2) - schema check skipped" }

$seedSql = ReadText "database/seed.sql"
if ($seedSql -ne $null) {
  if ($seedSql -match 'MARK-001') { Ok "database/seed.sql contains MARK-001" }
  else { Warn "database/seed.sql lacks MARK-001 (Unity seed has it)" }
}

Write-Host "`n== [6/6] cross-contract consistency =="
$boot = ReadText "Assets/Scripts/Utils/AppBootstrapper.cs"
$cfg = ReadText "Assets/Scripts/Core/ApiConfig.cs"
if ($boot -ne $null -and $cfg -ne $null) {
  $m1 = [regex]::Match($boot, 'ApiUrlPlayerPrefsKey\s*=\s*"([^"]+)"')
  $m2 = [regex]::Match($cfg, 'PlayerPrefsKey\s*=\s*"([^"]+)"')
  if ($m1.Success -and $m2.Success) {
    if ($m1.Groups[1].Value -ceq $m2.Groups[1].Value) { Ok "PlayerPrefs key parity: $($m1.Groups[1].Value)" }
    else { Fail "PlayerPrefs key split-brain: DEV4=$($m1.Groups[1].Value) vs DEV2=$($m2.Groups[1].Value)" }
  } else { Warn "could not parse PlayerPrefs keys (regex miss)" }
  $d1 = [regex]::Match($boot, 'DefaultApiBaseUrl\s*=\s*"([^"]+)"')
  $apiJson = Join-Path $RepoRoot "Assets/Resources/api_config.json"
  if ($d1.Success -and (Test-Path $apiJson)) {
    $base = (Get-Content $apiJson -Raw | ConvertFrom-Json).BaseUrl
    if ("$base".TrimEnd('/') -ceq $d1.Groups[1].Value.TrimEnd('/')) { Ok "default origin parity: $($d1.Groups[1].Value)" }
    else { Warn "default origin differs: AppBootstrapper=$($d1.Groups[1].Value) vs api_config.json=$base" }
  }
  if ($boot -match 'localhost:3001') { Fail "AppBootstrapper still references :3001 (backend is :3000)" }
  else { Ok "no stale :3001 in AppBootstrapper" }
} else { Warn "AppBootstrapper or ApiConfig missing - parity check skipped" }

$server = ReadText "Backend/server.js"
if ($server -ne $null) {
  if ($server -match "app\.get\('/health'") { Ok "GET /health at root (server.js)" }
  else { Fail "server.js lacks GET /health (AppBootstrapper probes {origin}/health)" }
  if ($server -match "process\.env\.PORT.*3000|PORT.*3000") { Ok "backend default port 3000" }
  else { Warn "could not confirm backend port 3000 in server.js" }
}
$rootEnv = ReadText ".env.example"
if ($rootEnv -ne $null) {
  if ($rootEnv -match 'localhost:3001') { Fail ".env.example still references :3001" }
  else { Ok ".env.example port consistent" }
  if ($rootEnv -match 'AR_API_BASE_URL') { Ok ".env.example documents AR_API_BASE_URL" }
  else { Warn ".env.example lacks AR_API_BASE_URL alias" }
}

if ($ProbeApi) {
  Write-Host "`n== [7/6] API probe: $ApiOrigin =="
  foreach ($ep in @("/health","/api/assets","/api/assets/marker/MARK-001","/api/issues?assetId=AST-001")) {
    $url = $ApiOrigin.TrimEnd('/') + $ep
    try {
      $r = Invoke-WebRequest -Uri $url -TimeoutSec 5 -UseBasicParsing
      Ok "GET $url -> $($r.StatusCode)"
    } catch { Warn "GET $url unreachable: $($_.Exception.Message)" }
  }
}

Write-Host "`n---- result: $($global:Err) error(s), $($global:Warn) warning(s) ----"
if ($Strict -and $global:Warn -gt 0) { $global:Err += $global:Warn; $global:Warn = 0 }
if ($global:Err -gt 0) { exit 1 } else { Write-Host "PASS"; exit 0 }
