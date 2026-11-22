$ErrorActionPreference='Stop'
$root=Join-Path ([IO.Path]::GetTempPath()) ('scadivo-profile-proof-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
function Get-Service { param($Name,$ErrorAction) if ($Name -eq 'missing') { throw 'Service absent' }; [pscustomobject]@{Name=$Name;Status='Running'} }
try {
    $config=Join-Path $root 'app.json'; $profile=Join-Path $root 'native-network-profile.json'
    $original='{"Kestrel":{"Endpoints":{"Web":{"Url":"http://127.0.0.1:18080"},"Api":{"Url":"http://127.0.0.1:15120"}}},"secret":"must-not-copy"}'
    [IO.File]::WriteAllText($config,$original)
    $script=Join-Path (Get-Location) 'web/scada-web/public/installation/provision-native-network-profile.ps1'
    $failed=$false
    try { & $script -ConfigurationPath $config -ServiceName missing -DestinationPath $profile } catch { $failed=$true }
    if (-not $failed -or (Test-Path -LiteralPath $profile)) { throw 'Missing service accepted.' }
    $pending="$config.media-port-pending.json"
    [IO.File]::WriteAllText($pending,'pending')
    $failed=$false
    try { & $script -ConfigurationPath $config -ServiceName ScadivoTest -DestinationPath $profile } catch { $failed=$true }
    if (-not $failed -or (Test-Path -LiteralPath $profile)) { throw 'Pending recovery ignored.' }
    Remove-Item -LiteralPath $pending
    [IO.File]::WriteAllText($config,$original.Replace(':15120',':18080'))
    $failed=$false
    try { & $script -ConfigurationPath $config -ServiceName ScadivoTest -DestinationPath $profile } catch { $failed=$true }
    if (-not $failed -or (Test-Path -LiteralPath $profile)) { throw 'Colliding listeners accepted.' }
    [IO.File]::WriteAllText($config,$original)
    & $script -ConfigurationPath $config -ServiceName ScadivoTest -DestinationPath $profile
    $content=[IO.File]::ReadAllText($profile); $result=$content | ConvertFrom-Json
    if ($result.configurationFile -ne $config -or $result.serviceName -ne 'ScadivoTest' -or $content.Contains('must-not-copy')) { throw 'Invalid profile or secret copied.' }
    $acl=[IO.File]::GetAccessControl($profile)
    if (-not $acl.AreAccessRulesProtected) { throw 'Profile ACL inherited.' }
    $allowed=@([Security.Principal.WindowsIdentity]::GetCurrent().User.Value,'S-1-5-18','S-1-5-32-544')
    foreach ($rule in $acl.Access) { if ($rule.IsInherited -or $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value -notin $allowed) { throw 'Unexpected ACL principal.' } }
    $failed=$false
    try { & $script -ConfigurationPath $config -ServiceName ScadivoTest -DestinationPath $profile } catch { $failed=$true }
    if (-not $failed -or [IO.File]::ReadAllText($profile) -ne $content) { throw 'Existing profile overwritten.' }
    if ([IO.File]::ReadAllText($config) -ne $original) { throw 'Provisioning changed application configuration.' }
    $registered=Join-Path $root 'registered-profile.json'
    & $script -ConfigurationPath $config -ServiceName ScadivoTest -DestinationPath $registered -Register
    $updated=Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
    if ($updated.InstallationNetwork.Profile -ne 'native-windows' -or $updated.InstallationNetwork.AdministrativeProfilePath -ne $registered -or $updated.secret -ne 'must-not-copy') { throw 'Profile registration failed.' }
    if (([Uri]$updated.Kestrel.Endpoints.Web.Url).Port -ne 18080 -or ([Uri]$updated.Kestrel.Endpoints.Api.Url).Port -ne 15120) { throw 'Registration changed ports.' }
    $backup=@(Get-ChildItem -LiteralPath $root -Filter 'app.json.network-backup-*')
    if ($backup.Count -ne 1 -or [IO.File]::ReadAllText($backup[0].FullName) -ne $original) { throw 'Registration backup missing or invalid.' }
    $media=Join-Path $root 'media.yml'; $mediaProfile=Join-Path $root 'media-profile.json'
    $updated | Add-Member -NotePropertyName MediaSources -NotePropertyValue ([pscustomobject]@{GatewayControlUrl='http://127.0.0.1:9997';GatewayPlaybackUrl='http://127.0.0.1:8888'})
    $updated | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $config
    [IO.File]::WriteAllText($media,"apiAddress: :9998`nhlsAddress: :8888`n")
    $failed=$false
    try { & $script -ConfigurationPath $config -ServiceName ScadivoTest -DestinationPath $mediaProfile -MediaConfigurationPath $media -MediaServiceName MediaTest } catch { $failed=$true }
    if (-not $failed -or (Test-Path -LiteralPath $mediaProfile)) { throw 'Media port mismatch accepted.' }
    [IO.File]::WriteAllText($media,"apiAddress: :9997`nhlsAddress: :8888`n")
    & $script -ConfigurationPath $config -ServiceName ScadivoTest -DestinationPath $mediaProfile -MediaConfigurationPath $media -MediaServiceName MediaTest
    $result=Get-Content -LiteralPath $mediaProfile -Raw | ConvertFrom-Json
    if ($result.media.serviceName -ne 'MediaTest' -or $result.media.configurationFile -ne $media) { throw 'Media profile not registered.' }
    $topology=Join-Path $root 'topology.json'; $pg=Join-Path $root 'pg.conf'; $psql=Join-Path $root 'psql.exe'; $declaration=Join-Path $root 'db.json'; $dbProfile=Join-Path $root 'db-profile.json'
    @{schemaVersion=1;active=@{mode='LocalManaged'};pending=$null} | ConvertTo-Json | Set-Content -LiteralPath $topology
    [IO.File]::WriteAllText($pg,'port=15432'); [IO.File]::WriteAllText($psql,'fixture-only-not-executed')
    $db=@{topologyFile=$topology;configurationFile=$pg;psqlExecutable=$psql;serviceName='DatabaseTest';currentPort=15432;username='test-account';databaseName='plant';password='must-reject'}
    $db | ConvertTo-Json | Set-Content -LiteralPath $declaration
    $failed=$false
    try { & $script -ConfigurationPath $config -ServiceName ScadivoTest -DestinationPath $dbProfile -DatabaseRegistrationPath $declaration } catch { $failed=$true }
    if (-not $failed -or (Test-Path -LiteralPath $dbProfile)) { throw 'Database credentials accepted.' }
    $db.Remove('password'); $db | ConvertTo-Json | Set-Content -LiteralPath $declaration
    & $script -ConfigurationPath $config -ServiceName ScadivoTest -DestinationPath $dbProfile -DatabaseRegistrationPath $declaration
    $result=Get-Content -LiteralPath $dbProfile -Raw | ConvertFrom-Json
    if ($result.database.serviceName -ne 'DatabaseTest' -or $result.topologyFile -ne $topology) { throw 'Database profile not registered.' }
    $combined=Join-Path $root 'combined.json'
    $db.configurationFile=$media; $db | ConvertTo-Json | Set-Content -LiteralPath $declaration
    $failed=$false
    try { & $script -ConfigurationPath $config -ServiceName ScadivoTest -DestinationPath $combined -MediaConfigurationPath $media -MediaServiceName MediaTest -DatabaseRegistrationPath $declaration } catch { $failed=$true }
    if (-not $failed -or (Test-Path -LiteralPath $combined)) { throw 'Shared database/media configuration accepted.' }
    $db.configurationFile=$pg; $db | ConvertTo-Json | Set-Content -LiteralPath $declaration
    & $script -ConfigurationPath $config -ServiceName ScadivoTest -DestinationPath $combined -MediaConfigurationPath $media -MediaServiceName MediaTest -DatabaseRegistrationPath $declaration
    $result=Get-Content -LiteralPath $combined -Raw | ConvertFrom-Json
    if ($result.database.configurationFile -ne $pg -or $result.media.configurationFile -ne $media) { throw 'Combined profile invalid.' }
    'PASS: profile generation, protected Windows ACL, no secret copy, no overwrite, no application mutation; service lookup simulated.'
} finally {
    $expected=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetFileName($root))))
    if ([IO.Path]::GetFullPath($root) -ne $expected -or [IO.Path]::GetFileName($root) -notlike 'scadivo-profile-proof-*') { throw 'Unsafe cleanup.' }
    Remove-Item -LiteralPath $root -Recurse -Force
}
