[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('up', 'reset', 'backend', 'web', 'e2e', 'all', 'down', 'versions')]
    [string]$Command,
    [int]$DbPort = 5432
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$compose = Join-Path $root 'ci\local\docker-compose.yml'
$env:ELITESCADA_CI_DB_PORT = "$DbPort"
$connection = "Host=127.0.0.1;Port=$DbPort;Database={0};Username=postgres;Password=postgres;Pooling=false"

function Compose([string[]]$Arguments) { & docker compose -f $compose @Arguments; if ($LASTEXITCODE) { throw "docker compose failed" } }
function Ensure-Up { Compose @('up', '-d', '--wait') }
function Invoke-Backend {
    Ensure-Up
    $env:ELITESCADA_TEST_POSTGRES = ($connection -f 'elitescada_test')
    $env:ConnectionStrings__EliteScada = $env:ELITESCADA_TEST_POSTGRES
    $env:Historian__Provider = 'timescaledb'
    $env:HistoricalQuery__Enabled = 'true'
    $env:HistoricalQuery__CursorKeyBase64 = 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA='
    Push-Location $root
    try {
        dotnet restore ScadaPlatform.sln
        dotnet build ScadaPlatform.sln --no-restore --configuration Release
        dotnet test ScadaPlatform.sln --no-build --configuration Release --verbosity normal
        $env:ConnectionStrings__EliteScada = ($connection -f 'postgres')
        $env:ELITESCADA_CI_ARTIFACTS = Join-Path $root 'ci\local\artifacts\runtime-smoke'
        python scripts/ci/runtime-smoke.py
    } finally { Pop-Location }
}
function Invoke-Web { Push-Location (Join-Path $root 'web\scada-web'); try { npm ci --no-audit --no-fund; npm run build } finally { Pop-Location } }
function Invoke-E2E {
    Ensure-Up
    $env:ConnectionStrings__EliteScada = ($connection -f 'elitescada_e2e')
    Push-Location (Join-Path $root 'web\scada-web')
    try { npx playwright install chromium; npm run test:e2e } finally { Pop-Location }
}

switch ($Command) {
    'up' { Ensure-Up }
    'reset' { Compose @('down', '--volumes', '--remove-orphans'); Ensure-Up }
    'backend' { Invoke-Backend }
    'web' { Invoke-Web }
    'e2e' { Invoke-E2E }
    'all' { Compose @('down', '--volumes', '--remove-orphans'); Ensure-Up; Invoke-Backend; Invoke-Web; Invoke-E2E }
    'down' { Compose @('down', '--volumes', '--remove-orphans') }
    'versions' {
        Write-Output 'Expected: .NET SDK 10.0.400 (global.json), Node 24.19.0, TimescaleDB 2.29.2-pg18'
        Write-Output 'Effective host versions:'
        dotnet --version; node --version; npm --version; docker version --format '{{.Client.Version}}/{{.Server.Version}}'; docker compose version
    }
}
