param(
    [Parameter(Position = 0)]
    [ValidateSet(
        "start", "stop", "reset", "status", "logs", "smoke", "all-start", "all-stop",
        "cip-start", "cip-stop", "cip-status",
        "opcua-start", "opcua-stop", "opcua-status", "opcua-smoke",
        "iec104-start", "iec104-stop", "iec104-status",
        "dnp3-start", "dnp3-stop", "dnp3-status",
        "s7-start", "s7-stop", "s7-status",
        "bacnet-start", "bacnet-stop", "bacnet-status"
    )]
    [string]$Command = "status"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

$NodeRedPort = if ($env:NODE_RED_PORT) { $env:NODE_RED_PORT } else { "1880" }
$NodeRedUrl = "http://127.0.0.1:$NodeRedPort"

function Invoke-Compose {
    param([string[]]$Files, [string[]]$ComposeArgs, [string]$Label = "lab")
    $fileArgs = @()
    foreach ($file in $Files) { $fileArgs += @("-f", $file) }
    & docker compose @fileArgs @ComposeArgs
    if ($LASTEXITCODE -ne 0) { throw "docker compose $Label failed with exit code $LASTEXITCODE" }
}

$BaseFiles = @("compose.yaml")
$CipFiles = @("compose.yaml", "compose.cip.yaml")
$OpcUaFiles = @("compose.yaml", "compose.opcua.yaml")
$Iec104Files = @("compose.yaml", "compose.iec104.yaml")
$Dnp3Files = @("compose.yaml", "compose.dnp3.yaml")
$S7Files = @("compose.yaml", "compose.s7.yaml")
$BacnetFiles = @("compose.yaml", "compose.bacnet.yaml")
$AllFiles = @("compose.yaml", "compose.cip.yaml", "compose.opcua.yaml", "compose.iec104.yaml", "compose.dnp3.yaml", "compose.s7.yaml", "compose.bacnet.yaml")

function Wait-NodeRed {
    for ($i = 0; $i -lt 60; $i++) {
        try {
            $response = Invoke-RestMethod -Uri "$NodeRedUrl/lab/health" -Method Get -TimeoutSec 2
            if ($response.status -eq "ok") { return }
        } catch { Start-Sleep -Seconds 1 }
    }
    throw "Node-RED lab health endpoint did not become ready."
}

function Invoke-Smoke {
    Wait-NodeRed
    Invoke-RestMethod -Uri "$NodeRedUrl/lab/reset" -Method Post | Out-Null
    $token = "interop-smoke-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())-$PID"
    $body = @{ topic = "elitescada/lab/smoke"; payload = @{ token = $token }; qos = 1; retain = $false } | ConvertTo-Json -Depth 4
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

$WaitArgs = @("up", "-d", "--build", "--wait", "--wait-timeout", "180")

switch ($Command) {
    "start" { Invoke-Compose $BaseFiles $WaitArgs "base lab"; Wait-NodeRed }
    "stop" { Invoke-Compose $BaseFiles @("down", "--remove-orphans") "base lab" }
    "reset" { Invoke-Compose $AllFiles @("down", "-v", "--remove-orphans") "all peers" }
    "status" { Invoke-Compose $AllFiles @("ps") "all peers" }
    "logs" { Invoke-Compose $AllFiles @("logs", "-f") "all peers" }
    "smoke" { Invoke-Smoke }
    "all-start" { Invoke-Compose $AllFiles $WaitArgs "all peers"; Wait-NodeRed }
    "all-stop" { Invoke-Compose $AllFiles @("down", "--remove-orphans") "all peers" }
    "cip-start" { Invoke-Compose $CipFiles ($WaitArgs + @("cip-controllogix", "cip-compactlogix")) "CIP" }
    "cip-stop" { Invoke-Compose $CipFiles @("stop", "cip-controllogix", "cip-compactlogix") "CIP" }
    "cip-status" { Invoke-Compose $CipFiles @("ps", "cip-controllogix", "cip-compactlogix") "CIP" }
    "opcua-start" { Invoke-Compose $OpcUaFiles ($WaitArgs + @("opcua-peer", "node-red")) "OPC UA" }
    "opcua-stop" { Invoke-Compose $OpcUaFiles @("stop", "opcua-peer") "OPC UA" }
    "opcua-status" { Invoke-Compose $OpcUaFiles @("ps", "opcua-peer") "OPC UA" }
    "opcua-smoke" { Invoke-Compose $OpcUaFiles @("exec", "-T", "node-red", "node", "/data/opcua-smoke.js") "OPC UA smoke" }
    "iec104-start" { Invoke-Compose $Iec104Files ($WaitArgs + @("iec104-lib60870")) "IEC-104" }
    "iec104-stop" { Invoke-Compose $Iec104Files @("stop", "iec104-lib60870") "IEC-104" }
    "iec104-status" { Invoke-Compose $Iec104Files @("ps", "iec104-lib60870") "IEC-104" }
    "dnp3-start" { Invoke-Compose $Dnp3Files ($WaitArgs + @("dnp3-dnp3py")) "DNP3" }
    "dnp3-stop" { Invoke-Compose $Dnp3Files @("stop", "dnp3-dnp3py") "DNP3" }
    "dnp3-status" { Invoke-Compose $Dnp3Files @("ps", "dnp3-dnp3py") "DNP3" }
    "s7-start" { Invoke-Compose $S7Files ($WaitArgs + @("s7-python-snap7")) "S7" }
    "s7-stop" { Invoke-Compose $S7Files @("stop", "s7-python-snap7") "S7" }
    "s7-status" { Invoke-Compose $S7Files @("ps", "s7-python-snap7") "S7" }
    "bacnet-start" { Invoke-Compose $BacnetFiles ($WaitArgs + @("bacnet-bacpypes")) "BACnet" }
    "bacnet-stop" { Invoke-Compose $BacnetFiles @("stop", "bacnet-bacpypes") "BACnet" }
    "bacnet-status" { Invoke-Compose $BacnetFiles @("ps", "bacnet-bacpypes") "BACnet" }
}
