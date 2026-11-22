#requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
$manifest = Get-Content -LiteralPath "$build/manifest.json" -Raw | ConvertFrom-Json
if ($manifest.profile -ne 'windows-x64-payload-lab') { throw 'Expected the private Windows payload profile.' }
$setup = Get-Item -LiteralPath (Join-Path $build $manifest.artifact)
if ((Get-FileHash $setup.FullName).Hash -ne $manifest.sha256 -or
    (Get-FileHash "$build/files.json").Hash -ne $manifest.inventorySha256) { throw 'Build evidence mismatch.' }
$inventory = Get-Content "$build/files.json" -Raw | ConvertFrom-Json
$registration = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{5E77D761-B55A-42C2-B674-5D981500D9A1}_is1'
foreach ($hive in 'HKCU','HKLM') {
    if (Test-Path ("{0}:\{1}" -f $hive,$registration)) { throw 'An installation exists. Use a clean Windows account or VM.' }
}
$work = New-Item -ItemType Directory -Path (Join-Path ([IO.Path]::GetTempPath()) ('SCADIVO setup ' + [Guid]::NewGuid().ToString('N')))
$install = Join-Path $work.FullName 'Program files'
$release = Join-Path $install ('releases\' + $setup.VersionInfo.FileVersion.Trim())
function Invoke-LabSetup([string]$exe, [string[]]$options) {
    $argsList = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-') + $options
    $p = Start-Process -FilePath $exe -ArgumentList $argsList -WindowStyle Hidden -PassThru -Wait
    if ($p.ExitCode) { throw "Setup failed ($($p.ExitCode)). Logs: $work" }
}
function Assert-Inventory {
    $installed = (Get-ChildItem -LiteralPath $release -Recurse -File).Count
    if ($installed -ne $inventory.Count) { throw "Expected $($inventory.Count) files, found $installed." }
    foreach ($item in $inventory) {
        $path = Join-Path $release $item.path
        if ((Get-FileHash -LiteralPath $path).Hash -ne $item.sha256) { throw "Changed file: $($item.path)" }
    }
}
$options = @('/CURRENTUSER', ('/DIR="' + $install + '"'))
Invoke-LabSetup $setup.FullName ($options + ('/LOG="' + $work.FullName + '\install.log"'))
$uninstaller = Get-Item -LiteralPath "$install/unins000.exe"
try {
    Assert-Inventory
    # Deliberately damage one file only inside this test's own fresh install.
    [IO.File]::WriteAllText((Join-Path $release 'scadivo.ico'), 'repair probe')
    Invoke-LabSetup $setup.FullName ($options + ('/LOG="' + $work.FullName + '\repair.log"'))
    Assert-Inventory
    # Make a separate runtime copy; first start can create mutable files.
    $run = New-Item -ItemType Directory -Path "$work/first-start"
    Copy-Item -LiteralPath "$release/api" -Destination $run.FullName -Recurse
    $log = Join-Path $work 'api.log'
    $key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $run 'api/Scada.Api.exe'))
    $start.WorkingDirectory = $run.FullName
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment.Clear()
    foreach ($keyName in 'SystemRoot','WINDIR','PATH','TEMP','TMP','SystemDrive') {
        $value = [Environment]::GetEnvironmentVariable($keyName)
        if ($value) { $start.Environment[$keyName] = $value }
    }
    $dbImage = 'timescale/timescaledb:2.29.2-pg18'
    $dbPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    $previousPassword = $env:POSTGRES_PASSWORD
    try {
        $env:POSTGRES_PASSWORD = $dbPassword
        $dbId = docker run --pull=never --rm -d -p 127.0.0.1::5432 -e POSTGRES_PASSWORD -e POSTGRES_DB=scadivo_lab $dbImage
        if ($LASTEXITCODE -or $dbId -notmatch '^[a-f0-9]{64}$') { throw 'Cannot start the isolated PostgreSQL fixture.' }
    } finally { $env:POSTGRES_PASSWORD = $previousPassword }
    $portSpec = docker port $dbId 5432/tcp
    if ($LASTEXITCODE -or $portSpec -notmatch '^127\.0\.0\.1:(\d+)$') { throw 'Cannot resolve the fixture port.' }
    $port = $Matches[1]
    $dbReady = $false
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        docker exec $dbId pg_isready -q -h 127.0.0.1 -U postgres -d scadivo_lab
        if ($LASTEXITCODE -eq 0) { $dbReady = $true; break }
        Start-Sleep -Seconds 1
    }
    if (!$dbReady) { throw 'The isolated database did not become ready.' }
    $start.Environment['ConnectionStrings__EliteScada'] = "Host=127.0.0.1;Port=$port;Database=scadivo_lab;Username=postgres;Password=$dbPassword"
    $start.Environment['DOTNET_ENVIRONMENT'] = 'Production'
    $start.Environment['ASPNETCORE_URLS'] = 'http://127.0.0.1:0'
    $start.Environment['InstallationNetwork__WebRootPath'] = Join-Path $release 'web'
    $start.Environment['Authentication__Enabled'] = 'true'
    $start.Environment['Authentication__Local__Enabled'] = 'true'
    $start.Environment['Authentication__Jwt__Issuer'] = 'installer-lab'
    $start.Environment['Authentication__Jwt__Audience'] = 'installer-lab'
    $start.Environment['Authentication__Jwt__SigningKey'] = $key
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stderr = $process.StandardError.ReadToEndAsync()
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        $endpoint = $null
        while ([DateTime]::UtcNow -lt $deadline) {
            $line = $process.StandardOutput.ReadLineAsync().WaitAsync([timespan]::FromSeconds(10)).GetAwaiter().GetResult()
            if ($null -eq $line) { throw "API exited. $($stderr.GetAwaiter().GetResult())" }
            Add-Content -LiteralPath $log -Value $line
            if ($line -match 'Now listening on: (http://127\.0\.0\.1:\d+)') { $endpoint = $Matches[1]; break }
        }
        if (!$endpoint) { throw 'Packaged API did not start.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $identity = Invoke-RestMethod "$endpoint/api/product/info" -TimeoutSec 10
        if ($identity.displayVersion -ne $manifest.product -or $identity.buildCommit -ne $manifest.productCommit) { throw 'Running API identity differs from the manifest.' }
        $html = (Invoke-WebRequest "$endpoint/" -TimeoutSec 10).Content
        if ($html -notmatch '<title>SCADIVO</title>' -or $html -notmatch 'src="(/assets/[^"]+\.js)"') { throw 'Runtime Web entry point missing.' }
        $js = Invoke-WebRequest "$endpoint$($Matches[1])" -Method Head -TimeoutSec 10
        if ($js.Headers.'Content-Type' -notmatch 'javascript') { throw 'Runtime Web bundle unavailable.' }
        $health = (Invoke-WebRequest "$endpoint/health" -TimeoutSec 10).StatusCode
    } finally {
        if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
    $probe = Join-Path $install 'retained-probe.txt'
    Set-Content -LiteralPath $probe -Value 'operator-owned'
} finally {
    try {
        if ($dbId -match '^[a-f0-9]{64}$') {
            docker stop -t 10 $dbId | Out-Null
            if ($LASTEXITCODE) { throw 'Could not stop the disposable database.' }
        }
    } finally {
        Invoke-LabSetup $uninstaller.FullName @(('/LOG="' + $work.FullName + '\uninstall.log"'))
    }
}
if (!(Test-Path -LiteralPath $probe) -or (Test-Path -LiteralPath "$release/api/Scada.Api.exe") -or
    (Test-Path ("HKCU:\{0}" -f $registration))) { throw 'Uninstall verification failed.' }
[ordered]@{
    installerSha256 = $manifest.sha256; filesVerified = $inventory.Count; install = 'passed'; repair = 'passed'
    mode = 'disposable per-user payload, loopback API with disposable PostgreSQL'; dbImage = $dbImage; windows = [Environment]::OSVersion.Version.ToString()
    health = $health; apiVersion = $identity.displayVersion; apiCommit = $identity.buildCommit
    web = 'index and JavaScript bundle'; uninstall = 'passed'; unownedFileRetained = $true
} | ConvertTo-Json | Tee-Object -FilePath "$build/smoke.json"
Write-Output "Test logs: $work"
