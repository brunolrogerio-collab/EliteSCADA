[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ConfigurationPath,
    [Parameter(Mandatory=$true)][string]$ServiceName,
    [Parameter(Mandatory=$true)][string]$DestinationPath
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
    # Creates the protected file before writing any content; refuses replacement.
    & (Join-Path $PSScriptRoot 'copy-native-protected-backup.ps1') -SourcePath $configPath -DestinationPath $destination -CreateEmpty
    $bytes=[Text.Encoding]::UTF8.GetBytes(($profile | ConvertTo-Json))
    $output=[IO.File]::Open($destination,[IO.FileMode]::Open,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $output.Write($bytes,0,$bytes.Length); $output.Flush($true) } finally { $output.Dispose() }
    Write-Output 'Protected Web/API administrative profile created. Deployment registration and service configuration ownership must still be verified.'
} finally { $lock.Dispose() }
