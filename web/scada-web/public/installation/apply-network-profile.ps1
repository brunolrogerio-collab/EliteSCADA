[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstallDirectory,
    [Parameter(Mandatory = $true)][string]$InstanceId,
    [Parameter(Mandatory = $true)][ValidateSet('127.0.0.1', '0.0.0.0')][string]$WebBind,
    [Parameter(Mandatory = $true)][ValidateSet('127.0.0.1', '0.0.0.0')][string]$ApiBind,
    [Parameter(Mandatory = $true)][ValidateRange(1, 65535)][int]$WebPort,
    [Parameter(Mandatory = $true)][ValidateRange(1, 65535)][int]$ApiPort,
    [Parameter(Mandatory = $true)][ValidateRange(1, 65535)][int]$DatabasePort,
    [Parameter(Mandatory = $true)][ValidateRange(1, 65535)][int]$MediaControlPort,
    [Parameter(Mandatory = $true)][ValidateRange(1, 65535)][int]$MediaPlaybackPort,
    [string]$SourceInstanceId,
    [switch]$NewInstallation,
    [switch]$ConfirmIsolatedSecrets
)

$ErrorActionPreference = 'Stop'
$script:Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Read-EnvValue([string[]]$Lines, [string]$Name, [string]$DefaultValue) {
    $matches = @($Lines | Where-Object { $_ -match "^\s*$([regex]::Escape($Name))=" })
    if ($matches.Count -gt 1) { throw "The .env file has duplicate $Name entries. Correct them before applying a network profile." }
    if ($matches.Count -eq 0) { return $DefaultValue }
    return ($matches[0] -replace "^\s*$([regex]::Escape($Name))=", '').Trim().Trim('"', "'")
}

function Set-EnvValue([System.Collections.Generic.List[string]]$Lines, [string]$Name, [string]$Value) {
    $pattern = "^\s*$([regex]::Escape($Name))="
    $indexes = @()
    for ($index = 0; $index -lt $Lines.Count; $index++) {
        if ($Lines[$index] -match $pattern) { $indexes += $index }
    }
    if ($indexes.Count -gt 1) { throw "The .env file has duplicate $Name entries. No change was made." }
    if ($indexes.Count -eq 1) { $Lines[$indexes[0]] = "$Name=$Value" }
    else { $Lines.Add("$Name=$Value") }
}

function Get-ProjectContainerOwners([string]$ProjectName, [string]$ComposeFile) {
    $ids = @(& docker compose --project-name $ProjectName -f $ComposeFile ps --all --quiet 2>$null)
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose could not inspect the selected project.' }
    $owners = @()
    foreach ($id in $ids) {
        if ([string]::IsNullOrWhiteSpace($id)) { continue }
        $labelsJson = & docker inspect --format '{{json .Config.Labels}}' $id 2>$null
        if ($LASTEXITCODE -ne 0) { throw 'Docker could not inspect a Compose container. No change was made.' }
        $labels = $labelsJson | ConvertFrom-Json
        $owner = [string]$labels.'com.docker.compose.project.working_dir'
        if ([string]::IsNullOrWhiteSpace($owner)) { throw 'Docker container has no Compose working-directory label. No change was made.' }
        $owners += [System.IO.Path]::GetFullPath($owner.Trim())
    }
    return $owners
}

function Test-TcpPortAvailable([string]$Address, [int]$Port) {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Parse($Address), $Port)
    try { $listener.Start(); return $true }
    catch { return $false }
    finally { $listener.Stop() }
}

function Test-DockerPublishedPortAvailable([string]$Address, [int]$Port) {
    $ids = @(& docker ps --quiet)
    if ($LASTEXITCODE -ne 0) { throw 'Docker could not inspect published host ports.' }
    if ($ids.Count -eq 0) { return $true }

    $json = (& docker inspect @ids | Out-String)
    if ($LASTEXITCODE -ne 0) { throw 'Docker could not inspect published host ports.' }
    $containers = @($json | ConvertFrom-Json)
    foreach ($container in $containers) {
        $published = $container.NetworkSettings.Ports
        if ($null -eq $published) { continue }
        foreach ($entry in $published.PSObject.Properties) {
            foreach ($binding in @($entry.Value)) {
                if ($null -eq $binding -or [int]$binding.HostPort -ne $Port) { continue }
                $boundAddress = [string]$binding.HostIp
                if ($Address -eq '0.0.0.0' -or $boundAddress -in @('', '0.0.0.0', '::', $Address)) { return $false }
            }
        }
    }
    return $true
}

function Test-ProjectVolumesExist([string]$ProjectName) {
    $volumeNames = @(& docker volume ls --quiet)
    if ($LASTEXITCODE -ne 0) { throw 'Docker could not inspect existing data volumes.' }
    foreach ($suffix in @('database', 'protected-material', 'host-keys')) {
        if ($volumeNames -contains "$ProjectName-$suffix") { return $true }
    }
    return $false
}

function Add-AuditRecord([string]$Path, [string]$Result, [string]$OldId, [string]$NewId) {
    $actor = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $line = "{0:o} actor={1} result={2} instance={3} web={4} api={5} database={6} mediaControl={7} mediaPlayback={8}" -f [DateTimeOffset]::Now, $actor, $Result, $NewId, $WebPort, $ApiPort, $DatabasePort, $MediaControlPort, $MediaPlaybackPort
    [System.IO.File]::AppendAllText($Path, $line + [Environment]::NewLine, $script:Utf8NoBom)
}

if ($InstanceId -notmatch '^[a-z0-9][a-z0-9-]{0,47}$') { throw 'Instance ID must use 1-48 lowercase letters, numbers or hyphens and start with a letter or number.' }
if ($NewInstallation) {
    if ($SourceInstanceId -notmatch '^[a-z0-9][a-z0-9-]{0,47}$' -or $SourceInstanceId -eq $InstanceId) { throw 'A new installation needs a different, valid source instance ID.' }
    if (-not $ConfirmIsolatedSecrets) { throw 'A new installation requires -ConfirmIsolatedSecrets after preparing its own .env and unique secrets.' }
} elseif ($SourceInstanceId -or $ConfirmIsolatedSecrets) {
    throw 'New-installation options can only be used when provisioning a second installation.'
}
$selectedPorts = @($WebPort, $ApiPort, $DatabasePort)
if (@($selectedPorts | Select-Object -Unique).Count -ne 3) { throw 'Web, API and database host ports must be different.' }
if ($MediaControlPort -eq $MediaPlaybackPort) { throw 'Media control and HLS must use different internal ports.' }

$root = [System.IO.Path]::GetFullPath($InstallDirectory)
$compose = Join-Path $root 'docker-compose.stable.yml'
$envPath = Join-Path $root '.env'
$auditPath = Join-Path $root 'network-profile-audit.log'
if (-not (Test-Path -LiteralPath $compose -PathType Leaf)) { throw 'The supported docker-compose.stable.yml was not found in the selected installation directory.' }
if (-not (Test-Path -LiteralPath $envPath -PathType Leaf)) { throw 'The installation .env was not found. Create it from the documented example; this script will not create or copy secrets.' }
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker CLI was not found.' }

$lines = [System.IO.File]::ReadAllLines($envPath) | ForEach-Object { $_ }
$oldId = Read-EnvValue $lines 'SCADIVO_INSTALLATION_ID' 'elitescada-stable'
$oldWebBind = Read-EnvValue $lines 'SCADIVO_WEB_BIND' '127.0.0.1'
$oldApiBind = Read-EnvValue $lines 'SCADIVO_API_BIND' '127.0.0.1'
$oldWebPort = [int](Read-EnvValue $lines 'ELITESCADA_WEB_PORT' '18080')
$oldApiPort = [int](Read-EnvValue $lines 'ELITESCADA_API_PORT' '15080')
$oldDatabasePort = [int](Read-EnvValue $lines 'ELITESCADA_DB_PORT' '15432')
$oldMediaControlPort = [int](Read-EnvValue $lines 'SCADIVO_MEDIA_CONTROL_PORT' '9997')
$oldMediaPlaybackPort = [int](Read-EnvValue $lines 'SCADIVO_MEDIA_PLAYBACK_PORT' '8888')
$ownersForOld = @(Get-ProjectContainerOwners $oldId $compose)
$ownersForNew = @(Get-ProjectContainerOwners $InstanceId $compose)
$sameDirectoryOld = $ownersForOld | Where-Object { [string]::Equals($_, $root, [System.StringComparison]::OrdinalIgnoreCase) }
$sameDirectoryNew = $ownersForNew | Where-Object { [string]::Equals($_, $root, [System.StringComparison]::OrdinalIgnoreCase) }
$otherDirectoryNew = $ownersForNew | Where-Object { -not [string]::Equals($_, $root, [System.StringComparison]::OrdinalIgnoreCase) }

if ($otherDirectoryNew) { throw 'This instance ID is already used by a different Compose directory. Choose another ID.' }
if ($NewInstallation) {
    if ($oldId -ne $InstanceId) { throw 'For a new installation, set SCADIVO_INSTALLATION_ID to the new ID in that directory .env before running this script. Existing IDs are never changed in place.' }
    if ($sameDirectoryNew) { throw 'The target instance already has containers in this directory. Use a fresh directory.' }
    if (Test-ProjectVolumesExist $InstanceId) {
        throw "A data volume for '$InstanceId' already exists. A new installation must use a fresh unique ID so it cannot attach to existing data."
    }
} elseif ($InstanceId -ne $oldId) {
    throw 'Instance IDs are not changed in place. Prepare a separate installation directory and set its unique ID in that .env first.'
}

$endpoints = @(
    @{ Name = 'Web'; Address = $WebBind; Port = $WebPort; OldAddress = $oldWebBind; OldPort = $oldWebPort },
    @{ Name = 'API'; Address = $ApiBind; Port = $ApiPort; OldAddress = $oldApiBind; OldPort = $oldApiPort },
    @{ Name = 'Database'; Address = '127.0.0.1'; Port = $DatabasePort; OldAddress = '127.0.0.1'; OldPort = $oldDatabasePort }
)
foreach ($endpoint in $endpoints) {
    $unchanged = $InstanceId -eq $oldId -and $endpoint.Address -eq $endpoint.OldAddress -and $endpoint.Port -eq $endpoint.OldPort
    if (-not $unchanged) {
        if (-not (Test-TcpPortAvailable $endpoint.Address $endpoint.Port)) {
            throw "$($endpoint.Name) port $($endpoint.Port) is already occupied on $($endpoint.Address). No service was stopped."
        }
        if (-not (Test-DockerPublishedPortAvailable $endpoint.Address $endpoint.Port)) {
            throw "$($endpoint.Name) port $($endpoint.Port) is already published by another Docker container. No service was stopped."
        }
    }
}

$candidate = Join-Path $root '.env.scadivo-network-candidate'
$backup = Join-Path $root ('.env.network-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$work = [System.Collections.Generic.List[string]]::new()
$work.AddRange([string[]]$lines)
Set-EnvValue $work 'SCADIVO_INSTALLATION_ID' $InstanceId
Set-EnvValue $work 'SCADIVO_WEB_BIND' $WebBind
Set-EnvValue $work 'SCADIVO_API_BIND' $ApiBind
Set-EnvValue $work 'ELITESCADA_WEB_PORT' ([string]$WebPort)
Set-EnvValue $work 'ELITESCADA_API_PORT' ([string]$ApiPort)
Set-EnvValue $work 'ELITESCADA_DB_PORT' ([string]$DatabasePort)
Set-EnvValue $work 'SCADIVO_MEDIA_CONTROL_PORT' ([string]$MediaControlPort)
Set-EnvValue $work 'SCADIVO_MEDIA_PLAYBACK_PORT' ([string]$MediaPlaybackPort)
[System.IO.File]::WriteAllLines($candidate, $work, $script:Utf8NoBom)

try {
    & docker compose --project-name $InstanceId --env-file $candidate -f $compose config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose rejected the candidate configuration. No service was stopped.' }

    Copy-Item -LiteralPath $envPath -Destination $backup
    Add-AuditRecord $auditPath 'started' $oldId $InstanceId
    Move-Item -LiteralPath $candidate -Destination $envPath -Force

    & docker compose --project-name $InstanceId -f $compose up -d
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose could not apply the profile.' }

    $deadline = (Get-Date).AddSeconds(120)
    $healthy = $false
    while ((Get-Date) -lt $deadline -and -not $healthy) {
        try {
            $web = Invoke-WebRequest -Uri "http://127.0.0.1:$WebPort/healthz" -TimeoutSec 3 -UseBasicParsing
            $api = Invoke-WebRequest -Uri "http://127.0.0.1:$ApiPort/health" -TimeoutSec 3 -UseBasicParsing
            & docker compose --project-name $InstanceId -f $compose exec -T database pg_isready -U elitescada -d elitescada *> $null
            $databaseHealthy = $LASTEXITCODE -eq 0
            & docker compose --project-name $InstanceId -f $compose exec -T api python3 -c "import socket,urllib.request; urllib.request.urlopen('http://media-gateway:$MediaControlPort/v3/paths/list',timeout=3).read(); socket.create_connection(('media-gateway',$MediaPlaybackPort),timeout=3).close()" *> $null
            $mediaHealthy = $LASTEXITCODE -eq 0
            $healthy = $web.StatusCode -eq 200 -and $api.StatusCode -eq 200 -and $databaseHealthy -and $mediaHealthy
        } catch { Start-Sleep -Seconds 3 }
    }
    if (-not $healthy) { throw 'Health checks did not pass within 120 seconds.' }

    Add-AuditRecord $auditPath 'applied' $oldId $InstanceId
    Write-Host "Network profile applied. Web: http://localhost:$WebPort; API: http://localhost:$ApiPort"
    Write-Host "Previous .env backup: $backup"
    if ($InstanceId -ne $oldId) { Write-Host 'This is a separate installation. Keep its own directory, .env secrets and backup.' }
    Write-Host 'No firewall rules were changed. Remote access requires a separately configured TLS/reverse-proxy and firewall policy.'
} catch {
    $failure = $_.Exception.Message
    if (Test-Path -LiteralPath $backup -PathType Leaf) {
        Copy-Item -LiteralPath $backup -Destination $envPath -Force
        if ($sameDirectoryOld) {
            & docker compose --project-name $oldId -f $compose up -d *> $null
        } else {
            & docker compose --project-name $InstanceId -f $compose down *> $null
        }
        Add-AuditRecord $auditPath 'rolled-back' $oldId $InstanceId
    }
    throw "Profile was not accepted: ${failure}. Previous .env and data volumes were preserved. See the local backup and network-profile-audit.log."
} finally {
    Remove-Item -LiteralPath $candidate -Force -ErrorAction SilentlyContinue
}
