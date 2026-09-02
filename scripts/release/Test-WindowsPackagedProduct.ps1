[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProductRoot,
    [string]$BaseUrl = "http://127.0.0.1:5093",
    [string]$ExpectedProjectKey = "wave13-release-smoke"
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

function Wait-PackagedProductHost {
    param(
        [Parameter(Mandatory = $true)][System.Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][string]$Url
    )

    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri "$Url/health" -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) { return }
        }
        catch {
            if ($Process.HasExited) { break }
            Start-Sleep -Seconds 1
        }
    }

    $exitDiagnostic = if ($Process.HasExited) { [string]$Process.ExitCode } else { "still running" }
    throw "Packaged product host did not become healthy. Process state: $exitDiagnostic."
}

if ([string]::IsNullOrWhiteSpace($ExpectedProjectKey)) {
    throw "ExpectedProjectKey is required for the packaged persistence/Active Runtime regression."
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
$restartStdoutPath = Join-Path $temporaryRoot "wave13-product-host.restart.stdout.log"
$restartStderrPath = Join-Path $temporaryRoot "wave13-product-host.restart.stderr.log"
$diagnosticPaths = @($stdoutPath, $stderrPath, $restartStdoutPath, $restartStderrPath)
$packagePath = Join-Path $temporaryRoot "wave13-product-roundtrip.escadapkg"
$process = Start-Process `
    -FilePath $productExe `
    -ArgumentList '--urls', $BaseUrl `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -PassThru

try {
    Wait-PackagedProductHost -Process $process -Url $BaseUrl

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

    $persistence = Invoke-RestMethod `
        -Uri "$BaseUrl/api/engineering/persistence/status" `
        -WebSession $session `
        -TimeoutSec 5
    Assert-ReleaseCondition ($persistence.enabled -eq $true -and $persistence.provider -eq 'postgresql') `
        "Packaged Engineering persistence is not using PostgreSQL."
    Assert-ReleaseCondition ([string]$persistence.configuredProjectKey -eq $ExpectedProjectKey) `
        "Packaged Runtime project configuration does not match ExpectedProjectKey."

    $licensing = Invoke-RestMethod -Uri "$BaseUrl/api/licensing/status" -WebSession $session -TimeoutSec 5
    Assert-ReleaseCondition ($licensing.license.state -eq 'Demo' -and $licensing.license.maximumTags -eq 200) `
        "Packaged product did not start in the accepted 200-TAG Demo mode."
    Assert-ReleaseCondition ($licensing.license.demoMaximumContinuousMinutes -eq 300) `
        "Packaged product did not expose the accepted 300-minute Demo session contract."

    $machineRequest = Invoke-RestMethod -Uri "$BaseUrl/api/licensing/request" -WebSession $session -TimeoutSec 5
    Assert-ReleaseCondition ([string]$machineRequest.requestCode -like 'ESREQ1.*') `
        "Packaged product did not expose a versioned machine request code."

    $dynamosResponse = Invoke-RestMethod -Uri "$BaseUrl/api/engineering/dynamos" -WebSession $session -TimeoutSec 5
    $dynamos = @(
        foreach ($dynamo in $dynamosResponse) {
            $dynamo
        }
    )
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

    $screensResponse = Invoke-RestMethod -Uri "$BaseUrl/api/engineering/screens" -WebSession $session -TimeoutSec 5
    $screens = @(
        foreach ($screen in $screensResponse) {
            $screen
        }
    )
    Assert-ReleaseCondition (@($screens | Where-Object { $_.key -eq 'demo.overview' -and $_.route -eq '/demo' }).Count -eq 1) `
        "Packaged Demo screen is missing from canonical Engineering."

    $driversResponse = Invoke-RestMethod -Uri "$BaseUrl/api/drivers" -WebSession $session -TimeoutSec 5
    $drivers = @(
        foreach ($driver in $driversResponse) {
            $driver
        }
    )
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

    $workspaceBeforeActivationFixture = Invoke-RestMethod `
        -Uri "$BaseUrl/api/engineering/workspace" `
        -WebSession $session `
        -TimeoutSec 5
    $activationExport = Invoke-WebRequest `
        -Uri "$BaseUrl/api/engineering/export/json" `
        -WebSession $session `
        -UseBasicParsing `
        -TimeoutSec 10
    $activationPackage = $activationExport.Content | ConvertFrom-Json
    $activationDataSource = @(
        $activationPackage.dataSources | Where-Object { $_.key -eq 'builtin.simulation' }
    )[0]
    Assert-ReleaseCondition ($null -ne $activationDataSource) `
        "Packaged Demo activation fixture did not contain its expected Data Source."
    $activationDataSource.driver = 'builtin.memory.server'
    $activationDataSource.settings = $null
    $activationFixtureApply = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/engineering/import/json/apply" `
        -WebSession $session `
        -Headers @{ 'x-elitescada-workspace-version' = [string]$workspaceBeforeActivationFixture.changeVersion } `
        -ContentType 'application/json' `
        -Body ($activationPackage | ConvertTo-Json -Depth 100 -Compress) `
        -TimeoutSec 20
    Assert-ReleaseCondition ([int]$activationFixtureApply.updated -ge 1) `
        "Packaged activation fixture did not convert the Demo Data Source to Server Memory."

    $workspaceBeforeSave = Invoke-RestMethod `
        -Uri "$BaseUrl/api/engineering/workspace" `
        -WebSession $session `
        -TimeoutSec 5
    $saveBody = @{
        projectName = 'Wave 13 Packaged Release Smoke'
        savedBy = 'wave13-release-ci'
    } | ConvertTo-Json
    $firstSave = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/engineering/persistence/$ExpectedProjectKey/save" `
        -WebSession $session `
        -ContentType 'application/json' `
        -Body $saveBody `
        -TimeoutSec 15
    $firstRevision = [long]$firstSave.revision
    Assert-ReleaseCondition ($firstRevision -gt 0 -and [string]$firstSave.projectKey -eq $ExpectedProjectKey) `
        "Packaged Working Engineering was not saved as the configured project."

    $firstPublish = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/engineering/persistence/$ExpectedProjectKey/revisions/$firstRevision/publish" `
        -WebSession $session `
        -ContentType 'application/json' `
        -Body (@{ publishedBy = 'wave13-release-ci' } | ConvertTo-Json) `
        -TimeoutSec 15
    Assert-ReleaseCondition ([long]$firstPublish.lifecycle.publishedRevision -eq $firstRevision) `
        "Packaged saved Revision was not published."

    $firstActivation = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/engineering/persistence/$ExpectedProjectKey/published/activate" `
        -WebSession $session `
        -ContentType 'application/json' `
        -Body (@{ activatedBy = 'wave13-release-ci' } | ConvertTo-Json) `
        -TimeoutSec 20
    Assert-ReleaseCondition ($firstActivation.activated -eq $true) `
        "Packaged published Revision did not activate."
    Assert-ReleaseCondition ([long]$firstActivation.lifecycle.activeRevision -eq $firstRevision) `
        "Packaged lifecycle did not record the first Active revision."

    $firstRuntime = Invoke-RestMethod `
        -Uri "$BaseUrl/api/engineering/persistence/$ExpectedProjectKey/runtime" `
        -WebSession $session `
        -TimeoutSec 10
    Assert-ReleaseCondition ($firstRuntime.consistent -eq $true) `
        "Packaged live Runtime is inconsistent with persisted Active Engineering."
    Assert-ReleaseCondition (
        [long]$firstRuntime.durable.activeRevision -eq $firstRevision -and
        [long]$firstRuntime.live.revision -eq $firstRevision -and
        [string]$firstRuntime.live.projectKey -eq $ExpectedProjectKey) `
        "Packaged Runtime identity does not match the first persisted Active revision."

    $firstRuntimeTagsResponse = Invoke-RestMethod `
        -Uri "$BaseUrl/api/tags" `
        -WebSession $session `
        -TimeoutSec 10
    $firstRuntimeTags = @(
        foreach ($tag in $firstRuntimeTagsResponse) {
            $tag
        }
    )
    $expectedRuntimeTagPaths = @(
        'Demo.Discharge.Flow',
        'Demo.Discharge.Pressure',
        'Demo.P01.Current',
        'Demo.P01.Fault',
        'Demo.P01.Frequency',
        'Demo.P01.Running',
        'Demo.Tank01.Level'
    )
    $actualRuntimeTagPaths = @($firstRuntimeTags | ForEach-Object { [string]$_.path } | Sort-Object)
    Assert-ReleaseCondition ($actualRuntimeTagPaths.Count -eq $expectedRuntimeTagPaths.Count) `
        "Packaged Active Runtime did not load the complete persisted Demo TAG set."
    Assert-ReleaseCondition (($actualRuntimeTagPaths -join '|') -eq ($expectedRuntimeTagPaths -join '|')) `
        "Packaged Active Runtime TAG identities differ from the persisted Demo TAG set."

    $firstApplication = Invoke-RestMethod `
        -Uri "$BaseUrl/api/runtime/application" `
        -WebSession $session `
        -TimeoutSec 10
    Assert-ReleaseCondition (
        $firstApplication.mode -eq 'engineering' -and
        [long]$firstApplication.revision -eq $firstRevision -and
        @($firstApplication.package.screens).Count -ge 1 -and
        @($firstApplication.package.dynamos).Count -eq 8) `
        "Packaged HMI Runtime did not project the persisted Active Engineering package."

    $workingExport = Invoke-WebRequest `
        -Uri "$BaseUrl/api/engineering/export/json" `
        -WebSession $session `
        -UseBasicParsing `
        -TimeoutSec 10
    $workingPackage = $workingExport.Content | ConvertFrom-Json
    $firstWorkingTag = @($workingPackage.tags)[0]
    Assert-ReleaseCondition ($null -ne $firstWorkingTag) `
        "Packaged Engineering export did not contain a TAG for Working isolation verification."
    if ($null -eq $firstWorkingTag.PSObject.Properties['description']) {
        $firstWorkingTag | Add-Member -NotePropertyName description -NotePropertyValue 'Wave 13 Working-only mutation'
    }
    else {
        $firstWorkingTag.description = 'Wave 13 Working-only mutation'
    }
    $workingMutationJson = $workingPackage | ConvertTo-Json -Depth 100 -Compress
    $workingApply = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/engineering/import/json/apply" `
        -WebSession $session `
        -Headers @{ 'x-elitescada-workspace-version' = [string]$workspaceBeforeSave.changeVersion } `
        -ContentType 'application/json' `
        -Body $workingMutationJson `
        -TimeoutSec 20
    Assert-ReleaseCondition ([int]$workingApply.updated -ge 1) `
        "Packaged Working mutation did not update canonical Engineering."

    $workspaceAfterMutation = Invoke-RestMethod `
        -Uri "$BaseUrl/api/engineering/workspace" `
        -WebSession $session `
        -TimeoutSec 5
    Assert-ReleaseCondition (
        $workspaceAfterMutation.isDirty -eq $true -and
        [long]$workspaceAfterMutation.changeVersion -gt [long]$workspaceBeforeSave.changeVersion) `
        "Packaged Working mutation did not leave a dirty, versioned workspace."

    $runtimeAfterWorkingMutation = Invoke-RestMethod `
        -Uri "$BaseUrl/api/engineering/persistence/$ExpectedProjectKey/runtime" `
        -WebSession $session `
        -TimeoutSec 10
    Assert-ReleaseCondition (
        $runtimeAfterWorkingMutation.consistent -eq $true -and
        [long]$runtimeAfterWorkingMutation.live.revision -eq $firstRevision -and
        [long]$runtimeAfterWorkingMutation.durable.activeRevision -eq $firstRevision) `
        "Mutable Working Engineering changed the persisted Active Runtime directly."

    $secondSave = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/engineering/persistence/$ExpectedProjectKey/save" `
        -WebSession $session `
        -ContentType 'application/json' `
        -Body $saveBody `
        -TimeoutSec 15
    $secondRevision = [long]$secondSave.revision
    Assert-ReleaseCondition (
        $secondRevision -gt $firstRevision -and
        [long]$secondSave.basedOnRevision -eq $firstRevision) `
        "Packaged second Revision did not preserve Working lineage."

    $lifecycleAfterSecondSave = Invoke-RestMethod `
        -Uri "$BaseUrl/api/engineering/persistence/$ExpectedProjectKey/lifecycle" `
        -WebSession $session `
        -TimeoutSec 10
    Assert-ReleaseCondition (
        [long]$lifecycleAfterSecondSave.workingRevision -eq $secondRevision -and
        [long]$lifecycleAfterSecondSave.publishedRevision -eq $firstRevision -and
        [long]$lifecycleAfterSecondSave.activeRevision -eq $firstRevision) `
        "Saving a new Revision incorrectly changed Published or Active authority."

    $secondPublish = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/engineering/persistence/$ExpectedProjectKey/revisions/$secondRevision/publish" `
        -WebSession $session `
        -ContentType 'application/json' `
        -Body (@{ publishedBy = 'wave13-release-ci' } | ConvertTo-Json) `
        -TimeoutSec 15
    Assert-ReleaseCondition (
        [long]$secondPublish.lifecycle.publishedRevision -eq $secondRevision -and
        [long]$secondPublish.lifecycle.activeRevision -eq $firstRevision) `
        "Publishing a new Revision incorrectly replaced Active Runtime."

    $runtimeBeforeSecondActivation = Invoke-RestMethod `
        -Uri "$BaseUrl/api/engineering/persistence/$ExpectedProjectKey/runtime" `
        -WebSession $session `
        -TimeoutSec 10
    Assert-ReleaseCondition ([long]$runtimeBeforeSecondActivation.live.revision -eq $firstRevision) `
        "Published Engineering drove HMI Runtime before explicit activation."

    $secondActivation = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/engineering/persistence/$ExpectedProjectKey/published/activate" `
        -WebSession $session `
        -ContentType 'application/json' `
        -Body (@{ activatedBy = 'wave13-release-ci' } | ConvertTo-Json) `
        -TimeoutSec 20
    Assert-ReleaseCondition (
        $secondActivation.activated -eq $true -and
        [long]$secondActivation.lifecycle.activeRevision -eq $secondRevision) `
        "Explicit activation did not move Runtime to the second Published revision."

    $secondApplication = Invoke-RestMethod `
        -Uri "$BaseUrl/api/runtime/application" `
        -WebSession $session `
        -TimeoutSec 10
    Assert-ReleaseCondition (
        $secondApplication.mode -eq 'engineering' -and
        [long]$secondApplication.revision -eq $secondRevision) `
        "HMI Runtime did not move to the explicitly activated second revision."

    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
    $process = Start-Process `
        -FilePath $productExe `
        -ArgumentList '--urls', $BaseUrl `
        -RedirectStandardOutput $restartStdoutPath `
        -RedirectStandardError $restartStderrPath `
        -PassThru
    Wait-PackagedProductHost -Process $process -Url $BaseUrl

    $restartSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $restartLogin = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/auth/login" `
        -WebSession $restartSession `
        -ContentType 'application/json' `
        -Body $loginBody `
        -TimeoutSec 10
    Assert-ReleaseCondition ($restartLogin.username -eq 'wave13-admin') `
        "Packaged local identity did not persist across host restart."

    $recoveredRuntime = Invoke-RestMethod `
        -Uri "$BaseUrl/api/engineering/persistence/$ExpectedProjectKey/runtime" `
        -WebSession $restartSession `
        -TimeoutSec 10
    Assert-ReleaseCondition (
        $recoveredRuntime.consistent -eq $true -and
        [long]$recoveredRuntime.durable.activeRevision -eq $secondRevision -and
        [long]$recoveredRuntime.live.revision -eq $secondRevision) `
        "Packaged host restart did not recover persisted Active Engineering."

    $recoveredApplication = Invoke-RestMethod `
        -Uri "$BaseUrl/api/runtime/application" `
        -WebSession $restartSession `
        -TimeoutSec 10
    Assert-ReleaseCondition (
        $recoveredApplication.mode -eq 'engineering' -and
        [long]$recoveredApplication.revision -eq $secondRevision -and
        @($recoveredApplication.package.screens).Count -ge 1 -and
        @($recoveredApplication.package.dynamos).Count -eq 8) `
        "Packaged HMI application was not recovered from persisted Active Engineering."

    $recoveredMachineRequest = Invoke-RestMethod `
        -Uri "$BaseUrl/api/licensing/request" `
        -WebSession $restartSession `
        -TimeoutSec 5
    Assert-ReleaseCondition ($recoveredMachineRequest.requestCode -eq $machineRequest.requestCode) `
        "Packaged machine request identity changed across a same-machine restart."

    Write-Host "Wave 13 packaged-product regression passed."
    Write-Host "Web UI, local identity, Demo/machine request, Dynamos, Pyodide, Drivers and .escadapkg round-trip verified."
    Write-Host "Working -> Revision -> Published -> Active -> HMI Runtime isolation and restart recovery verified through PostgreSQL."
}
catch {
    foreach ($diagnosticPath in $diagnosticPaths) {
        if (Test-Path -LiteralPath $diagnosticPath) { Get-Content -LiteralPath $diagnosticPath }
    }
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
