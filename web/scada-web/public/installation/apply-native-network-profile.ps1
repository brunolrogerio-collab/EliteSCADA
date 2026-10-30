[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProfilePath,
    [ValidateRange(1,65535)][int]$WebPort,
    [ValidateRange(1,65535)][int]$ApiPort,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
# The installed host adapter owns this profile. It must identify the effective
# appsettings file and endpoint, not a container profile or an arbitrary command.
$profile = Get-Content -LiteralPath $ProfilePath -Raw | ConvertFrom-Json
if ($profile.schema -ne 'scadivo-native-network-v1' -or $profile.platform -ne 'windows') {
    throw 'A registered Windows native network profile is required.'
}
if ($profile.serviceName -notmatch '^[A-Za-z0-9_.-]+$') { throw 'Invalid service identity.' }
$configPath = [IO.Path]::GetFullPath($profile.configurationFile)
# An exclusive handle serializes participating administrative operations.
# The OS releases the handle if this process terminates.
$networkLock = [IO.File]::Open("$configPath.network.lock", [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) { throw 'Configuration file is missing.' }
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$endpoint = $config.Kestrel.Endpoints.PSObject.Properties[$profile.endpointName]
if ($null -eq $endpoint) { throw 'The registered Kestrel endpoint is missing.' }
$current = $null
if (-not [Uri]::TryCreate([string]$endpoint.Value.Url, [UriKind]::Absolute, [ref]$current) -or
    $current.Scheme -notin @('http','https') -or $current.UserInfo -or $current.Query -or $current.AbsolutePath -ne '/') {
    throw 'The registered endpoint must be an HTTP or HTTPS listener URL.'
}
if (-not $PSBoundParameters.ContainsKey('WebPort')) { $WebPort = $current.Port }
$target = [UriBuilder]::new($current)
$target.Port = $WebPort
$health = [UriBuilder]::new($target.Uri)
$health.Host = '127.0.0.1'
$health.Path = 'health'
$apiEndpoint=$null
$apiCurrent=$null
$apiTarget=$null
$apiHealth=$null
if ($profile.apiEndpointName) {
    $apiEndpoint=$config.Kestrel.Endpoints.PSObject.Properties[$profile.apiEndpointName]
    if ($null -eq $apiEndpoint -or $profile.apiEndpointName -eq $profile.endpointName) { throw 'Distinct registered Web/API endpoints required.' }
    if (-not [Uri]::TryCreate([string]$apiEndpoint.Value.Url,[UriKind]::Absolute,[ref]$apiCurrent) -or
        $apiCurrent.Scheme -notin @('http','https') -or $apiCurrent.UserInfo -or $apiCurrent.Query -or $apiCurrent.AbsolutePath -ne '/') { throw 'Invalid API listener URL.' }
    if (-not $PSBoundParameters.ContainsKey('ApiPort')) { $ApiPort=$apiCurrent.Port }
    if ($WebPort -eq $ApiPort) { throw 'Web and API must have distinct access ports.' }
    $apiTarget=[UriBuilder]::new($apiCurrent); $apiTarget.Port=$ApiPort
    $apiHealth=[UriBuilder]::new($apiTarget.Uri); $apiHealth.Host='127.0.0.1'; $apiHealth.Path='health'
} elseif ($PSBoundParameters.ContainsKey('ApiPort')) { throw 'This profile does not register an independent API listener.' }
$service = Get-Service -Name $profile.serviceName -ErrorAction Stop
if ($service.Status -ne 'Running') { throw 'The service must be running before a port change.' }
if ($WebPort -eq $current.Port -and ($null -eq $apiCurrent -or $ApiPort -eq $apiCurrent.Port)) {
    Write-Output "Web/API already use port $WebPort. No changes required."
    return
}
$portsToCheck=@()
if ($WebPort -ne $current.Port) { $portsToCheck += $WebPort }
if ($apiCurrent -and $ApiPort -ne $apiCurrent.Port) { $portsToCheck += $ApiPort }
foreach ($portToCheck in $portsToCheck) {
    $probe=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Any,$portToCheck)
    try { $probe.Start() } finally { $probe.Stop() }
}
if (-not $Apply) {
    $apiLabel='shared'
    if ($apiCurrent) { $apiLabel=[string]$ApiPort }
    Write-Output "Plan: Web port $($current.Port) -> $WebPort; API target $apiLabel. Run with -Apply to apply this plan."
    return
}
$backup = "$configPath.network-backup-$([Guid]::NewGuid().ToString('N'))"
& (Join-Path $PSScriptRoot 'copy-native-protected-backup.ps1') -SourcePath $configPath -DestinationPath $backup
function Wait-NativeHealth([string]$Url) {
    for ($attempt=0; $attempt -lt 30; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2 -MaximumRedirection 0
            if ($response.StatusCode -eq 200) { return }
        } catch { }
        Start-Sleep -Seconds 1
    }
    throw 'The service did not become healthy after the port change.'
}
try {
    $endpoint.Value.Url = $target.Uri.AbsoluteUri.TrimEnd('/')
    if ($apiEndpoint) { $apiEndpoint.Value.Url=$apiTarget.Uri.AbsoluteUri.TrimEnd('/') }
    # Keep the existing file ACL; never print or return configuration secrets.
    [IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
    Restart-Service -Name $profile.serviceName -ErrorAction Stop
    Wait-NativeHealth $health.Uri.AbsoluteUri
    if ($apiHealth) { Wait-NativeHealth $apiHealth.Uri.AbsoluteUri }
    $apiLabel='shared'; if ($apiCurrent) { $apiLabel=[string]$ApiPort }
    Write-Output "Web port $WebPort; API port $apiLabel. Configuration backup: $backup"
} catch {
    $failure = $_
    try {
        Copy-Item -LiteralPath $backup -Destination $configPath -Force -ErrorAction Stop
        Restart-Service -Name $profile.serviceName -ErrorAction Stop
        $health.Port = $current.Port
        Wait-NativeHealth $health.Uri.AbsoluteUri
        if ($apiHealth) { $apiHealth.Port=$apiCurrent.Port; Wait-NativeHealth $apiHealth.Uri.AbsoluteUri }
    } catch {
        throw "Port change failed and automatic recovery failed. Restore the protected configuration backup: $backup"
    }
    throw 'Port change failed. The previous configuration was restored and its health check passed.'
}
} finally {
    $networkLock.Dispose()
}
