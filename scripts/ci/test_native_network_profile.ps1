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
 $backups=@(Get-ChildItem -LiteralPath $root -Filter 'appsettings.json.network-backup-*')
 if ($backups.Count -ne 1) { throw 'Expected one protected backup.' }
 $acl=Get-Acl -LiteralPath $backups[0].FullName
 if (-not $acl.AreAccessRulesProtected) { throw 'Backup inherited directory permissions.' }
 $allowed=@([Security.Principal.WindowsIdentity]::GetCurrent().User.Value,'S-1-5-18','S-1-5-32-544')
 foreach ($rule in $acl.Access) {
   $sid=$rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
   if ($rule.IsInherited -or $sid -notin $allowed) { throw 'Unexpected backup access rule.' }
 }
 if ([IO.File]::ReadAllText($backups[0].FullName) -ne $original) { throw 'Backup content differs.' }
 $copyHelper=Join-Path (Get-Location) 'web/scada-web/public/installation/copy-native-protected-backup.ps1'
 $overwriteRejected=$false
 try { & $copyHelper -SourcePath $config -DestinationPath $backups[0].FullName } catch { $overwriteRejected=$true }
 if (-not $overwriteRejected -or [IO.File]::ReadAllText($backups[0].FullName) -ne $original) { throw 'Existing protected backup was overwritten.' }
 [IO.File]::WriteAllText($config,$original)
 $global:NativeRestartCount=0; $global:NativeForceFailure=$true; $failed=$false
 try { & $script -ProfilePath $profile -WebPort $port -Apply } catch { $failed=$true }
 if (-not $failed -or [IO.File]::ReadAllText($config) -ne $original -or $global:NativeRestartCount -ne 2) { throw 'Rollback failed' }
 $dual='{"Kestrel":{"Endpoints":{"Web":{"Url":"http://127.0.0.1:18080"},"Api":{"Url":"http://127.0.0.1:15120"}}},"secret":"preserve-me"}'
 [IO.File]::WriteAllText($config,$dual)
 @{schema='scadivo-native-network-v1';platform='windows';serviceName='ScadivoTest';configurationFile=$config;endpointName='Web';apiEndpointName='Api'} | ConvertTo-Json | Set-Content -LiteralPath $profile
 $global:NativeForceFailure=$false; $global:NativeRestartCount=0
 & $script -ProfilePath $profile -WebPort $port -Apply
 $changed=Get-Content -Raw -LiteralPath $config|ConvertFrom-Json
 if (([Uri]$changed.Kestrel.Endpoints.Web.Url).Port -ne $port -or ([Uri]$changed.Kestrel.Endpoints.Api.Url).Port -ne 15120) { throw 'Web change affected independent API port.' }
 [IO.File]::WriteAllText($config,$dual)
 & $script -ProfilePath $profile -ApiPort $port -Apply
 $changed=Get-Content -Raw -LiteralPath $config|ConvertFrom-Json
 if (([Uri]$changed.Kestrel.Endpoints.Web.Url).Port -ne 18080 -or ([Uri]$changed.Kestrel.Endpoints.Api.Url).Port -ne $port) { throw 'API change affected independent Web port.' }
 [IO.File]::WriteAllText($config,$dual)
 $global:NativeRestartCount=0; $global:NativeForceFailure=$true; $failed=$false
 try { & $script -ProfilePath $profile -ApiPort $port -Apply } catch { $failed=$true }
 if (-not $failed -or [IO.File]::ReadAllText($config) -ne $dual -or $global:NativeRestartCount -ne 2) { throw 'Independent listener rollback failed.' }
 $global:NativeForceFailure=$false; $global:NativeRestartCount=0; $failed=$false
 try { & $script -ProfilePath $profile -WebPort 28080 -ApiPort 28080 -Apply } catch { $failed=$true }
 if (-not $failed -or $global:NativeRestartCount -ne 0) { throw 'Listener collision was not rejected.' }
 # Simulate a stopped administrative process with a persisted recovery record.
 $backup=Join-Path $root 'appsettings.json.network-backup-interrupted'
 & $copyHelper -SourcePath $config -DestinationPath $backup
 $journal="$config.access-port-pending.json"
 & $copyHelper -SourcePath $config -DestinationPath $journal -CreateEmpty
 @{schema='scadivo-access-port-recovery-v1';backup=$backup} | ConvertTo-Json | Set-Content -LiteralPath $journal
 [IO.File]::WriteAllText($config,'{}')
 $global:NativeRestartCount=0; $failed=$false
 try { & $script -ProfilePath $profile -WebPort 28080 -Apply } catch { $failed=$true }
 if (-not $failed -or $global:NativeRestartCount -ne 0) { throw 'Pending recovery did not block a new change.' }
 $global:NativeForceFailure=$true; $failed=$false
 try { & $script -ProfilePath $profile -Recover } catch { $failed=$true }
 if (-not $failed -or -not (Test-Path -LiteralPath $journal)) { throw 'Failed recovery lost its journal.' }
 $global:NativeForceFailure=$false
 & $script -ProfilePath $profile -Recover
 if ((Test-Path -LiteralPath $journal) -or [IO.File]::ReadAllText($config) -ne $dual) { throw 'Explicit recovery failed.' }
 foreach ($operation in @('database','media')) {
   $other="$config.$operation-port-pending.json"
   [IO.File]::WriteAllText($other,'pending-other-operation')
   $before=$global:NativeRestartCount; $failed=$false
   try { & $script -ProfilePath $profile -ApiPort $port -Apply } catch { $failed=$true }
   if (-not $failed -or $global:NativeRestartCount -ne $before) { throw 'Another pending operation did not block Web/API apply.' }
   $failed=$false
   try { & $script -ProfilePath $profile -Recover } catch { $failed=$true }
   if (-not $failed -or $global:NativeRestartCount -ne $before) { throw 'Another pending operation did not block Web/API recovery.' }
   Remove-Item -LiteralPath $other
 }
 'PASS: defaults, plan-only, apply, protected backups/journals, explicit and cross-operation recovery with simulated service/health.'
} finally {
 $resolved=[IO.Path]::GetFullPath($root)
 if (-not $resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()))) { throw 'Unsafe cleanup target' }
 [IO.Directory]::Delete($resolved,$true)
 Remove-Variable NativeRestartCount,NativeForceFailure -Scope Global -ErrorAction SilentlyContinue
}
