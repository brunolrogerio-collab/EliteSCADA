[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$nonce=[Guid]::NewGuid().ToString('N')
$root=Join-Path ([IO.Path]::GetTempPath()) ('scadivo-docker-win-executor-'+$nonce)
$subject=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$trusted=@($subject,'S-1-5-18','S-1-5-32-544')
$process=$null
$cert=$null

function Lock-Directory([string]$path){
  $acl=New-Object Security.AccessControl.DirectorySecurity
  $acl.SetAccessRuleProtection($true,$false)
  foreach($sid in $trusted){
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
function Lock-File([string]$path){
  $acl=New-Object Security.AccessControl.FileSecurity
  $acl.SetAccessRuleProtection($true,$false)
  foreach($sid in $trusted){
    $rule=New-Object Security.AccessControl.FileSystemAccessRule(
      (New-Object Security.Principal.SecurityIdentifier($sid)),
      [Security.AccessControl.FileSystemRights]::FullControl,
      [Security.AccessControl.AccessControlType]::Allow)
    $acl.AddAccessRule($rule)
  }
  [IO.File]::SetAccessControl($path,$acl)
}
function Free-Port{
  $listener=New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback,0)
  try{$listener.Start();return [int]$listener.LocalEndpoint.Port}finally{$listener.Stop()}
}
function Tls-Rpc([string]$method,[string]$route,[string]$token,$body=$null){
  $client=New-Object Net.Sockets.TcpClient
  $client.Connect('127.0.0.1',$port)
  $validator=[Net.Security.RemoteCertificateValidationCallback]{
    param($sender,$candidate,$chain,$errors)
    if($null -eq $candidate){return $false}
    $sha=[Security.Cryptography.SHA256]::Create()
    try{
      $hash=([BitConverter]::ToString($sha.ComputeHash($candidate.GetRawCertData()))).Replace('-','')
      return [string]::Equals($hash,$certHash,[StringComparison]::OrdinalIgnoreCase)
    }finally{$sha.Dispose()}
  }
  $tls=New-Object Net.Security.SslStream($client.GetStream(),$false,$validator)
  try{
    $tls.AuthenticateAsClient('host.docker.internal')
    $payload=''
    if($null -ne $body){$payload=ConvertTo-Json -InputObject $body -Compress -Depth 8}
    $nl=[string][char]13+[char]10
    $bytesBody=[Text.Encoding]::UTF8.GetBytes($payload)
    $raw=$method+' '+$route+' HTTP/1.1'+$nl+'Host: host.docker.internal'+$nl+
      'X-Scadivo-Installation-Token: '+$token+$nl+'Content-Type: application/json'+$nl+
      'Content-Length: '+$bytesBody.Length+$nl+'Connection: close'+$nl+$nl+$payload
    $bytes=[Text.Encoding]::UTF8.GetBytes($raw)
    $tls.Write($bytes,0,$bytes.Length);$tls.Flush()
    $reader=New-Object IO.StreamReader($tls,[Text.Encoding]::UTF8)
    $response=$reader.ReadToEnd()
    $parts=$response.Split(@($nl+$nl),[StringSplitOptions]::None)
    if($parts.Length -lt 2){throw 'Invalid host TLS response.'}
    $httpStatus=[int]([regex]::Match($parts[0],'^HTTP/1.1 ([0-9]{3})').Groups[1].Value)
    return [pscustomobject]@{Status=$httpStatus;Body=($parts[1]|ConvertFrom-Json)}
  }finally{$tls.Dispose();$client.Dispose()}
}
try{
  [void][IO.Directory]::CreateDirectory($root)
  Lock-Directory $root
  $install=Join-Path $root 'installed'
  $state=Join-Path $root 'state'
  $adapters=Join-Path $root 'adapters'
  [void][IO.Directory]::CreateDirectory($install)
  [void][IO.Directory]::CreateDirectory($state)
  [void][IO.Directory]::CreateDirectory($adapters)
  Lock-Directory $install;Lock-Directory $state;Lock-Directory $adapters
  $nested=Join-Path $install 'docker'
  $nestedStable=Join-Path $nested 'stable'
  [void][IO.Directory]::CreateDirectory($nested);[void][IO.Directory]::CreateDirectory($nestedStable)
  Lock-Directory $nested;Lock-Directory $nestedStable
  $adapter=Join-Path $adapters 'apply-docker-host-network-profile.ps1'
  Copy-Item -LiteralPath (Join-Path $repo 'web/scada-web/public/installation/apply-docker-host-network-profile.ps1') -Destination $adapter
  Lock-File $adapter
  $compose=Join-Path $install 'docker-compose.stable.yml'
  $overlay=Join-Path $nestedStable 'compose-host-executor.windows.override.yml'
  $envFile=Join-Path $install '.env'
  $ports=@()
  while($ports.Count -lt 7){$p=Free-Port;if($p -notin $ports){$ports+= $p}}
  $port=[int]$ports[6]
  [IO.File]::WriteAllText($compose,"services:\n  web: {}\n  api: {}\n  database: {}\n  media-gateway: {}\n")
  [IO.File]::WriteAllText($overlay,"services:\n  api: {}\n")
  $initial=@("SCADIVO_INSTALLATION_ID=win-docker-ci","SCADIVO_WEB_BIND=127.0.0.1",
    "SCADIVO_API_BIND=127.0.0.1","ELITESCADA_WEB_PORT=$($ports[0])",
    "ELITESCADA_API_PORT=$($ports[1])","ELITESCADA_DB_PORT=$($ports[2])",
    "SCADIVO_MEDIA_CONTROL_PORT=$($ports[3])","SCADIVO_MEDIA_PLAYBACK_PORT=$($ports[4])",
    "ELITESCADA_DB_PASSWORD=never-print-this-in-ci")
  [IO.File]::WriteAllLines($envFile,$initial)
  foreach($p in @($compose,$overlay,$envFile)){Lock-File $p}
  $docker=Join-Path $root 'docker-probe.exe'
  $mock=@'
using System;
using System.Linq;
public static class DockerProbe {
  public static int Main(string[] args) {
    if (args.Length < 3 || args[0] != "--context" || args[1] != "desktop-linux") return 10;
    if (args.Contains("config") && args.Contains("--services")) {
      Console.WriteLine("web");Console.WriteLine("api");
      Console.WriteLine("database");Console.WriteLine("media-gateway");return 0;
    }
    if (args.Contains("config") || args.Contains("ps") || args.Contains("up") || args.Contains("exec")) return 0;
    return 12;
  }
}
'@
  Add-Type -TypeDefinition $mock -OutputAssembly $docker -OutputType ConsoleApplication
  Lock-File $docker
  $profile=Join-Path $state 'docker-profile.json'
  $registration=Join-Path $state 'docker-executor-registration.json'
  $tokenFile=Join-Path $state 'token'
  $pfx=Join-Path $state 'server.pfx'
  $pfxPassword=Join-Path $state 'server-password'
  $cert=New-SelfSignedCertificate -DnsName 'host.docker.internal' -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy Exportable -NotAfter (Get-Date).AddDays(1)
  $password=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
  $secure=ConvertTo-SecureString -String $password -AsPlainText -Force
  Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $secure | Out-Null
  [IO.File]::WriteAllText($pfxPassword,$password)
  Lock-File $pfx;Lock-File $pfxPassword
  $sha=[Security.Cryptography.SHA256]::Create()
  try{$certHash=([BitConverter]::ToString($sha.ComputeHash($cert.RawData))).Replace('-','')}finally{$sha.Dispose()}
  $token=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
  [IO.File]::WriteAllText($tokenFile,$token)
  Lock-File $tokenFile
  $details=@{
    schema='scadivo-docker-host-network-v1';instanceId='win-docker-ci';
    installationDirectory=$install;dockerExecutable=$docker;
    composeSha256=(Get-FileHash -LiteralPath $compose -Algorithm SHA256).Hash.ToLowerInvariant();
    composeOverrideSha256=(Get-FileHash -LiteralPath $overlay -Algorithm SHA256).Hash.ToLowerInvariant()
  }
  [IO.File]::WriteAllText($profile,($details|ConvertTo-Json))
  Lock-File $profile
  $reg=@{
    schema='scadivo-network-host-executor-v1';profile='stable-docker-windows';
    instanceId='win-docker-ci';pipeName='scadivo-docker-win-'+$nonce;
    apiSid=$subject;stateDirectory=$state;nativeProfilePath=$profile;tokenFile=$tokenFile;
    adapterDirectory=$adapters;httpsListenAddress='127.0.0.1';httpsListenPort=$port;
    httpsCertificatePfx=$pfx;httpsCertificatePasswordFile=$pfxPassword
  }
  [IO.File]::WriteAllText($registration,($reg|ConvertTo-Json))
  Lock-File $registration
  $assembly=Join-Path $repo 'tools/Scadivo.InstallationExecutor.Windows/bin/Release/net10.0-windows/Scadivo.InstallationExecutor.Windows.dll'
  if(-not (Test-Path -LiteralPath $assembly)){throw 'Windows host executor assembly missing.'}
  $process=Start-Process -FilePath 'dotnet' -ArgumentList @([char]34+$assembly+[char]34,'--registration',[char]34+$registration+[char]34) -PassThru -WindowStyle Hidden
  $ready=$false
  for($i=0;$i -lt 80;$i++){
    if($process.HasExited){throw 'Protected Docker Windows host process exited.'}
    try{
      $status=Tls-Rpc GET /v1/status $token
      if($status.Status -eq 200 -and $status.Body.available){$ready=$true;break}
    }catch{}
    Start-Sleep -Milliseconds 200
  }
  if(-not $ready){throw 'Pinned TLS Windows host did not become available.'}
  if($status.Body.profile -ne 'stable-docker-windows'){throw 'Wrong Windows Docker host profile.'}
  if((Tls-Rpc GET /v1/status 'invalid-token').Status -ne 403){throw 'Bad TLS host token was not rejected.'}
  $before=[IO.File]::ReadAllBytes($envFile)
  $desired=@{webPort=[int]$ports[0];apiPort=[int]$ports[1]}
  $plan=Tls-Rpc POST /v1/preflight $token @{
    schema='scadivo-network-port-plan-v1';module='access';ports=$desired
  }
  if($plan.Status -ne 200 -or -not $plan.Body.planId){throw 'Registered Docker Desktop preflight rejected.'}
  if([Convert]::ToBase64String($before) -cne [Convert]::ToBase64String([IO.File]::ReadAllBytes($envFile))) {
    throw 'Registered Docker Desktop preflight modified .env.'
  }
  $busy=New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback,[int]$ports[5])
  $busy.Start()
  try{
    $conflict=Tls-Rpc POST /v1/preflight $token @{
      schema='scadivo-network-port-plan-v1';module='access';
      ports=@{webPort=[int]$ports[5];apiPort=[int]$ports[1]}
    }
    if($conflict.Status -eq 200){throw 'Busy Docker Desktop published port accepted.'}
  }finally{$busy.Stop()}
  [IO.File]::WriteAllText($overlay,"services:\n  api:\n    privileged: true\n")
  Lock-File $overlay
  $tampered=Tls-Rpc GET /v1/status $token
  if($tampered.Status -ne 200 -or $tampered.Body.available){throw 'Modified Compose overlay did not fail closed.'}
  Write-Host 'PASS: Windows Docker Desktop host pinned TLS, token, registered Compose digest, non-mutating preflight, busy port rejection.'
}finally{
  if($null -ne $process){
    try{if(-not $process.HasExited){Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue}}catch{}
  }
  if($null -ne $cert){
    try{Remove-Item -LiteralPath ('Cert:\CurrentUser\My\'+$cert.Thumbprint) -Force -ErrorAction SilentlyContinue}catch{}
  }
  if((Test-Path -LiteralPath $root) -and $root -eq (Join-Path ([IO.Path]::GetTempPath()) ('scadivo-docker-win-executor-'+$nonce))){
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
  }
}
