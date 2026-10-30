$ErrorActionPreference='Stop'
$root=Join-Path ([IO.Path]::GetTempPath()) ('scadivo-native-network-test-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
function Get-Service { param($Name,$ErrorAction) [pscustomobject]@{Status='Running'} }
function Restart-Service { param($Name,$ErrorAction) $global:NativeRestartCount++ }
function Invoke-WebRequest { param($Uri,[switch]$UseBasicParsing,$TimeoutSec,$MaximumRedirection) if ($global:NativeRestartCount -eq 1 -and $global:NativeForceFailure) { throw 'simulated failure' }; [pscustomobject]@{StatusCode=200} }
function Start-Sleep { param($Seconds) }
try {
 $config=Join-Path $root 'appsettings.json'; $profile=Join-Path $root 'profile.json'
 $original='{"Kestrel":{"Endpoints":{"Http":{"Url":"http://127.0.0.1:18080"}}},"secret":"preserve-me"}'
 [IO.File]::WriteAllText($config,$original)
 @{schema='scadivo-native-network-v1';platform='windows';serviceName='ScadivoTest';configurationFile=$config;endpointName='Http'} | ConvertTo-Json | Set-Content -LiteralPath $profile
 $script=Join-Path (Get-Location) 'web/scada-web/public/installation/apply-native-network-profile.ps1'
 $global:NativeRestartCount=0; $global:NativeForceFailure=$false
 $heldLock = [IO.File]::Open("$config.network.lock", [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
 $rejected=$false
 try {
   try { & $script -ProfilePath $profile -WebPort 28080 -Apply } catch { $rejected=$true }
 } finally { $heldLock.Dispose() }
 if (-not $rejected -or [IO.File]::ReadAllText($config) -ne $original -or $global:NativeRestartCount -ne 0) { throw 'Concurrent operation was not rejected before mutation.' }
 & $script -ProfilePath $profile
 if ([IO.File]::ReadAllText($config) -ne $original -or $global:NativeRestartCount -ne 0) { throw 'Default no-op failed' }
 $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$listener.Start();$port=$listener.LocalEndpoint.Port;$listener.Stop()
 & $script -ProfilePath $profile -WebPort $port
 if ([IO.File]::ReadAllText($config) -ne $original) { throw 'Plan wrote configuration' }
 & $script -ProfilePath $profile -WebPort $port -Apply
 $changed=Get-Content -Raw -LiteralPath $config|ConvertFrom-Json
 if ($changed.secret -ne 'preserve-me' -or ([Uri]$changed.Kestrel.Endpoints.Http.Url).Port -ne $port) { throw 'Apply failed' }
 [IO.File]::WriteAllText($config,$original)
 $global:NativeRestartCount=0; $global:NativeForceFailure=$true; $failed=$false
 try { & $script -ProfilePath $profile -WebPort $port -Apply } catch { $failed=$true }
 if (-not $failed -or [IO.File]::ReadAllText($config) -ne $original -or $global:NativeRestartCount -ne 2) { throw 'Rollback failed' }
 'PASS: defaults, plan-only, apply, secret preservation and recovery with simulated service/health.'
} finally {
 $resolved=[IO.Path]::GetFullPath($root)
 if (-not $resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()))) { throw 'Unsafe cleanup target' }
 [IO.Directory]::Delete($resolved,$true)
 Remove-Variable NativeRestartCount,NativeForceFailure -Scope Global -ErrorAction SilentlyContinue
}