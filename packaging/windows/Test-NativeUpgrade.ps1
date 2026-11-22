#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PreviousBuild,
    [Parameter(Mandatory)][string]$CurrentBuild,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [int]$WebPort = 28081,
    [int]$ApiPort = 25121,
    [int]$DbPort = 25433
)

$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this test elevated.' }
$root = Join-Path $env:ProgramData 'SCADIVO Windows'
$program = Join-Path $env:ProgramFiles 'SCADIVO'
$registry = 'HKLM:\SOFTWARE\SCADIVO\Windows'
$evidence = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$ps = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'

function Assert-CleanHost {
    if ((Test-Path -LiteralPath $root) -or (Test-Path -LiteralPath $program) -or
        (Test-Path -LiteralPath $registry) -or (Get-Service ScadivoRuntime,ScadivoPostgres -ErrorAction SilentlyContinue)) {
        throw 'Expected a clean installation state.'
    }
}
Assert-CleanHost

function Read-Build([string]$directory) {
    $path = (Resolve-Path -LiteralPath $directory).Path
    $manifest = Get-Content -LiteralPath "$path\manifest.json" -Raw | ConvertFrom-Json
    $exe = Join-Path $path $manifest.artifact
    if ($manifest.profile -ne 'windows-x64-private-evaluation' -or
        (Get-FileHash -LiteralPath $exe).Hash -ne $manifest.sha256) {
        throw 'Installer integrity check failed.'
    }
    return [pscustomobject]@{
        Manifest=$manifest; Exe=$exe; Version=[version](Get-Item -LiteralPath $exe).VersionInfo.FileVersion.Trim()
    }
}

function Invoke-Setup([string]$phase,[string]$exe,[string]$options) {
    $log = Join-Path $evidence "$phase.log"
    $argsList = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LOG=""$log"" $options"
    $process = Start-Process -FilePath $exe -ArgumentList $argsList -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode) { throw "$phase failed ($($process.ExitCode)). Consult $log." }
}

function Assert-Identity($build) {
    $identity = Invoke-RestMethod "$web/api/product/info" -TimeoutSec 12
    if ($identity.buildCommit -ne $build.Manifest.productCommit -or
        $identity.displayVersion -ne $build.Manifest.product) { throw 'Unexpected product version.' }
}

function Login-Headers([string]$path) {
    $response = Invoke-WebRequest -UseBasicParsing "$web/api/auth/$path" -Method Post -Body $userBody -ContentType 'application/json' -TimeoutSec 20
    $cookie = @($response.Headers['Set-Cookie']) -join ';'
    if ($cookie -notmatch 'elitescada_access=([A-Za-z0-9_.-]+)') { throw 'The installed product did not issue a session.' }
    return @{Authorization=('Bearer ' + $Matches[1])}
}

function Invoke-Database([string]$query) {
    $active = Get-Content -LiteralPath "$root\instance.json" -Raw | ConvertFrom-Json
    $psql = Join-Path $active.ActiveRelease 'postgres\bin\psql.exe'
    $env:PGPASSWORD = $active.ApiPassword
    try {
        $result = & $psql -h 127.0.0.1 -p $DbPort -U scadivo -d scadivo -X -v ON_ERROR_STOP=1 -Atq -c $query
        if ($LASTEXITCODE) { throw 'The Historian fixture query failed.' }
        return @($result) -join ','
    } finally { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
}

function Remove-Fixture([string]$phase) {
    $active = Get-Content -LiteralPath "$root\instance.json" -Raw | ConvertFrom-Json
    if ($active.WebPort -ne $WebPort -or $active.ApiPort -ne $ApiPort -or $active.DbPort -ne $DbPort) {
        throw 'Unexpected instance ports. Preserve the data.'
    }
    $hostScript = Join-Path $active.ActiveRelease 'host\Install-Host.ps1'
    $copiedHostScript = Join-Path $evidence "$phase-host.ps1"
    Copy-Item -LiteralPath $hostScript -Destination $copiedHostScript
    $uninstaller = Get-ChildItem -LiteralPath $program -Filter 'unins*.exe' -File | Select-Object -First 1
    if (!$uninstaller) { throw 'Uninstaller missing.' }
    Invoke-Setup $phase $uninstaller.FullName ''
    if (!(Test-Path -LiteralPath "$root\postgres\PG_VERSION")) { throw 'Default uninstall did not retain the database.' }
    $removeArgs = @('-NoLogo','-NoProfile','-NonInteractive','-ExecutionPolicy','RemoteSigned','-File',
        $copiedHostScript,'-Action','Remove','-Release',$active.ActiveRelease,'-PurgeData')
    & $ps @removeArgs
    if ($LASTEXITCODE) { throw 'Explicit purge failed.' }
    Assert-CleanHost
}

$previous = Read-Build $PreviousBuild
$current = Read-Build $CurrentBuild
if ($previous.Version -ge $current.Version) { throw 'The first build must be a predecessor.' }
$web = "http://127.0.0.1:$WebPort"
$ports = "/WEBPORT=$WebPort /APIPORT=$ApiPort /DBPORT=$DbPort"
$bytes = New-Object byte[] 24
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$userBody = @{username='windows-upgrade-qa';displayName='Windows Upgrade QA';password=('Aa1!' + [Convert]::ToBase64String($bytes))} |
    ConvertTo-Json -Compress

Invoke-Setup 'install-predecessor' $previous.Exe $ports
Assert-Identity $previous
$headers = Login-Headers 'bootstrap'
$beforeUser = Invoke-RestMethod "$web/api/auth/me" -Headers $headers -TimeoutSec 10
$projectBody = @{projectKey='windows-upgrade-qa';projectName='Windows upgrade QA'} | ConvertTo-Json -Compress
$project = Invoke-RestMethod "$web/api/engineering/persistence/projects/first" -Method Post -Headers $headers -ContentType 'application/json' -Body $projectBody -TimeoutSec 20
if (!$project.revision.revision) { throw 'Engineering project fixture was not created.' }
$before = Get-Content -LiteralPath "$root\instance.json" -Raw | ConvertFrom-Json
$tag = [Guid]::NewGuid().ToString()
$null = Invoke-Database "INSERT INTO elitescada.tag_history (tag_id,ts,quality,source,value) VALUES ('$tag',now(),192,'windows-upgrade-qa','41.25');"
if ((Invoke-Database "SELECT value FROM elitescada.tag_history WHERE tag_id='$tag' ORDER BY ts;") -ne '41.25') {
    throw 'Initial Historian sample missing.'
}

Invoke-Setup 'upgrade' $current.Exe ''
Assert-Identity $current
$after = Get-Content -LiteralPath "$root\instance.json" -Raw | ConvertFrom-Json
if ($after.WebPort -ne $WebPort -or $after.ApiPort -ne $ApiPort -or $after.DbPort -ne $DbPort -or
    $after.JwtKey -ne $before.JwtKey -or $after.MaterialKey -ne $before.MaterialKey -or
    $after.ActiveRelease -eq $before.ActiveRelease) {
    throw 'The upgrade changed durable settings or failed to switch release.'
}
$headers = Login-Headers 'login'
$afterUser = Invoke-RestMethod "$web/api/auth/me" -Headers $headers -TimeoutSec 10
if (!$beforeUser.subjectId -or $beforeUser.subjectId -ne $afterUser.subjectId) {
    throw 'Administrator identity did not survive upgrade.'
}
$afterProject = Invoke-RestMethod "$web/api/engineering/persistence/windows-upgrade-qa/latest" -Headers $headers -TimeoutSec 10
if ($afterProject.revision -ne $project.revision.revision -or $afterProject.projectName -ne 'Windows upgrade QA') {
    throw 'Engineering revision did not survive upgrade.'
}
$null = Invoke-Database "INSERT INTO elitescada.tag_history (tag_id,ts,quality,source,value) VALUES ('$tag',now(),192,'windows-upgrade-qa','43.5');"
if ((Invoke-Database "SELECT value FROM elitescada.tag_history WHERE tag_id='$tag' ORDER BY ts;") -ne '41.25,43.5') {
    throw 'Historian samples missing after upgrade.'
}

[ordered]@{
    previous=$previous.Manifest.product; previousSha256=$previous.Manifest.sha256
    current=$current.Manifest.product; currentSha256=$current.Manifest.sha256
    administratorRetained=$true; engineeringRevision=$afterProject.revision
    historianValues=@(41.25,43.5); portsAndHostKeysRetained=$true
} | ConvertTo-Json | Set-Content -LiteralPath "$evidence\windows-upgrade-data.json"

Remove-Fixture 'uninstall-upgrade'
Invoke-Setup 'reinstall-clean' $current.Exe $ports
Assert-Identity $current
$auth = Invoke-RestMethod "$web/api/auth/config" -TimeoutSec 10
if (!$auth.initialAdministratorRequired -or !$auth.initialAdministratorSetupAvailable) {
    throw 'Full purge did not restore first-run setup on reinstallation.'
}
Remove-Fixture 'uninstall-clean'
[ordered]@{defaultUninstallPreservedData=$true; explicitPurge=$true; reinstallStartsFresh=$true} |
    ConvertTo-Json | Set-Content -LiteralPath "$evidence\windows-upgrade-removal.json"
