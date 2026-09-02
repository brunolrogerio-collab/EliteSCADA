[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UnsignedCandidateRoot,
    [Parameter(Mandatory = $true)][string]$SignedReturnRoot,
    [Parameter(Mandatory = $true)][string]$OutputRoot,
    [Parameter(Mandatory = $true)][string]$ExpectedSourceSha,
    [Parameter(Mandatory = $true)][string]$ExpectedPublisher
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "WindowsReleaseVerification.ps1")

function Get-ReleaseFileMap {
    param([Parameter(Mandatory = $true)][string]$Root)

    $map = @{}
    foreach ($file in Get-ChildItem -LiteralPath $Root -File -Recurse) {
        $relativePath = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        if ($map.ContainsKey($relativePath)) {
            throw "Duplicate candidate path encountered: $relativePath"
        }
        $map[$relativePath] = $file
    }
    return $map
}

function Test-PathWithinRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Root
    )

    if ($Path.Equals($Root, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    return $Path.StartsWith($Root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)
}

if ($ExpectedSourceSha -notmatch '^[0-9a-fA-F]{40}$') {
    throw "ExpectedSourceSha must be a full 40-character Git commit SHA."
}
if ([string]::IsNullOrWhiteSpace($ExpectedPublisher)) {
    throw "ExpectedPublisher is required."
}

$expectedSha = $ExpectedSourceSha.ToLowerInvariant()
$unsignedRoot = (Resolve-Path -LiteralPath $UnsignedCandidateRoot).Path
$signedRoot = (Resolve-Path -LiteralPath $SignedReturnRoot).Path
$outputFullPath = [IO.Path]::GetFullPath($OutputRoot)

if ($unsignedRoot.Equals($signedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "UnsignedCandidateRoot and SignedReturnRoot must be separate directories."
}
if ((Test-PathWithinRoot -Path $outputFullPath -Root $unsignedRoot) -or
    (Test-PathWithinRoot -Path $outputFullPath -Root $signedRoot)) {
    throw "OutputRoot must not be inside either signing input directory."
}
if (Test-Path -LiteralPath $outputFullPath) {
    throw "OutputRoot already exists; signed-release completion will not overwrite it: $outputFullPath"
}

& (Join-Path $PSScriptRoot "Test-WindowsReleaseCandidate.ps1") -CandidateRoot $unsignedRoot
& (Join-Path $PSScriptRoot "Test-WindowsReleaseCandidate.ps1") -CandidateRoot $signedRoot

$metadataPath = Join-Path $unsignedRoot "candidate-metadata.json"
$signedMetadataPath = Join-Path $signedRoot "candidate-metadata.json"
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$signedMetadataHash = (Get-FileHash -LiteralPath $signedMetadataPath -Algorithm SHA256).Hash
$unsignedMetadataHash = (Get-FileHash -LiteralPath $metadataPath -Algorithm SHA256).Hash
if ($signedMetadataHash -ne $unsignedMetadataHash) {
    throw "Signed return changed candidate-metadata.json."
}
if ([string]$metadata.sourceSha -ne $expectedSha) {
    throw "Unsigned candidate source SHA '$($metadata.sourceSha)' does not match expected '$expectedSha'."
}

$unsignedFiles = Get-ReleaseFileMap -Root $unsignedRoot
$signedFiles = Get-ReleaseFileMap -Root $signedRoot
if ($unsignedFiles.Count -ne $signedFiles.Count) {
    throw "Signed return file count differs from the unsigned candidate."
}

foreach ($relativePath in ($unsignedFiles.Keys | Sort-Object)) {
    if (-not $signedFiles.ContainsKey($relativePath)) {
        throw "Signed return is missing candidate file: $relativePath"
    }

    $unsignedFile = $unsignedFiles[$relativePath]
    $signedFile = $signedFiles[$relativePath]
    $unsignedIsPe = Test-WindowsPortableExecutable -Path $unsignedFile.FullName
    $signedIsPe = Test-WindowsPortableExecutable -Path $signedFile.FullName
    if ($unsignedIsPe -ne $signedIsPe) {
        throw "Signed return changed PE classification for: $relativePath"
    }

    if ($unsignedIsPe) {
        Assert-WindowsAuthenticodeSigningDelta `
            -UnsignedPath $unsignedFile.FullName `
            -SignedPath $signedFile.FullName `
            -RelativePath $relativePath
    }
    else {
        $unsignedHash = (Get-FileHash -LiteralPath $unsignedFile.FullName -Algorithm SHA256).Hash
        $signedHash = (Get-FileHash -LiteralPath $signedFile.FullName -Algorithm SHA256).Hash
        if ($unsignedHash -ne $signedHash) {
            throw "Signed return changed non-PE candidate content: $relativePath"
        }
    }
}

foreach ($relativePath in $signedFiles.Keys) {
    if (-not $unsignedFiles.ContainsKey($relativePath)) {
        throw "Signed return added unexpected file: $relativePath"
    }
}

$outputParent = Split-Path -Parent $outputFullPath
New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
$stagingRoot = "$outputFullPath.staging-$PID-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $stagingRoot | Out-Null

try {
    Get-ChildItem -LiteralPath $signedRoot -Force | Copy-Item -Destination $stagingRoot -Recurse -Force

    $releaseMetadata = [ordered]@{
        schemaVersion = 1
        product = [string]$metadata.product
        version = [string]$metadata.version
        sourceSha = $expectedSha
        runtimeIdentifier = [string]$metadata.runtimeIdentifier
        packageFormat = [string]$metadata.packageFormat
        signingState = "signed-return"
        expectedPublisher = $ExpectedPublisher
        dnp3IncludedInProductGraph = [bool]$metadata.dnp3IncludedInProductGraph
        dnp3CommercialGate = [string]$metadata.dnp3CommercialGate
        commercialDistributionAuthorized = [bool]$metadata.commercialDistributionAuthorized
        productDirectory = [string]$metadata.productDirectory
        authorityDirectory = [string]$metadata.authorityDirectory
    }
    $releaseMetadata | ConvertTo-Json -Depth 5 | Set-Content `
        -LiteralPath (Join-Path $stagingRoot "release-metadata.json") `
        -Encoding utf8NoBOM
    Remove-Item -LiteralPath (Join-Path $stagingRoot "candidate-metadata.json") -Force

    & (Join-Path $PSScriptRoot "New-WindowsReleaseManifest.ps1") `
        -SignedRoot $stagingRoot `
        -SourceSha $expectedSha `
        -ExpectedPublisher $ExpectedPublisher
    & (Join-Path $PSScriptRoot "Test-WindowsRelease.ps1") `
        -ReleaseRoot $stagingRoot `
        -ExpectedSourceSha $expectedSha `
        -ExpectedPublisher $ExpectedPublisher

    Move-Item -LiteralPath $stagingRoot -Destination $outputFullPath
}
catch {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
    throw
}

$manifestPath = Join-Path $outputFullPath "release-manifest.json"
$manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Wave 13 signed return completed at $outputFullPath"
Write-Host "Source SHA: $expectedSha"
Write-Host "Publisher: $ExpectedPublisher"
Write-Host "Release manifest SHA-256: $manifestHash"
