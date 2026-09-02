[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProductRoot,
    [string]$BaseUrl = "http://127.0.0.1:5093"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Assert-ReleaseCondition {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) { throw $Message }
}

$root = (Resolve-Path -LiteralPath $ProductRoot).Path
$productExe = Join-Path $root "Scada.Api.exe"
$webIndex = Join-Path $root "wwwroot/index.html"
$pyodideEntry = Join-Path $root "wwwroot/pyodide/pyodide.js"

foreach ($requiredPath in @($productExe, $webIndex, $pyodideEntry)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required packaged-product file is missing: $requiredPath"
    }
}

$temporaryRoot = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    [IO.Path]::GetTempPath()
}
else {
    $env:RUNNER_TEMP
}
$stdoutPath = Join-Path $temporaryRoot "wave13-product-host.stdout.log"
$stderrPath = Join-Path $temporaryRoot "wave13-product-host.stderr.log"
$packagePath = Join-Path $temporaryRoot "wave13-product-roundtrip.escadapkg"
$process = Start-Process `
    -FilePath $productExe `
    -ArgumentList '--urls', $BaseUrl `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -PassThru

try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri "$BaseUrl/health" -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                $ready = $true
                break
            }
        }
        catch {
            if ($process.HasExited) { break }
            Start-Sleep -Seconds 1
        }
    }

    if (-not $ready) {
        $exitDiagnostic = if ($process.HasExited) { [string]$process.ExitCode } else { "still running" }
        throw "Packaged product host did not become healthy. Process state: $exitDiagnostic."
    }

    $web = Invoke-WebRequest -Uri "$BaseUrl/" -UseBasicParsing -TimeoutSec 5
    Assert-ReleaseCondition ($web.StatusCode -eq 200 -and $web.Content -match '<div id="root"') `
        "Packaged Web UI was not served by the product host."
    Assert-ReleaseCondition ([string]$web.Headers['Cross-Origin-Opener-Policy'] -eq 'same-origin') `
        "Packaged Web UI is missing Cross-Origin-Opener-Policy: same-origin."
    Assert-ReleaseCondition ([string]$web.Headers['Cross-Origin-Embedder-Policy'] -eq 'require-corp') `
        "Packaged Web UI is missing Cross-Origin-Embedder-Policy: require-corp."

    $pyodide = Invoke-WebRequest -Uri "$BaseUrl/pyodide/pyodide.js" -UseBasicParsing -TimeoutSec 5
    Assert-ReleaseCondition ($pyodide.StatusCode -eq 200 -and $pyodide.Content.Length -gt 1000) `
        "Packaged Pyodide runtime was not served correctly."

    $missingApiStatus = $null
    try {
        Invoke-WebRequest -Uri "$BaseUrl/api/wave13-definitely-missing" -UseBasicParsing -TimeoutSec 5 | Out-Null
    }
    catch {
        $missingApiStatus = [int]$_.Exception.Response.StatusCode
    }
    Assert-ReleaseCondition ($missingApiStatus -eq 404) `
        "Unknown /api route was incorrectly handled by the SPA fallback."

    $authConfig = Invoke-RestMethod -Uri "$BaseUrl/api/auth/config" -TimeoutSec 5
    Assert-ReleaseCondition ($authConfig.authenticationEnabled -eq $true -and $authConfig.localLoginEnabled -eq $true) `
        "Packaged local-authentication configuration is not enabled for the release smoke."

    $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $loginBody = @{ username = 'wave13-admin'; password = 'Wave13-CI-Only-Password!2026' } | ConvertTo-Json
    $login = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/auth/login" `
        -WebSession $session `
        -ContentType 'application/json' `
        -Body $loginBody `
        -TimeoutSec 10
    Assert-ReleaseCondition ($login.username -eq 'wave13-admin' -and (@($login.roles) -contains 'developer')) `
        "Packaged local login did not return the expected developer identity."

    $profile = Invoke-RestMethod -Uri "$BaseUrl/api/auth/me" -WebSession $session -TimeoutSec 5
    Assert-ReleaseCondition ($profile.displayName -eq 'Wave 13 CI Administrator') `
        "Packaged authenticated profile did not preserve the local identity."

    $licensing = Invoke-RestMethod -Uri "$BaseUrl/api/licensing/status" -WebSession $session -TimeoutSec 5
    Assert-ReleaseCondition ($licensing.license.state -eq 'Demo' -and $licensing.license.maximumTags -eq 200) `
        "Packaged product did not start in the accepted 200-TAG Demo mode."
    Assert-ReleaseCondition ($licensing.license.demoMaximumContinuousMinutes -eq 300) `
        "Packaged product did not expose the accepted 300-minute Demo session contract."

    $machineRequest = Invoke-RestMethod -Uri "$BaseUrl/api/licensing/request" -WebSession $session -TimeoutSec 5
    Assert-ReleaseCondition ([string]$machineRequest.requestCode -like 'ESREQ1.*') `
        "Packaged product did not expose a versioned machine request code."

    $dynamos = @(Invoke-RestMethod -Uri "$BaseUrl/api/engineering/dynamos" -WebSession $session -TimeoutSec 5)
    $expectedDynamos = @(
        'dynamo.pump.standard',
        'process.motor.standard',
        'process.motor.vfd',
        'process.pump.submersible',
        'process.tank.horizontal',
        'process.tank.vertical',
        'process.valve.control',
        'process.valve.onoff'
    )
    $actualDynamos = @($dynamos | ForEach-Object { [string]$_.key } | Sort-Object)
    Assert-ReleaseCondition ($actualDynamos.Count -eq $expectedDynamos.Count) `
        "Packaged product did not expose all eight built-in Dynamos."
    Assert-ReleaseCondition (($actualDynamos -join '|') -eq (($expectedDynamos | Sort-Object) -join '|')) `
        "Packaged built-in Dynamo identities differ from the accepted library."

    $screens = @(Invoke-RestMethod -Uri "$BaseUrl/api/engineering/screens" -WebSession $session -TimeoutSec 5)
    Assert-ReleaseCondition (@($screens | Where-Object { $_.key -eq 'demo.overview' -and $_.route -eq '/demo' }).Count -eq 1) `
        "Packaged Demo screen is missing from canonical Engineering."

    $drivers = @(Invoke-RestMethod -Uri "$BaseUrl/api/drivers" -WebSession $session -TimeoutSec 5)
    Assert-ReleaseCondition ($drivers.Count -ge 1 -and -not [string]::IsNullOrWhiteSpace([string]$drivers[0].driverId)) `
        "Packaged product did not load its Runtime driver surface."

    Invoke-WebRequest `
        -Uri "$BaseUrl/api/project-package/export?projectKey=demo&projectName=Wave%2013%20Demo" `
        -WebSession $session `
        -OutFile $packagePath `
        -TimeoutSec 15
    Assert-ReleaseCondition ((Get-Item -LiteralPath $packagePath).Length -gt 0) `
        "Packaged product did not export a non-empty .escadapkg application."

    $inspection = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/project-package/inspect" `
        -WebSession $session `
        -InFile $packagePath `
        -ContentType 'application/vnd.elitescada.project-package' `
        -TimeoutSec 15
    Assert-ReleaseCondition ($inspection.manifest.projectKey -eq 'demo' -and $inspection.engineering.dynamos -eq 8) `
        "Packaged .escadapkg inspection did not preserve Demo identity and built-in Dynamos."

    $preview = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/project-package/import/preview" `
        -WebSession $session `
        -InFile $packagePath `
        -ContentType 'application/vnd.elitescada.project-package' `
        -TimeoutSec 15
    Assert-ReleaseCondition ($preview.canApply -eq $true -and $preview.errorCount -eq 0) `
        "Packaged .escadapkg Open/Preview path rejected its own exported application."

    Write-Host "Wave 13 packaged-product regression passed."
    Write-Host "Web UI, local login, Demo/machine request, Dynamos, Pyodide, Runtime driver surface and .escadapkg round-trip verified."
}
catch {
    if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath }
    if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath }
    throw
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
    if (Test-Path -LiteralPath $packagePath) {
        Remove-Item -LiteralPath $packagePath -Force
    }
}
