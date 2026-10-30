[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProfilePath,
    [Parameter(Mandatory=$true)][string]$PythonExecutable,
    [ValidateRange(1,65535)][int]$ControlPort,
    [ValidateRange(1,65535)][int]$HlsPort,
    [switch]$Apply,
    [switch]$Recover
)
$ErrorActionPreference='Stop'
if ($Recover -and ($Apply -or $PSBoundParameters.ContainsKey('ControlPort') -or $PSBoundParameters.ContainsKey('HlsPort'))) { throw 'Recovery cannot be combined with a port change.' }
$resolvedProfile=(Resolve-Path -LiteralPath $ProfilePath).Path
$profile=Get-Content -LiteralPath $resolvedProfile -Raw | ConvertFrom-Json
if ($profile.schema -ne 'scadivo-native-network-v1' -or $profile.platform -ne 'windows') { throw 'Registered Windows native profile required.' }
$config=(Resolve-Path -LiteralPath $profile.configurationFile).Path
$python=(Resolve-Path -LiteralPath $PythonExecutable).Path
$controlArgument='current'; $hlsArgument='current'
if ($PSBoundParameters.ContainsKey('ControlPort')) { $controlArgument=[string]$ControlPort }
if ($PSBoundParameters.ContainsKey('HlsPort')) { $hlsArgument=[string]$HlsPort }
$mode='plan'
if ($Apply) { $mode='apply' }
if ($Recover) { $mode='recover' }
$lock=[IO.File]::Open("$config.network.lock",[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    $runner=@'
import sys
try:
    sys.path.insert(0,sys.argv[1])
    import native_media_ports
    control=None if sys.argv[3]=='current' else int(sys.argv[3])
    hls=None if sys.argv[4]=='current' else int(sys.argv[4])
    print(native_media_ports.execute_locked(sys.argv[2],control,hls,sys.argv[5]=='apply',sys.argv[5]=='recover'))
except Exception:
    raise SystemExit('Native media operation failed. Check protected backups and service diagnostics.')
'@
    & $python -c $runner $PSScriptRoot $resolvedProfile $controlArgument $hlsArgument $mode
    if ($LASTEXITCODE -ne 0) { throw 'Native media operation failed; inspect protected backups before retrying.' }
} finally { $lock.Dispose() }
