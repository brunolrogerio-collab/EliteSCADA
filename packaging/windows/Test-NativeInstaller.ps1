#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BuildDirectory,
    [int]$WebPort = 28080,
    [int]$ApiPort = 25120,
    [int]$DbPort = 25432
)

$ErrorActionPreference = 'Stop'
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
$manifest = Get-Content "$build\manifest.json" -Raw | ConvertFrom-Json
$root = Join-Path $env:ProgramData 'SCADIVO Windows'
$program = Join-Path $env:ProgramFiles 'SCADIVO'
$registry = 'HKLM:\SOFTWARE\SCADIVO\Windows'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'The service smoke requires elevation.' }
if ((Test-Path $root) -or (Test-Path $program) -or (Test-Path $registry) -or (Get-Service ScadivoRuntime,ScadivoPostgres -ErrorAction SilentlyContinue)) {
    throw 'Use a clean Windows host or VM without an installation or retained SCADIVO data.'
}
if ($manifest.profile -ne 'windows-x64-private-evaluation') { throw 'Use a Windows evaluation build.' }
$setup = Join-Path $build $manifest.artifact
if ((Get-FileHash -LiteralPath $setup).Hash -ne $manifest.sha256 -or
    (Get-FileHash "$build\files.json").Hash -ne $manifest.inventorySha256) { throw 'Installer integrity check failed.' }

function Invoke-Setup([string]$name,[string]$exe,[string]$options) {
    $log = Join-Path $build "$name.log"
    $argsList = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LOG=""$log"" $options"
    $process = Start-Process $exe -ArgumentList $argsList -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode) { throw "$name failed ($($process.ExitCode)). Consult $log." }
}
function Wait-Product([int]$port) {
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    do {
        try {
            $r = Invoke-RestMethod "http://127.0.0.1:$port/api/product/info" -TimeoutSec 3
            if ($r.buildCommit -eq $manifest.productCommit) { return $r }
        } catch {}
        Start-Sleep -Milliseconds 750
    } until ([DateTime]::UtcNow -ge $deadline)
    throw "Product readiness timed out on port $port."
}
function Assert-Payload([string]$release) {
    $inventory = Get-Content "$build\files.json" -Raw | ConvertFrom-Json
    foreach ($file in $inventory) {
        if ((Get-FileHash -LiteralPath (Join-Path $release $file.path)).Hash -ne $file.sha256) {
            throw "Installed file differs: $($file.path)"
        }
    }
}
$webAddress = "http://127.0.0.1:$WebPort"
$apiAddress = "http://127.0.0.1:$ApiPort"
$ports = "/WEBPORT=$WebPort /APIPORT=$ApiPort /DBPORT=$DbPort"
Invoke-Setup 'install' $setup $ports
$active = Get-Content "$root\instance.json" -Raw | ConvertFrom-Json
$release = $active.ActiveRelease
Assert-Payload $release
$identity = Wait-Product $ApiPort
$null = Wait-Product $WebPort
if ($identity.displayVersion -ne $manifest.product) { throw 'Wrong product identity.' }
$web = Invoke-WebRequest -UseBasicParsing "$webAddress/" -TimeoutSec 10
if ($web.Content -notmatch 'src="(/assets/[^"]+\.js)"') { throw 'Web application entry point missing.' }
$bundle = Invoke-WebRequest -UseBasicParsing "$webAddress$($Matches[1])" -Method Head -TimeoutSec 10
if ($bundle.Headers['Content-Type'] -notmatch 'javascript') { throw 'JavaScript bundle missing.' }
$auth = Invoke-RestMethod "$webAddress/api/auth/config" -TimeoutSec 10
if (!$auth.initialAdministratorSetupAvailable -or !$auth.localLoginEnabled) { throw 'First-run Administrator creation is unavailable.' }
$bytes = New-Object byte[] 24
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$password = 'Aa1!' + [Convert]::ToBase64String($bytes)
$body = @{username='windows-qa';displayName='Windows QA';password=$password} | ConvertTo-Json -Compress
$null = Invoke-RestMethod "$webAddress/api/auth/bootstrap" -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 20

$app = Get-CimInstance Win32_Service -Filter "Name='ScadivoRuntime'"
$db = Get-CimInstance Win32_Service -Filter "Name='ScadivoPostgres'"
if ($app.StartName -ne 'LocalSystem' -or $app.StartMode -ne 'Auto' -or $db.StartMode -ne 'Auto' -or
    $app.State -ne 'Running' -or $db.State -ne 'Running') {
    throw 'Unexpected service configuration.'
}
if (Get-NetTCPConnection -State Listen -ErrorAction Stop | Where-Object {
    $_.LocalPort -in $WebPort,$ApiPort,$DbPort -and $_.LocalAddress -ne '127.0.0.1'
}) { throw 'An evaluation endpoint is listening outside loopback.' }

Stop-Service ScadivoRuntime
Restart-Service ScadivoPostgres
Start-Service ScadivoRuntime
$null = Wait-Product $WebPort
$afterRestart = Invoke-RestMethod "$webAddress/api/auth/config" -TimeoutSec 10
if ($afterRestart.initialAdministratorRequired) { throw 'Administrator did not persist after restart.' }

[IO.File]::WriteAllText((Join-Path $release 'scadivo.ico'),'repair probe')
Invoke-Setup 'repair' $setup $ports
Assert-Payload $release
$null = Wait-Product $WebPort
$afterRepair = Invoke-RestMethod "$webAddress/api/auth/config" -TimeoutSec 10
if ($afterRepair.initialAdministratorRequired) { throw 'Administrator did not persist after repair.' }
$uninstall = Get-ChildItem -LiteralPath $program -Filter 'unins*.exe' | Select-Object -First 1
if (!$uninstall) { throw 'The uninstaller is missing.' }
Invoke-Setup 'uninstall' $uninstall.FullName ''
if ((Get-Service ScadivoRuntime,ScadivoPostgres -ErrorAction SilentlyContinue) -or
    (Test-Path -LiteralPath "$release\api\Scada.Api.exe") -or
    !(Test-Path -LiteralPath "$root\postgres\PG_VERSION") -or
    !(Test-Path -LiteralPath "$root\instance.json")) { throw 'Removal or data retention failed.' }

# Keep evidence of the removal without making future installs reuse the QA database.
$resolvedRoot = (Resolve-Path -LiteralPath $root).Path
$programData = (Resolve-Path -LiteralPath $env:ProgramData).Path
if ([IO.Path]::GetDirectoryName($resolvedRoot) -ne $programData -or
    [IO.Path]::GetFileName($resolvedRoot) -ne 'SCADIVO Windows') { throw 'Unexpected QA data root.' }
$retainedTestData = Join-Path $programData ('SCADIVO Windows QA ' + [Guid]::NewGuid().ToString('N'))
Move-Item -LiteralPath $resolvedRoot -Destination $retainedTestData
Remove-Item -LiteralPath $registry
[ordered]@{
    installerSha256=$manifest.sha256; product=$identity.displayVersion; productCommit=$identity.buildCommit
    windows=[Environment]::OSVersion.Version.ToString(); payloadFiles=$manifest.files
    install='passed'; firstAdministrator='passed'; localApiAndWeb='passed'; servicesAutomaticAndLoopback='passed'
    restartWithData='passed'; repairWithData='passed'; uninstallWithDataRetained='passed'
} | ConvertTo-Json | Tee-Object -FilePath "$build\smoke-native.json"
