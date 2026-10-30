[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProfilePath,
    [Parameter(Mandatory=$true)][string]$PythonExecutable,
    [ValidateRange(1,65535)][int]$DatabasePort,
    [switch]$Apply
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
    $runner=@'
import importlib.util, sys
try:
    spec=importlib.util.spec_from_file_location('native_database_port',sys.argv[1])
    module=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    port=None if sys.argv[3]=='current' else int(sys.argv[3])
    print(module.execute_locked(sys.argv[2],port,sys.argv[4]=='apply'))
except Exception:
    raise SystemExit('Native database operation failed. Check protected backups and service diagnostics.')
'@
    & $python -c $runner $module $resolvedProfile $portArgument $applyArgument
    if ($LASTEXITCODE -ne 0) { throw 'Native database operation failed; inspect protected backups before retrying.' }
} finally { $lock.Dispose() }
