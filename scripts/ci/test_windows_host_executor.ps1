[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$nonce=[Guid]::NewGuid().ToString('N')
$root=Join-Path ([IO.Path]::GetTempPath()) ('scadivo-host-pipe-'+$nonce)
$name='ScadivoHostPipe'+$nonce
$pipeName='scadivo-host-'+$nonce
$subject=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$who=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $who.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Windows SCM integration test requires administrator.' }
$trusted=@($subject,'S-1-5-18','S-1-5-32-544')
$created=$false
$hostProcess=$null

function Lock-Directory([string]$path) {
  $acl=New-Object Security.AccessControl.DirectorySecurity
  $acl.SetAccessRuleProtection($true,$false)
  foreach($sid in $trusted) {
    $rule=New-Object Security.AccessControl.FileSystemAccessRule(
      (New-Object Security.Principal.SecurityIdentifier($sid)),
      [Security.AccessControl.FileSystemRights]::FullControl,
      ([Security.AccessControl.InheritanceFlags]::ObjectInherit -bor [Security.AccessControl.InheritanceFlags]::ContainerInherit),
      [Security.AccessControl.PropagationFlags]::None,
      [Security.AccessControl.AccessControlType]::Allow)
    $acl.AddAccessRule($rule)
  }
  [IO.Directory]::SetAccessControl($path,$acl)
}
function Lock-File([string]$path) {
  $acl=New-Object Security.AccessControl.FileSecurity
  $acl.SetAccessRuleProtection($true,$false)
  foreach($sid in $trusted) {
    $rule=New-Object Security.AccessControl.FileSystemAccessRule(
      (New-Object Security.Principal.SecurityIdentifier($sid)),
      [Security.AccessControl.FileSystemRights]::FullControl,
      [Security.AccessControl.AccessControlType]::Allow)
    $acl.AddAccessRule($rule)
  }
  [IO.File]::SetAccessControl($path,$acl)
}
function Free-Port {
  $p=New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback,0)
  try { $p.Start(); return $p.LocalEndpoint.Port } finally { $p.Stop() }
}
function Wait-Health([int]$port) {
  for($i=0;$i -lt 50;$i++){
    try {
      $h=Invoke-RestMethod -Uri "http://127.0.0.1:$port/health" -TimeoutSec 1
      if($h.status -eq 'ok' -and $h.service -eq 'scada-api') { return }
    } catch {}
    Start-Sleep -Milliseconds 200
  }
  throw 'Registered SCM listener did not pass health verification.'
}
function Pipe-Rpc([string]$method,[string]$route,$data,[string]$credential) {
  $body=''
  if($null -ne $data) { $body=ConvertTo-Json -InputObject $data -Depth 8 -Compress }
  $nl=[string][char]13+[char]10
  $bodyBytes=[Text.Encoding]::UTF8.GetBytes($body)
  $header=$method+' '+$route+' HTTP/1.1'+$nl+'Host: localhost'+$nl+
    'X-Scadivo-Installation-Token: '+$credential+$nl+
    'Content-Length: '+$bodyBytes.Length+$nl+'Connection: close'+$nl+$nl
  $bytes=[Text.Encoding]::UTF8.GetBytes($header+$body)
  $pipe=New-Object IO.Pipes.NamedPipeClientStream('.',$pipeName,[IO.Pipes.PipeDirection]::InOut)
  try {
    $pipe.Connect(5000)
    $pipe.Write($bytes,0,$bytes.Length)
    $pipe.Flush()
    $reader=New-Object IO.StreamReader($pipe,[Text.Encoding]::UTF8)
    $raw=$reader.ReadToEnd()
    $parts=$raw.Split(@($nl+$nl),[StringSplitOptions]::None)
    if($parts.Length -lt 2) { throw 'Invalid executor HTTP response' }
    $status=[int]([regex]::Match($parts[0],'^HTTP/1.1 ([0-9]{3})').Groups[1].Value)
    return [pscustomobject]@{status=$status;body=($parts[1]|ConvertFrom-Json)}
  } finally { $pipe.Dispose() }
}
function Await-State([string]$wanted,[string]$credential) {
  for($i=0;$i -lt 300;$i++){
    $r=Pipe-Rpc GET /v1/status $null $credential
    if($r.status -eq 200 -and $r.body.operation.state -eq $wanted) { return $r.body }
    Start-Sleep -Milliseconds 200
  }
  throw ('Durable executor state not reached: '+$wanted)
}

try {
  [void][IO.Directory]::CreateDirectory($root)
  Lock-Directory $root
  $adapters=Join-Path $root 'adapters'; $state=Join-Path $root 'state'
  [void][IO.Directory]::CreateDirectory($adapters)
  [void][IO.Directory]::CreateDirectory($state)
  Lock-Directory $adapters; Lock-Directory $state
  $installation=Join-Path $repo 'web/scada-web/public/installation'
  foreach($file in @('apply-native-network-profile.ps1','copy-native-protected-backup.ps1')) {
    $dst=Join-Path $adapters $file
    Copy-Item -LiteralPath (Join-Path $installation $file) -Destination $dst
    Lock-File $dst
  }
  $exe=Join-Path $root 'ScmProof.exe'
  Add-Type -Path (Join-Path $PSScriptRoot 'NativePortProofService.cs') -ReferencedAssemblies System.ServiceProcess,System.Web.Extensions -OutputAssembly $exe -OutputType ConsoleApplication
  Lock-File $exe
  $ports=@()
  while($ports.Count -lt 6) { $p=Free-Port; if($p -notin $ports){$ports+= $p} }
  $config=Join-Path $root 'appsettings.json'
  $profile=Join-Path $root 'native-profile.json'
  $registration=Join-Path $root 'executor.json'
  $tokenFile=Join-Path $root 'executor.token'
  $original=@{Kestrel=@{Endpoints=@{Web=@{Url="http://127.0.0.1:$($ports[0])"};Api=@{Url="http://127.0.0.1:$($ports[1])"}}};preserve='yes'} | ConvertTo-Json -Depth 8
  [IO.File]::WriteAllText($config,$original); Lock-File $config
  $binary=[char]34+$exe+[char]34+' '+[char]34+$name+[char]34+' '+[char]34+$config+[char]34
  New-Service -Name $name -BinaryPathName $binary -StartupType Manual | Out-Null
  $created=$true
  Start-Service -Name $name
  Wait-Health $ports[0]; Wait-Health $ports[1]
  [IO.File]::WriteAllText($profile,(@{schema='scadivo-native-network-v1';platform='windows';serviceName=$name;configurationFile=$config;endpointName='Web';apiEndpointName='Api'}|ConvertTo-Json))
  Lock-File $profile
  $token=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
  [IO.File]::WriteAllText($tokenFile,$token); Lock-File $tokenFile
  [IO.File]::WriteAllText($registration,(@{
    schema='scadivo-network-host-executor-v1';profile='native-windows';instanceId='ci-host';
    pipeName=$pipeName;apiSid=$subject;stateDirectory=$state;nativeProfilePath=$profile;
    tokenFile=$tokenFile;adapterDirectory=$adapters
  }|ConvertTo-Json))
  Lock-File $registration
  $assembly=Join-Path $repo 'tools/Scadivo.InstallationExecutor.Windows/bin/Release/net10.0-windows/Scadivo.InstallationExecutor.Windows.dll'
  if(-not (Test-Path -LiteralPath $assembly)) { throw 'Windows executor build output missing' }
  $hostProcess=Start-Process -FilePath 'dotnet' -ArgumentList @([char]34+$assembly+[char]34,'--registration',[char]34+$registration+[char]34) -PassThru -WindowStyle Hidden
  $ready=$false
  for($i=0;$i -lt 60;$i++){
    if($hostProcess.HasExited){ throw 'Windows host executor process exited during registration.' }
    try {
      $s=Pipe-Rpc GET /v1/status $null $token
      if($s.status -eq 200 -and $s.body.available){$ready=$true;break}
    } catch {}
    Start-Sleep -Milliseconds 200
  }
  if(-not $ready){throw 'Windows named pipe never became ready'}
  $bad=Pipe-Rpc GET /v1/status $null 'bad-token'
  if($bad.status -ne 403){throw 'Wrong executor token was not rejected'}
  $portsNew=@{webPort=[int]$ports[2];apiPort=[int]$ports[3]}
  $before=[IO.File]::ReadAllText($config)
  $plan=Pipe-Rpc POST /v1/preflight @{schema='scadivo-network-port-plan-v1';module='access';ports=$portsNew} $token
  if($plan.status -ne 200 -or [IO.File]::ReadAllText($config) -ne $before){throw 'Preflight did not remain non-mutating'}
  $apply=Pipe-Rpc POST /v1/apply @{schema='scadivo-network-port-plan-v1';planId=$plan.body.planId;confirm=$true} $token
  if($apply.status -ne 202){throw 'Executor rejected confirmed apply'}
  $after=Await-State applied $token
  Wait-Health $ports[2];Wait-Health $ports[3]
  if($after.configuredPorts.webPort -ne $ports[2]){throw 'SCM service did not use applied port'}
  $occupied=New-Object Net.Sockets.TcpListener([Net.IPAddress]::Any,[int]$ports[4])
  $occupied.Start()
  try {
    $denied=Pipe-Rpc POST /v1/preflight @{schema='scadivo-network-port-plan-v1';module='access';ports=@{webPort=[int]$ports[4];apiPort=[int]$ports[5]}} $token
    if($denied.status -eq 200){throw 'Occupied host port was accepted'}
    Wait-Health $ports[2];Wait-Health $ports[3]
  } finally {$occupied.Stop()}
  $pre=Pipe-Rpc POST /v1/preflight @{schema='scadivo-network-port-plan-v1';module='access';ports=@{webPort=[int]$ports[4];apiPort=[int]$ports[5]}} $token
  if($pre.status -ne 200){throw 'Recovery proof preflight failed'}
  $occupied.Start()
  try {
    $broken=Pipe-Rpc POST /v1/apply @{schema='scadivo-network-port-plan-v1';planId=$pre.body.planId;confirm=$true} $token
    if($broken.status -ne 202){throw 'Operation failed before durable acceptance'}
    [void](Await-State recovery_required $token)
  } finally {$occupied.Stop()}
  $recovery=Pipe-Rpc POST /v1/recover @{schema='scadivo-network-port-plan-v1';confirm=$true} $token
  if($recovery.status -ne 202){throw 'Recovery rejected'}
  [void](Await-State recovered $token)
  Wait-Health $ports[2];Wait-Health $ports[3]
  Write-Host 'PASS: Windows executor Named Pipe authentication/ACL, real SCM apply, preflight conflict, durable failure and recovery.'
} finally {
  if($null -ne $hostProcess){
    try { if(-not $hostProcess.HasExited){Stop-Process -Id $hostProcess.Id -Force -ErrorAction SilentlyContinue} } catch {}
  }
  if($created) {
    try {
      $svc=Get-Service -Name $name -ErrorAction SilentlyContinue
      if($null -ne $svc -and $svc.Status -ne 'Stopped'){Stop-Service -Name $name -Force -ErrorAction SilentlyContinue}
      & sc.exe delete $name | Out-Null
    } catch {}
  }
  if((Test-Path -LiteralPath $root) -and $root -eq (Join-Path ([IO.Path]::GetTempPath()) ('scadivo-host-pipe-'+$nonce))) {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
  }
}
