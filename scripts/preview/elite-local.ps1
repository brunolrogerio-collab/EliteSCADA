[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('help', 'launch', 'prepare', 'start', 'status', 'pause', 'resume', 'stop', 'restart', 'diagnose', 'reset')]
    [string]$Command = 'help',

    [Parameter()]
    [ValidateSet('audit', 'development')]
    [string]$Profile = 'audit',

    [Parameter()]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$AcceptedHarnessSha,

    [Parameter()]
    [ValidateRange(1024, 65535)]
    [int]$Port = 5173,

    [Parameter()]
    [string]$Checkpoint,

    [Parameter()]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$TrustedRootThumbprint,

    [Parameter()]
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if ($Command -eq 'help') {
    @'
EliteSCADA local workbench operator

  launch      Auto-prepare and start/reopen the separate local development workbench.
  prepare     Prepare locked .NET/Web dependencies for the selected profile.
  start       Resume a saved workbench; a new audit workbench requires Main's exact accepted SHA.
  status      Show the selected profile's Git identity, state, dependency key, health and URLs.
  pause       Gracefully stop containers while preserving all product state.
  resume      Resume the exact saved workbench and verify health.
  stop        Friendly alias for pause; never deletes product data.
  restart     Recreate app/database containers while preserving named volumes.
  diagnose    Write a compact, locally stored and redacted diagnostics report.
  reset       Destructive fresh-install boundary; requires -Force.

Examples:
  ./scripts/preview/elite-local.ps1 launch
  ./scripts/preview/elite-local.ps1 status -Profile development
  ./scripts/preview/elite-local.ps1 restart -Profile development
  ./scripts/preview/elite-local.ps1 stop -Profile development
  ./scripts/preview/elite-local.ps1 status
  ./scripts/preview/elite-local.ps1 prepare
  ./scripts/preview/elite-local.ps1 start -AcceptedHarnessSha <sha-from-main>
  ./scripts/preview/elite-local.ps1 pause -Profile development -Checkpoint "Last screen checked"
  ./scripts/preview/elite-local.ps1 resume
  ./scripts/preview/elite-local.ps1 restart
  ./scripts/preview/elite-local.ps1 diagnose
  ./scripts/preview/elite-local.ps1 reset -Force
'@
    return
}

if ($Command -eq 'launch') { $Profile = 'development' }

if ($Command -eq 'reset' -and -not $Force) {
    throw 'RESET_CONFIRMATION_REQUIRED: reset erases the local workbench database/session. Only use -Force after the Product Owner explicitly asks for a fresh install.'
}

$operatorPath = Join-Path $PSScriptRoot 'local-audit.ps1'
$arguments = @{
    Command = $Command
    Profile = $Profile
    Port = $Port
}
if (-not [string]::IsNullOrWhiteSpace($AcceptedHarnessSha)) { $arguments.AcceptedHarnessSha = $AcceptedHarnessSha }
if (-not [string]::IsNullOrWhiteSpace($Checkpoint)) { $arguments.Checkpoint = $Checkpoint }
if (-not [string]::IsNullOrWhiteSpace($TrustedRootThumbprint)) { $arguments.TrustedRootThumbprint = $TrustedRootThumbprint }
if ($Force) { $arguments.Force = $true }

& $operatorPath @arguments
