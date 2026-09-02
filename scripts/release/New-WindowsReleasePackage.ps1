[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReleaseRoot,
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

# PowerShell verifier throws on every failure. Do not consult LASTEXITCODE here because
# it belongs to native commands and may contain unrelated stale state.
& (Join-Path $PSScriptRoot "Test-WindowsRelease.ps1") -ReleaseRoot $root

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path (Split-Path -Parent $root) "packages"
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$version = [string]$identity.version
$rid = [string]$identity.runtimeIdentifier
$packageName = "EliteSCADA-$version-$rid.zip"
$packagePath = Join-Path $OutputDirectory $packageName
if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$fixedTimestamp = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
$files = @(Get-ChildItem -LiteralPath $root -File -Recurse | Sort-Object {
    [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\\', '/')
})

$archiveStream = [IO.File]::Open($packagePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $archive = [IO.Compression.ZipArchive]::new($archiveStream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($file in $files) {
            $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\\', '/')
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

Write-Host "Verified deterministic package created: $packagePath"
Write-Host "SHA-256: $packageHash"
Write-Host "SHA-256 record: $hashPath"
Write-Host "Commercial distribution authorized: $($identity.commercialDistributionAuthorized)"
