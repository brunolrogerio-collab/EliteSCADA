[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProfilePath,
    [Parameter(Mandatory=$true)][string]$PythonExecutable,
    [ValidateRange(1,65535)][int]$DatabasePort,
    [switch]$Apply,
    [switch]$Recover,
    [switch]$VerifyAuthenticated
)
$ErrorActionPreference='Stop'
$resolvedProfile=(Resolve-Path -LiteralPath $ProfilePath).Path
$profile=Get-Content -LiteralPath $resolvedProfile -Raw | ConvertFrom-Json
if ($profile.schema -ne 'scadivo-native-network-v1' -or $profile.platform -ne 'windows') { throw 'Registered Windows native profile required.' }
$config=[IO.Path]::GetFullPath($profile.configurationFile)
if (-not (Test-Path -LiteralPath $config -PathType Leaf)) { throw 'Registered configuration is missing.' }
$python=(Resolve-Path -LiteralPath $PythonExecutable).Path
$module=Join-Path $PSScriptRoot 'native_database_port.py'
if (-not (Test-Path -LiteralPath $module -PathType Leaf)) { throw 'Native database adapter payload is missing.' }
$lock=[IO.File]::Open("$config.network.lock",[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    $portArgument='current'
    if ($PSBoundParameters.ContainsKey('DatabasePort')) { $portArgument=[string]$DatabasePort }
    $applyArgument='plan'
    if ($Apply) { $applyArgument='apply' }
    if ($Recover) {
        if ($Apply -or $PSBoundParameters.ContainsKey('DatabasePort')) { throw 'Recovery cannot be combined with a port change.' }
        $applyArgument='recover'
    }
    $runner=@'
import importlib.util, sys
try:
    spec=importlib.util.spec_from_file_location('native_database_port',sys.argv[1])
    module=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    port=None if sys.argv[3]=='current' else int(sys.argv[3])
    verify=sys.argv[5]=='verify'
    print(module.recover_locked(sys.argv[2],verify) if sys.argv[4]=='recover' else module.execute_locked(sys.argv[2],port,sys.argv[4]=='apply',verify))
except Exception:
    raise SystemExit('Native database operation failed. Check protected backups and service diagnostics.')
'@
    $verification='none'; if ($VerifyAuthenticated) { $verification='verify' }
    & $python -c $runner $module $resolvedProfile $portArgument $applyArgument $verification
    if ($LASTEXITCODE -ne 0) { throw 'Native database operation failed; inspect protected backups before retrying.' }
} finally { $lock.Dispose() }
