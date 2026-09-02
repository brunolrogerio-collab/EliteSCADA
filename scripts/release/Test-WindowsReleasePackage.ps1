[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$ExpectedPackageSha256,
    [Parameter(Mandatory = $true)][string]$ExpectedSourceSha,
    [Parameter(Mandatory = $true)][string]$ExpectedPublisher,
    [Parameter(Mandatory = $true)][ValidateSet('product', 'authority')][string]$PackageRole
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($ExpectedPackageSha256 -notmatch '^[0-9a-fA-F]{64}$') {
    throw "ExpectedPackageSha256 must be a 64-character SHA-256 value from a trusted release record."
}

$package = (Resolve-Path -LiteralPath $PackagePath).Path
$expectedHash = $ExpectedPackageSha256.ToLowerInvariant()
$actualHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $expectedHash) {
    throw "Release package SHA-256 mismatch. Expected $expectedHash, actual $actualHash."
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$maximumEntries = 10000
$maximumUncompressedBytes = 4L * 1024 * 1024 * 1024
$extractionRoot = Join-Path ([IO.Path]::GetTempPath()) "elitescada-wave13-package-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $extractionRoot | Out-Null

try {
    $archiveStream = [IO.File]::OpenRead($package)
    try {
        $archive = [IO.Compression.ZipArchive]::new($archiveStream, [IO.Compression.ZipArchiveMode]::Read, $false)
        try {
            if ($archive.Entries.Count -lt 1 -or $archive.Entries.Count -gt $maximumEntries) {
                throw "Release package entry count is outside the verification limit."
            }

            $seen = @{}
            [long]$totalLength = 0
            foreach ($entry in $archive.Entries) {
                $entryPath = $entry.FullName.Replace('\', '/')
                if ([string]::IsNullOrWhiteSpace($entryPath) -or
                    $entryPath.Length -gt 1024 -or
                    $entryPath.EndsWith('/')) {
                    throw "Release package contains an empty or explicit directory entry."
                }
                if ([IO.Path]::IsPathRooted($entryPath) -or $entryPath.Contains('\')) {
                    throw "Release package contains a non-canonical path: $entryPath"
                }
                foreach ($segment in $entryPath.Split('/')) {
                    if ([string]::IsNullOrEmpty($segment) -or $segment -eq '.' -or $segment -eq '..') {
                        throw "Release package contains an unsafe path: $entryPath"
                    }
                    if ($segment.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or
                        $segment.EndsWith('.') -or
                        $segment.EndsWith(' ')) {
                        throw "Release package contains a Windows-unsafe path: $entryPath"
                    }
                    $deviceStem = $segment.Split('.')[0]
                    if ($deviceStem -match '^(?i:con|prn|aux|nul|com[1-9]|lpt[1-9])$') {
                        throw "Release package contains a reserved Windows device path: $entryPath"
                    }
                }
                if ($seen.ContainsKey($entryPath)) { throw "Release package contains duplicate path: $entryPath" }
                $seen[$entryPath] = $true

                if ([long]$entry.Length -gt $maximumUncompressedBytes - $totalLength) {
                    throw "Release package exceeds the uncompressed verification limit."
                }
                $totalLength += [long]$entry.Length

                $destination = [IO.Path]::GetFullPath((Join-Path $extractionRoot $entryPath))
                if (-not $destination.StartsWith(
                    $extractionRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar,
                    [StringComparison]::OrdinalIgnoreCase)) {
                    throw "Release package entry escapes the extraction root: $entryPath"
                }

                $destinationDirectory = Split-Path -Parent $destination
                New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
                $entryStream = $entry.Open()
                try {
                    $destinationStream = [IO.File]::Open(
                        $destination,
                        [IO.FileMode]::CreateNew,
                        [IO.FileAccess]::Write,
                        [IO.FileShare]::None)
                    try {
                        $entryStream.CopyTo($destinationStream)
                    }
                    finally {
                        $destinationStream.Dispose()
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

    & (Join-Path $PSScriptRoot "Test-WindowsRelease.ps1") `
        -ReleaseRoot $extractionRoot `
        -ExpectedSourceSha $ExpectedSourceSha `
        -ExpectedPublisher $ExpectedPublisher `
        -PackageRole $PackageRole
}
finally {
    if (Test-Path -LiteralPath $extractionRoot) {
        Remove-Item -LiteralPath $extractionRoot -Recurse -Force
    }
}

Write-Host "Wave 13 $PackageRole ZIP package verification passed."
Write-Host "Package SHA-256: $actualHash"
