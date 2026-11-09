[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$ProfilePath,
  [ValidateRange(1,65535)][int]$WebPort,
  [ValidateRange(1,65535)][int]$ApiPort,
  [ValidateRange(1,65535)][int]$DatabasePort,
  [ValidateRange(1,65535)][int]$MediaControlPort,
  [ValidateRange(1,65535)][int]$MediaPlaybackPort,
  [switch]$Apply,
  [switch]$Recover,
  [switch]$Verify
)
$ErrorActionPreference='Stop'
if (([int][bool]$Apply + [int][bool]$Recover + [int][bool]$Verify) -gt 1) { throw 'Only one registered host action may be requested.' }
if (($Recover -or $Verify) -and @(@('WebPort','ApiPort','DatabasePort','MediaControlPort','MediaPlaybackPort') | Where-Object {$PSBoundParameters.ContainsKey($_)}).Count -gt 0) { throw 'Recovery and verification cannot supply new ports.' }
if (-not ($Recover -or $Verify) -and @(@('WebPort','ApiPort','DatabasePort','MediaControlPort','MediaPlaybackPort') | Where-Object {-not $PSBoundParameters.ContainsKey($_)}).Count -gt 0) { throw 'The complete registered Docker network plan is required.' }

$profilePath=[IO.Path]::GetFullPath($ProfilePath)
$profile=Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
$names=@($profile.PSObject.Properties.Name)
$required=@('schema','instanceId','installationDirectory','composeSha256','dockerExecutable')
$optional=@('composeOverrideSha256')
if (@($required|Where-Object {$_ -notin $names}).Count -gt 0 -or @($names|Where-Object {$_ -notin ($required+$optional)}).Count -gt 0 -or $profile.schema -ne 'scadivo-docker-host-network-v1' -or $profile.instanceId -cnotmatch '^[a-z0-9][a-z0-9-]{0,47}$') { throw 'Invalid registered Docker host authority.' }
$id=[string]$profile.instanceId
$root=[IO.Path]::GetFullPath([string]$profile.installationDirectory)
$envFile=Join-Path $root '.env'
$compose=Join-Path $root 'docker-compose.stable.yml'
$override=Join-Path $root 'docker/stable/compose-host-executor.windows.override.yml'
$docker=[IO.Path]::GetFullPath([string]$profile.dockerExecutable)
if (-not (Test-Path -LiteralPath $root -PathType Container) -or
    -not (Test-Path -LiteralPath $compose -PathType Leaf) -or
    -not (Test-Path -LiteralPath $envFile -PathType Leaf) -or
    -not (Test-Path -LiteralPath $docker -PathType Leaf) -or
    -not ([IO.Path]::GetExtension($docker) -ieq '.exe')) { throw 'Registered Docker host files are unavailable.' }
if ($profile.composeSha256 -cnotmatch '^[0-9a-f]{64}$' -or
    (Get-FileHash -LiteralPath $compose -Algorithm SHA256).Hash.ToLowerInvariant() -cne $profile.composeSha256) { throw 'Registered Docker Compose hash changed.' }
$baseArgs=@('--context','desktop-linux','compose','--project-name',$id,'-f',$compose)
if ($names -contains 'composeOverrideSha256') {
  if ($profile.composeOverrideSha256 -cnotmatch '^[0-9a-f]{64}$' -or
      -not (Test-Path -LiteralPath $override -PathType Leaf) -or
      (Get-FileHash -LiteralPath $override -Algorithm SHA256).Hash.ToLowerInvariant() -cne $profile.composeOverrideSha256) { throw 'Registered Docker host IPC override hash changed.' }
  $baseArgs+=@('-f',$override)
}
$state=[IO.Path]::GetDirectoryName($profilePath)
$journal=Join-Path $state ('docker-network-'+$id+'.journal.json')
$lockFile=Join-Path $root '.scadivo-host-network.lock'
$utf8=[Text.UTF8Encoding]::new($false)
$lock=[IO.File]::Open($lockFile,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
function Invoke-Docker([string[]]$Arguments) {
  # Windows PowerShell 5.1 treats native stderr as an error record even when
  # Compose exits successfully. Compose writes normal progress there.
  $ErrorActionPreference='Continue'
  $output=@(& $docker @Arguments 2>$null)
  if ($LASTEXITCODE -ne 0) { throw 'Registered Docker Desktop command rejected; review protected host diagnostics.' }
  return $output
}
function Compose([string[]]$Arguments) { return @(Invoke-Docker ($baseArgs+$Arguments)) }
function Read-Env {
  $data=@{}
  foreach($line in [IO.File]::ReadAllLines($envFile)) {
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
      $name=$Matches[1];$value=$Matches[2].Trim().Trim('"',"'")
      if ($data.ContainsKey($name)) { throw 'Duplicate Docker host environment key.' }
      $data[$name]=$value
    }
  }
  if ($data.ContainsKey('SCADIVO_INSTALLATION_ID') -and $data['SCADIVO_INSTALLATION_ID'] -cne $id) { throw 'Docker installation identity changed.' }
  foreach($key in @('SCADIVO_WEB_BIND','SCADIVO_API_BIND')) { if ($data.ContainsKey($key) -and $data[$key] -notin @('127.0.0.1','0.0.0.0')) { throw 'Unsupported registered Docker bind.' } }
  return $data
}
function Get-Ports([hashtable]$data) {
  $out=@{}
  foreach($row in @(@('webPort','ELITESCADA_WEB_PORT','18080'),
                    @('apiPort','ELITESCADA_API_PORT','15120'),
                    @('databasePort','ELITESCADA_DB_PORT','15432'),
                    @('mediaControlPort','SCADIVO_MEDIA_CONTROL_PORT','9997'),
                    @('mediaPlaybackPort','SCADIVO_MEDIA_PLAYBACK_PORT','8888'))) {
    $val=$row[2]
    if ($data.ContainsKey($row[1])) { $val=$data[$row[1]] }
    if ($val -cnotmatch '^[0-9]{1,5}$') { throw 'Invalid registered Docker TCP port.' }
    $number=[int]$val
    if ($number -lt 1 -or $number -gt 65535) { throw 'Invalid registered Docker TCP port.' }
    $out[$row[0]]=$number
  }
  return $out
}
function Check-Owners {
  $ids=@(Compose -Arguments @('ps','--all','--quiet')|Where-Object {$_})
  foreach($container in $ids) {
    if ($container -cnotmatch '^[a-f0-9]{12,64}$') { throw 'Unexpected registered Docker container.' }
    $labels=((Invoke-Docker -Arguments @('--context','desktop-linux','inspect','--format','{{json .Config.Labels}}',$container))|Out-String|ConvertFrom-Json)
    if ($labels.'com.docker.compose.project' -cne $id -or
        -not [string]::Equals([IO.Path]::GetFullPath([string]$labels.'com.docker.compose.project.working_dir'),$root,[StringComparison]::OrdinalIgnoreCase)) { throw 'Docker project belongs to another installation.' }
  }
}
function Check-Available([hashtable]$before,[hashtable]$desired,[hashtable]$envData) {
  $published=@(Invoke-Docker -Arguments @('--context','desktop-linux','ps','--quiet')|Where-Object {$_})
  $containers=@()
  if($published.Count -gt 0) {
    foreach($container in $published){if ($container -cnotmatch '^[a-f0-9]{12,64}$'){throw 'Unexpected Docker process identifier.'}}
    $containers=@(((Invoke-Docker -Arguments (@('--context','desktop-linux','inspect')+$published))|Out-String|ConvertFrom-Json))
  }
  foreach($pair in @(@('webPort','SCADIVO_WEB_BIND'),@('apiPort','SCADIVO_API_BIND'),@('databasePort',''))) {
    $key=$pair[0];if($desired[$key] -eq $before[$key]){continue}
    $bind='127.0.0.1'
    if($pair[1] -and $envData.ContainsKey($pair[1])){$bind=[string]$envData[$pair[1]]}
    $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Parse($bind),[int]$desired[$key])
    try{$listener.Start()}catch{throw 'Requested host TCP port already occupied.'}finally{$listener.Stop()}
    foreach($container in $containers) {
      if($null -eq $container.NetworkSettings.Ports){continue}
      foreach($entry in $container.NetworkSettings.Ports.PSObject.Properties) {
        foreach($bound in @($entry.Value)) {
          if($null -eq $bound -or [int]$bound.HostPort -ne $desired[$key]){continue}
          if($bind -eq '0.0.0.0' -or [string]$bound.HostIp -in @('','::','0.0.0.0',$bind)) {
            throw 'Requested host TCP port already published by Docker.'
          }
        }
      }
    }
  }
}
function Compose-Check([string]$candidate) {
  [void](Compose -Arguments (@('--env-file',$candidate,'config','--quiet')))
  $services=@(Compose -Arguments (@('--env-file',$candidate,'config','--services') )|Where-Object {$_})
  if(@(Compare-Object -ReferenceObject @('web','api','database','media-gateway') -DifferenceObject $services).Count -ne 0){throw 'Compose service set differs from approved profile.'}
}
function Health([hashtable]$ports) {
  $deadline=(Get-Date).AddSeconds(120)
  while((Get-Date) -lt $deadline){
    try{
      $web=Invoke-WebRequest -Uri "http://127.0.0.1:$($ports.webPort)/healthz" -TimeoutSec 3 -UseBasicParsing
      $api=Invoke-WebRequest -Uri "http://127.0.0.1:$($ports.apiPort)/health" -TimeoutSec 3 -UseBasicParsing
      if($web.StatusCode -ne 200 -or $api.StatusCode -ne 200){throw 'Host HTTP health not ready.'}
      [void](Compose -Arguments @('exec','-T','database','pg_isready','-U','elitescada','-d','elitescada'))
      $probe="import socket,urllib.request,sys; urllib.request.urlopen('http://media-gateway:'+sys.argv[1]+'/v3/paths/list',timeout=3).read(); socket.create_connection(('media-gateway',int(sys.argv[2])),timeout=3).close()"
      [void](Compose -Arguments @('exec','-T','api','python3','-c',$probe,[string]$ports.mediaControlPort,[string]$ports.mediaPlaybackPort))
      return
    }catch{Start-Sleep -Seconds 3}
  }
  throw 'Registered Docker host health checks did not pass.'
}
function Write-Journal([object]$value) {
  $temp=Join-Path $state ('network-'+[Guid]::NewGuid().ToString('N')+'.tmp')
  try {
    [IO.File]::WriteAllText($temp,($value|ConvertTo-Json -Depth 8 -Compress),$utf8)
    Move-Item -LiteralPath $temp -Destination $journal -Force
  }finally{Remove-Item -LiteralPath $temp -ErrorAction SilentlyContinue}
}
function Get-Journal {
  if(-not (Test-Path -LiteralPath $journal -PathType Leaf)){return $null}
  $r=Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json
  if($r.schema -ne 'scadivo-docker-windows-recovery-v1' -or $r.instanceId -cne $id -or
     $r.phase -notin @('pending','applied','recovered')){throw 'Invalid registered Docker recovery record.'}
  $b=[IO.Path]::GetFullPath([string]$r.backup)
  if([IO.Path]::GetDirectoryName($b) -ne $state -or [IO.Path]::GetFileName($b) -cnotmatch '^docker-env-[0-9a-f]{32}\.backup$' -or
      -not (Test-Path -LiteralPath $b -PathType Leaf) -or
      (Get-FileHash -LiteralPath $b -Algorithm SHA256).Hash -ine $r.sha256){throw 'Invalid registered Docker recovery backup.'}
  return $r
}
function Restore([object]$record) {
  $bytes=[IO.File]::ReadAllBytes([string]$record.backup)
  [IO.File]::WriteAllBytes($envFile,$bytes)
  Check-Owners
  [void](Compose -Arguments @('up','-d'))
  $old=@{}
  foreach($k in @('webPort','apiPort','databasePort','mediaControlPort','mediaPlaybackPort')){$old[$k]=[int]$record.before.$k}
  Health $old
  $effective=Get-Ports (Read-Env)
  foreach($key in $old.Keys){if($old[$key] -ne $effective[$key]){throw 'Recovered Docker host ports differ from backup.'}}
  $record.phase='recovered';Write-Journal $record
}
try {
  $envData=Read-Env
  $old=Get-Ports $envData
  Check-Owners
  $existing=Get-Journal
  if($Recover){
    if($null -eq $existing){throw 'No registered Docker recovery is available.'}
    Restore $existing
    return
  }
  if($existing -and $existing.phase -eq 'pending'){throw 'Previous Docker operation requires confirmed recovery.'}
  if($Verify){
    Compose-Check $envFile
    Health $old
    return
  }
  $desired=@{webPort=$WebPort;apiPort=$ApiPort;databasePort=$DatabasePort;mediaControlPort=$MediaControlPort;mediaPlaybackPort=$MediaPlaybackPort}
  if(@(@($WebPort,$ApiPort,$DatabasePort)|Select-Object -Unique).Count -ne 3 -or $MediaControlPort -eq $MediaPlaybackPort){throw 'Registered Docker host ports overlap.'}
  Check-Available $old $desired $envData
  $lines=[Collections.Generic.List[string]]::new()
  $lines.AddRange([string[]][IO.File]::ReadAllLines($envFile))
  $values=@{ELITESCADA_WEB_PORT=$WebPort;ELITESCADA_API_PORT=$ApiPort;ELITESCADA_DB_PORT=$DatabasePort;SCADIVO_MEDIA_CONTROL_PORT=$MediaControlPort;SCADIVO_MEDIA_PLAYBACK_PORT=$MediaPlaybackPort}
  foreach($key in $values.Keys){
    $indexes=@(0..($lines.Count-1)|Where-Object {$lines[$_] -match ('^\s*'+[regex]::Escape($key)+'=')})
    if($indexes.Count -gt 1){throw 'Duplicate registered Docker port.'}
    if($indexes.Count -eq 1){$lines[$indexes[0]]=$key+'='+$values[$key]}
    else{$lines.Add($key+'='+$values[$key])}
  }
  $candidate=Join-Path $root ('.scadivo-network-preflight-'+[Guid]::NewGuid().ToString('N'))
  try {
    [IO.File]::WriteAllLines($candidate,$lines,$utf8)
    # Preserve the original non-inheriting DACL across the atomic .env swap.
    # The host daemon refuses an inherited/untrusted effective config after Apply.
    [IO.File]::SetAccessControl($candidate,[IO.File]::GetAccessControl($envFile))
    Compose-Check $candidate
    if(-not $Apply){return}
    $backup=Join-Path $state ('docker-env-'+[Guid]::NewGuid().ToString('N')+'.backup')
    [IO.File]::WriteAllBytes($backup,[IO.File]::ReadAllBytes($envFile))
    $record=@{schema='scadivo-docker-windows-recovery-v1';instanceId=$id;phase='pending';backup=$backup;sha256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash;before=$old;desired=$desired}
    Write-Journal $record
    try {
      # ReplaceFile preserves the destination's protected security descriptor.
      # Move-Item can re-enable ACL inheritance when replacing an existing file.
      # PowerShell converts a null string argument to an empty path. Keep the
      # replacement backup inside the protected state directory and remove it
      # after the existing recovery backup and journal have been committed.
      $swapBackup=Join-Path $state ('docker-env-swap-'+[Guid]::NewGuid().ToString('N')+'.backup')
      try { [IO.File]::Replace($candidate,$envFile,$swapBackup) }
      finally { [IO.File]::Delete($swapBackup) }
      [void](Compose -Arguments @('up','-d'))
      Health $desired
      $effective=Get-Ports (Read-Env)
      foreach($key in $desired.Keys){if($desired[$key] -ne $effective[$key]){throw 'Applied Docker host port diverged.'}}
      $record.phase='applied';Write-Journal $record
    }catch{
      try{Restore $record}catch{throw 'Docker apply and verified automatic recovery failed; protected journal retained.'}
      throw 'Docker application failed; the prior host configuration was restored and verified.'
    }
  }finally{Remove-Item -LiteralPath $candidate -ErrorAction SilentlyContinue}
}finally{$lock.Dispose()}
