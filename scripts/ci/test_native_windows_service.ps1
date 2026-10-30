[CmdletBinding()]
param([switch]$CompileOnly)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$nonce=[Guid]::NewGuid().ToString('N')
$name='ScadivoPortProof'+$nonce
$root=Join-Path ([IO.Path]::GetTempPath()) ('scadivo-scm-proof-'+$nonce)
$report=Join-Path ([IO.Path]::GetTempPath()) ('scadivo-scm-proof-'+$nonce+'.json')
$created=$false; $failure=$null; $cleanupFailure=$null; $checks=@()
$principal=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $CompileOnly -and -not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Open Windows PowerShell as administrator, then run this script.' }
[void][IO.Directory]::CreateDirectory($root)
function Free-Port {
    $probe=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    try { $probe.Start(); return $probe.LocalEndpoint.Port } finally { $probe.Stop() }
}
function Check-Health([int]$Port) {
    for ($attempt=0;$attempt -lt 30;$attempt++) {
        try {
            $result=Invoke-RestMethod -Uri "http://127.0.0.1:$Port/health" -TimeoutSec 2 -MaximumRedirection 0
            if ($result.status -eq 'ok' -and $result.service -eq 'scada-api') { return }
        } catch { }
        Start-Sleep -Seconds 1
    }
    throw "Fixture listener not ready on port $Port."
}
try {
    $exe=Join-Path $root 'NativePortProofService.exe'
    Add-Type -Path (Join-Path $PSScriptRoot 'NativePortProofService.cs') -ReferencedAssemblies System.ServiceProcess,System.Web.Extensions -OutputAssembly $exe -OutputType ConsoleApplication
    $checks+='Fixture compilation'
    if (-not $CompileOnly) {
        $ports=@()
        while ($ports.Count -lt 4) { $candidate=Free-Port; if ($candidate -notin $ports) { $ports += $candidate } }
        $config=Join-Path $root 'app.json'; $profile=Join-Path $root 'native-network-profile.json'
        $original=@{Kestrel=@{Endpoints=@{Web=@{Url="http://127.0.0.1:$($ports[0])"};Api=@{Url="http://127.0.0.1:$($ports[1])"}}};proof='preserve'} | ConvertTo-Json -Depth 8
        [IO.File]::WriteAllText($config,$original)
        $binary='"'+$exe+'" "'+$name+'" "'+$config+'"'
        New-Service -Name $name -BinaryPathName $binary -StartupType Manual -DisplayName $name | Out-Null
        $created=$true
        Start-Service -Name $name
        Check-Health $ports[0]; Check-Health $ports[1]
        $checks+='Real SCM start and two loopback listeners'
        $installation=Join-Path $repository 'web/scada-web/public/installation'
        & (Join-Path $installation 'provision-native-network-profile.ps1') -ConfigurationPath $config -ServiceName $name -DestinationPath $profile
        $adapter=Join-Path $installation 'apply-native-network-profile.ps1'
        & $adapter -ProfilePath $profile -WebPort $ports[2] -ApiPort $ports[3]
        if ([IO.File]::ReadAllText($config) -ne $original) { throw 'Plan mutated configuration.' }
        & $adapter -ProfilePath $profile -WebPort $ports[2] -ApiPort $ports[3] -Apply
        Check-Health $ports[2]; Check-Health $ports[3]
        $checks+='Real SCM restart with independent port changes'
        $changed=Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
        if ($changed.proof -ne 'preserve') { throw 'Configuration value lost.' }
        $backup=Join-Path $root 'app.json.network-backup-proof-original'
        & (Join-Path $installation 'copy-native-protected-backup.ps1') -SourcePath $config -DestinationPath $backup -CreateEmpty
        [IO.File]::WriteAllText($backup,$original)
        $journal="$config.access-port-pending.json"
        & (Join-Path $installation 'copy-native-protected-backup.ps1') -SourcePath $config -DestinationPath $journal -CreateEmpty
        [IO.File]::WriteAllText($journal,(@{schema='scadivo-access-port-recovery-v1';backup=$backup} | ConvertTo-Json))
        & $adapter -ProfilePath $profile -Recover
        Check-Health $ports[0]; Check-Health $ports[1]
        if ((Test-Path -LiteralPath $journal) -or [IO.File]::ReadAllText($config) -ne $original) { throw 'Recovery did not restore original configuration.' }
        $checks+='Explicit recovery with real SCM restart'
    }
} catch { $failure=$_.Exception.Message }
finally {
    try {
        if ($created) {
            $service=Get-Service -Name $name -ErrorAction Stop
            if ($service.Status -ne 'Stopped') { Stop-Service -Name $name -Force; $service.WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30)) }
            & sc.exe delete $name | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Temporary service deletion failed.' }
            $service.Dispose()
        }
        $expected=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('scadivo-scm-proof-'+$nonce)))
        if ([IO.Path]::GetFullPath($root) -ne $expected) { throw 'Unsafe cleanup target.' }
        Remove-Item -LiteralPath $root -Recurse -Force
    } catch { $cleanupFailure=$_.Exception.Message }
    @{schema='scadivo-native-scm-proof-v1';mode=$(if($CompileOnly){'compile-only'}else{'real-scm-fixture'});checks=$checks;passed=($null -eq $failure -and $null -eq $cleanupFailure);failure=$failure;cleanupFailure=$cleanupFailure;service=$name;productHostValidated=$false;previewChanged=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $report -Encoding UTF8
    Write-Output "Report: $report"
}
if ($failure -or $cleanupFailure) { throw 'Native SCM proof failed. See the report; do not repeat before diagnosis.' }
Write-Output 'PASS. This validates the adapter against a disposable Windows service, not the complete SCADIVO Windows host.'
