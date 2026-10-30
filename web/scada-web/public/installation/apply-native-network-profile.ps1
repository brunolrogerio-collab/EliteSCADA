[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProfilePath,
    [ValidateRange(1,65535)][int]$WebPort,
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
$service = Get-Service -Name $profile.serviceName -ErrorAction Stop
if ($service.Status -ne 'Running') { throw 'The service must be running before a port change.' }
if ($WebPort -eq $current.Port) {
    Write-Output "Web/API already use port $WebPort. No changes required."
    return
}
$probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Any, $WebPort)
try { $probe.Start() } finally { $probe.Stop() }
if (-not $Apply) {
    Write-Output "Plan: Web/API port $($current.Port) -> $WebPort. Run with -Apply to apply this plan."
    return
}
$backup = "$configPath.network-backup-$([Guid]::NewGuid().ToString('N'))"
Copy-Item -LiteralPath $configPath -Destination $backup -ErrorAction Stop
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
    # Keep the existing file ACL; never print or return configuration secrets.
    [IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
    Restart-Service -Name $profile.serviceName -ErrorAction Stop
    Wait-NativeHealth $health.Uri.AbsoluteUri
    Write-Output "Web/API now use port $WebPort. Configuration backup: $backup"
} catch {
    $failure = $_
    try {
        Copy-Item -LiteralPath $backup -Destination $configPath -Force -ErrorAction Stop
        Restart-Service -Name $profile.serviceName -ErrorAction Stop
        $health.Port = $current.Port
        Wait-NativeHealth $health.Uri.AbsoluteUri
    } catch {
        throw "Port change failed and automatic recovery failed. Restore the protected configuration backup: $backup"
    }
    throw 'Port change failed. The previous configuration was restored and its health check passed.'
}