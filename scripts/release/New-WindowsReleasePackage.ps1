[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReleaseRoot,
    [Parameter(Mandatory = $true)][string]$ExpectedSourceSha,
    [Parameter(Mandatory = $true)][string]$ExpectedPublisher,
    [Parameter(Mandatory = $true)][ValidateSet('product', 'authority')][string]$PackageRole,
    [string]$OutputDirectory,
    [switch]$SkipVerification
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = (Resolve-Path -LiteralPath $ReleaseRoot).Path
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$identity = Get-Content -LiteralPath (Join-Path $repositoryRoot "release/release-identity.json") -Raw | ConvertFrom-Json

if ($SkipVerification) {
    throw "SkipVerification is intentionally unsupported for Wave 13 release packaging. Release packaging is fail-closed."
}
if ($identity.dnp3CommercialGate -ne 'blocked' -or $identity.commercialDistributionAuthorized -ne $false) {
    throw "Current Wave 13 package contract requires the DNP3 commercial gate to remain explicitly blocked and commercial distribution unauthorized."
}

& (Join-Path $PSScriptRoot "Test-WindowsRelease.ps1") `
    -ReleaseRoot $root `
    -ExpectedSourceSha $ExpectedSourceSha `
    -ExpectedPublisher $ExpectedPublisher `
    -PackageRole all

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path (Split-Path -Parent $root) "packages"
}
$outputFullPath = [IO.Path]::GetFullPath($OutputDirectory)
if ($outputFullPath.Equals($root, [StringComparison]::OrdinalIgnoreCase) -or
    $outputFullPath.StartsWith($root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be outside ReleaseRoot so package outputs cannot contaminate the verified release set."
}
New-Item -ItemType Directory -Path $outputFullPath -Force | Out-Null

$manifestPath = Join-Path $root 'release-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$selectedPaths = @($manifest.artifacts | Where-Object {
    [string]$_.packageRole -eq $PackageRole -or [string]$_.packageRole -eq 'shared'
} | ForEach-Object { [string]$_.path })
$selectedSet = @{}
foreach ($path in $selectedPaths) { $selectedSet[$path] = $true }
$selectedSet['release-manifest.json'] = $true

$files = @(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {
    $relativePath = [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
    $selectedSet.ContainsKey($relativePath)
} | Sort-Object {
    [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
})
if ($files.Count -ne $selectedSet.Count) {
    throw "Package file selection differs from the verified manifest for role '$PackageRole'."
}

$version = [string]$identity.version
$rid = [string]$identity.runtimeIdentifier
$packageName = if ($PackageRole -eq 'product') {
    "EliteSCADA-$version-$rid.zip"
}
else {
    "EliteSCADA-LicenseGenerator-$version-$rid.zip"
}
$packagePath = Join-Path $outputFullPath $packageName
if (Test-Path -LiteralPath $packagePath) {
    throw "Package output already exists; deterministic packaging will not overwrite it: $packagePath"
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$fixedTimestamp = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
$archiveStream = [IO.File]::Open($packagePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $archive = [IO.Compression.ZipArchive]::new($archiveStream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($file in $files) {
            $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
            $entry = $archive.CreateEntry($relativePath, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $fixedTimestamp

            $entryStream = $entry.Open()
            try {
                $sourceStream = [IO.File]::OpenRead($file.FullName)
                try {
                    $sourceStream.CopyTo($entryStream)
                }
                finally {
                    $sourceStream.Dispose()
                }
            }
            finally {
                $entryStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $archiveStream.Dispose()
}

$packageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$hashPath = "$packagePath.sha256"
"$packageHash  $packageName" | Set-Content -LiteralPath $hashPath -Encoding ascii -NoNewline

& (Join-Path $PSScriptRoot "Test-WindowsReleasePackage.ps1") `
    -PackagePath $packagePath `
    -ExpectedPackageSha256 $packageHash `
    -ExpectedSourceSha $ExpectedSourceSha `
    -ExpectedPublisher $ExpectedPublisher `
    -PackageRole $PackageRole

Write-Host "Verified deterministic $PackageRole package created: $packagePath"
Write-Host "SHA-256: $packageHash"
Write-Host "SHA-256 record: $hashPath"
Write-Host "Commercial distribution authorized: $($identity.commercialDistributionAuthorized)"
