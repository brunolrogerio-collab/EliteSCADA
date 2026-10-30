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
    'PASS: profile generation, protected Windows ACL, no secret copy, no overwrite, no application mutation; service lookup simulated.'
} finally {
    $expected=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetFileName($root))))
    if ([IO.Path]::GetFullPath($root) -ne $expected -or [IO.Path]::GetFileName($root) -notlike 'scadivo-profile-proof-*') { throw 'Unsafe cleanup.' }
    Remove-Item -LiteralPath $root -Recurse -Force
}
