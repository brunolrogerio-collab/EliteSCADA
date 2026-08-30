param(
    [Parameter(Position = 0)]
    [ValidateSet("start", "stop", "reset", "status", "logs", "smoke", "cip-start", "cip-stop", "cip-status")]
    [string]$Command = "status"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

$NodeRedPort = if ($env:NODE_RED_PORT) { $env:NODE_RED_PORT } else { "1880" }
$NodeRedUrl = "http://127.0.0.1:$NodeRedPort"

function Invoke-BaseCompose {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    & docker compose -f compose.yaml @Args
    if ($LASTEXITCODE -ne 0) { throw "docker compose failed with exit code $LASTEXITCODE" }
}

function Invoke-CipCompose {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    & docker compose -f compose.yaml -f compose.cip.yaml @Args
    if ($LASTEXITCODE -ne 0) { throw "docker compose CIP overlay failed with exit code $LASTEXITCODE" }
}

function Wait-NodeRed {
    for ($i = 0; $i -lt 60; $i++) {
        try {
            $response = Invoke-RestMethod -Uri "$NodeRedUrl/lab/health" -Method Get -TimeoutSec 2
            if ($response.status -eq "ok") { return }
        } catch {
            Start-Sleep -Seconds 1
        }
    }
    throw "Node-RED lab health endpoint did not become ready."
}

function Invoke-Smoke {
    Wait-NodeRed
    Invoke-RestMethod -Uri "$NodeRedUrl/lab/reset" -Method Post | Out-Null

    $token = "interop-smoke-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())-$PID"
    $body = @{
        topic = "elitescada/lab/smoke"
        payload = @{ token = $token }
        qos = 1
        retain = $false
    } | ConvertTo-Json -Depth 4

    Invoke-RestMethod -Uri "$NodeRedUrl/lab/mqtt/publish" -Method Post -ContentType "application/json" -Body $body | Out-Null

    for ($i = 0; $i -lt 20; $i++) {
        $last = Invoke-RestMethod -Uri "$NodeRedUrl/lab/mqtt/last" -Method Get
        if (($last | ConvertTo-Json -Depth 8) -match [regex]::Escape($token)) {
            Write-Host "Interop lab smoke PASS: $token"
            return
        }
        Start-Sleep -Seconds 1
    }
    throw "Interop lab smoke FAIL: Node-RED did not observe MQTT token $token"
}

switch ($Command) {
    "start" {
        Invoke-BaseCompose up -d --build
        Wait-NodeRed
    }
    "stop" { Invoke-BaseCompose down --remove-orphans }
    "reset" { Invoke-BaseCompose down -v --remove-orphans }
    "status" { Invoke-BaseCompose ps }
    "logs" { Invoke-BaseCompose logs -f }
    "smoke" { Invoke-Smoke }
    "cip-start" { Invoke-CipCompose up -d --build cip-controllogix cip-compactlogix }
    "cip-stop" { Invoke-CipCompose stop cip-controllogix cip-compactlogix }
    "cip-status" { Invoke-CipCompose ps cip-controllogix cip-compactlogix }
}
