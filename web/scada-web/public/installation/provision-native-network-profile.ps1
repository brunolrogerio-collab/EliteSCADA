[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ConfigurationPath,
    [Parameter(Mandatory=$true)][string]$ServiceName,
    [Parameter(Mandatory=$true)][string]$DestinationPath,
    [switch]$Register,
    [string]$MediaConfigurationPath,
    [string]$MediaServiceName,
    [string]$DatabaseRegistrationPath
)
$ErrorActionPreference='Stop'
if ($ServiceName -notmatch '^[A-Za-z0-9_.-]+$') { throw 'Invalid service identity.' }
$configPath=(Resolve-Path -LiteralPath $ConfigurationPath).Path
$destination=[IO.Path]::GetFullPath($DestinationPath)
# The deployment executor supplies the actual external configuration file.
# Confirm service existence, but do not infer its configuration from a name.
$service=Get-Service -Name $ServiceName -ErrorAction Stop
if ($null -eq $service) { throw 'Registered service is missing.' }
$lock=[IO.File]::Open("$configPath.network.lock",[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    foreach ($operation in @('access','database','media')) {
        if (Test-Path -LiteralPath "$configPath.$operation-port-pending.json") { throw 'Complete pending recovery before provisioning.' }
    }
    $original=[IO.File]::ReadAllBytes($configPath)
    $config=Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    $ports=@()
    foreach ($name in @('Web','Api')) {
        $uri=$null
        if (-not [Uri]::TryCreate([string]$config.Kestrel.Endpoints.PSObject.Properties[$name].Value.Url,[UriKind]::Absolute,[ref]$uri) -or
            $uri.Scheme -notin @('http','https') -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.AbsolutePath -ne '/' -or $uri.Port -lt 1) { throw 'Valid Web and Api listeners are required.' }
        $ports += $uri.Port
    }
    if ($ports[0] -eq $ports[1]) { throw 'Web and API ports must differ.' }
    $profile=@{schema='scadivo-native-network-v1';platform='windows';configurationFile=$configPath;serviceName=$ServiceName;endpointName='Web';apiEndpointName='Api'}
    if ([bool]$MediaConfigurationPath -ne [bool]$MediaServiceName) { throw 'Supply both media configuration and service.' }
    if ($MediaConfigurationPath) {
        $mediaPath=(Resolve-Path -LiteralPath $MediaConfigurationPath).Path
        if ($mediaPath -eq $configPath -or $MediaServiceName -eq $ServiceName -or $MediaServiceName -notmatch '^[A-Za-z0-9_.-]+$') { throw 'Distinct media configuration and service required.' }
        $mediaService=Get-Service -Name $MediaServiceName -ErrorAction Stop
        if ($null -eq $mediaService) { throw 'Media service missing.' }
        $yaml=Get-Content -LiteralPath $mediaPath -Raw
        $mediaPorts=@()
        foreach ($pair in @(@('apiAddress','GatewayControlUrl'),@('hlsAddress','GatewayPlaybackUrl'))) {
            $listenerMatches=[regex]::Matches($yaml,'(?m)^'+$pair[0]+':\s*([^\r\n#]+)(?:\s*#[^\r\n]*)?$')
            if ($listenerMatches.Count -ne 1) { throw 'Unique root media listener required.' }
            $address=$listenerMatches[0].Groups[1].Value.Trim().Trim('"',"'")
            $portMatch=[regex]::Match($address,':([0-9]+)$')
            if (-not $portMatch.Success) { throw 'Invalid media listener.' }
            $port=[int]$portMatch.Groups[1].Value
            $url=$null
            if (-not [Uri]::TryCreate([string]$config.MediaSources.PSObject.Properties[$pair[1]].Value,[UriKind]::Absolute,[ref]$url) -or $url.Scheme -notin @('http','https') -or $url.Host -notin @('127.0.0.1','::1','[::1]','localhost') -or $url.UserInfo -or $url.Query -or $url.Fragment -or $url.AbsolutePath -ne '/' -or $url.Port -ne $port -or $port -lt 1 -or $port -gt 65535) { throw 'Local media relay and listener must match.' }
            $mediaPorts += $port
        }
        if ($mediaPorts[0] -eq $mediaPorts[1]) { throw 'Media listeners must differ.' }
        $profile.media=@{configurationFile=$mediaPath;serviceName=$MediaServiceName}
    }
    if ($DatabaseRegistrationPath) {
        $db=Get-Content -LiteralPath $DatabaseRegistrationPath -Raw | ConvertFrom-Json
        $required=@('topologyFile','configurationFile','psqlExecutable','serviceName','currentPort','username','databaseName')
        if (@($db.PSObject.Properties).Count -ne $required.Count -or @($db.PSObject.Properties.Name | Where-Object { $_ -notin $required }).Count -gt 0) { throw 'Only declared database fields are accepted; no credentials.' }
        $topologyPath=(Resolve-Path -LiteralPath $db.topologyFile).Path
        $topology=Get-Content -LiteralPath $topologyPath -Raw | ConvertFrom-Json
        if ($topology.schemaVersion -ne 1 -or $topology.active.mode -ne 'LocalManaged' -or $null -ne $topology.pending) { throw 'Idle local database topology required.' }
        $pg=(Resolve-Path -LiteralPath $db.configurationFile).Path
        $psql=(Resolve-Path -LiteralPath $db.psqlExecutable).Path
        if (-not (Test-Path -LiteralPath $pg -PathType Leaf) -or -not (Test-Path -LiteralPath $psql -PathType Leaf) -or $pg -eq $configPath -or ($MediaConfigurationPath -and $pg -eq $mediaPath) -or $psql -in @($configPath,$pg) -or $db.serviceName -in @($ServiceName,$MediaServiceName) -or $db.serviceName -notmatch '^[A-Za-z0-9_.-]+$') { throw 'Distinct database service and files required.' }
        if ($db.currentPort -isnot [int] -and $db.currentPort -isnot [long]) { throw 'Integer database port required.' }
        if ($db.currentPort -lt 1 -or $db.currentPort -gt 65535 -or ($null -ne $config.DatabaseTopology.LocalManagedPort -and $config.DatabaseTopology.LocalManagedPort -ne $db.currentPort)) { throw 'Database registration port differs from configuration.' }
        foreach ($key in @('username','databaseName')) { if ($db.$key -isnot [string] -or [string]::IsNullOrWhiteSpace($db.$key) -or $db.$key.StartsWith('-')) { throw 'Database identity required.' } }
        $dbService=Get-Service -Name $db.serviceName -ErrorAction Stop
        if ($null -eq $dbService) { throw 'Database service missing.' }
        $profile.topologyFile=$topologyPath
        $profile.database=@{configurationFile=$pg;psqlExecutable=$psql;serviceName=$db.serviceName;currentPort=$db.currentPort;username=$db.username;databaseName=$db.databaseName}
    }
    # Creates the protected file before writing any content; refuses replacement.
    & (Join-Path $PSScriptRoot 'copy-native-protected-backup.ps1') -SourcePath $configPath -DestinationPath $destination -CreateEmpty
    $bytes=[Text.Encoding]::UTF8.GetBytes(($profile | ConvertTo-Json))
    $output=[IO.File]::Open($destination,[IO.FileMode]::Open,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $output.Write($bytes,0,$bytes.Length); $output.Flush($true) } finally { $output.Dispose() }
    if ($Register) {
        $backup="$configPath.network-backup-$([Guid]::NewGuid().ToString('N'))"
        & (Join-Path $PSScriptRoot 'copy-native-protected-backup.ps1') -SourcePath $configPath -DestinationPath $backup
        try {
            if (-not $config.PSObject.Properties['InstallationNetwork']) { $config | Add-Member -NotePropertyName InstallationNetwork -NotePropertyValue ([pscustomobject]@{}) }
            $config.InstallationNetwork | Add-Member -NotePropertyName Profile -NotePropertyValue 'native-windows' -Force
            $config.InstallationNetwork | Add-Member -NotePropertyName AdministrativeProfilePath -NotePropertyValue $destination -Force
            $registered=[Text.Encoding]::UTF8.GetBytes(($config | ConvertTo-Json -Depth 100))
            $output=[IO.File]::Open($configPath,[IO.FileMode]::Open,[IO.FileAccess]::Write,[IO.FileShare]::None)
            try { $output.SetLength(0); $output.Write($registered,0,$registered.Length); $output.Flush($true) } finally { $output.Dispose() }
        } catch {
            [IO.File]::WriteAllBytes($configPath,$original)
            throw
        }
        Write-Output 'Protected profile registered. Service reload and account access still require verification.'
        return
    }
    Write-Output 'Protected Web/API administrative profile created. Deployment registration and service configuration ownership must still be verified.'
} finally { $lock.Dispose() }
