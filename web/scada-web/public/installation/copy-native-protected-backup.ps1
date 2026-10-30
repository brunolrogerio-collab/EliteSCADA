[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SourcePath,
    [Parameter(Mandatory=$true)][string]$DestinationPath
)
$ErrorActionPreference='Stop'
$source=(Resolve-Path -LiteralPath $SourcePath).Path
$destination=[IO.Path]::GetFullPath($DestinationPath)
if ([IO.Path]::GetDirectoryName($source) -ne [IO.Path]::GetDirectoryName($destination)) { throw 'Backup must remain in the registered configuration directory.' }
$security=[Security.AccessControl.FileSecurity]::new()
$security.SetAccessRuleProtection($true,$false)
$current=[Security.Principal.WindowsIdentity]::GetCurrent().User
$security.SetOwner($current)
foreach ($sid in @($current.Value,'S-1-5-18','S-1-5-32-544') | Select-Object -Unique) {
    $identity=[Security.Principal.SecurityIdentifier]::new($sid)
    $rule=[Security.AccessControl.FileSystemAccessRule]::new($identity,[Security.AccessControl.FileSystemRights]::FullControl,[Security.AccessControl.AccessControlType]::Allow)
    $security.AddAccessRule($rule)
}
# CreateNew prevents replacing an existing file. The DACL is installed at
# creation, before any configuration bytes can reach the destination.
$output=[IO.FileStream]::new($destination,[IO.FileMode]::CreateNew,[Security.AccessControl.FileSystemRights]::FullControl,[IO.FileShare]::None,4096,[IO.FileOptions]::WriteThrough,$security)
try {
    $inputStream=[IO.File]::OpenRead($source)
    try { $inputStream.CopyTo($output); $output.Flush($true) } finally { $inputStream.Dispose() }
} finally { $output.Dispose() }
