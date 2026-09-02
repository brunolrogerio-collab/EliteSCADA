[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CandidateRoot,
    [Parameter(Mandatory = $true)][string]$ExpectedSourceSha,
    [string]$ExpectedPublisher = 'CN=EliteSCADA Wave 13 Negative Test Publisher'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'WindowsReleaseVerification.ps1')

function Assert-ExpectedFailure {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Action,
        [Parameter(Mandatory = $true)][string]$ExpectedMessage,
        [Parameter(Mandatory = $true)][string]$CaseName
    )

    $failureMessage = $null
    try {
        & $Action
    }
    catch {
        $failureMessage = $_.Exception.Message
    }

    if ([string]::IsNullOrWhiteSpace($failureMessage)) {
        throw "Negative case '$CaseName' unexpectedly passed."
    }
    if ($failureMessage -notlike "*$ExpectedMessage*") {
        throw "Negative case '$CaseName' failed for the wrong reason. Expected '*$ExpectedMessage*', actual '$failureMessage'."
    }

    Write-Host "Negative case passed: $CaseName"
}

function Copy-DirectoryContent {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    New-Item -ItemType Directory -Path $Destination | Out-Null
    Get-ChildItem -LiteralPath $Source -Force | Copy-Item -Destination $Destination -Recurse -Force
}

function New-TestZip {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string[]]$EntryPaths
    )

    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($entryPath in $EntryPaths) {
                $entry = $archive.CreateEntry($entryPath)
                $writer = [IO.StreamWriter]::new($entry.Open())
                try {
                    $writer.Write('wave13-negative-fixture')
                }
                finally {
                    $writer.Dispose()
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

if ($ExpectedSourceSha -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'ExpectedSourceSha must be a full 40-character Git commit SHA.'
}
$expectedSha = $ExpectedSourceSha.ToLowerInvariant()
$candidate = (Resolve-Path -LiteralPath $CandidateRoot).Path

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$testRoot = Join-Path ([IO.Path]::GetTempPath()) "elitescada-wave13-negative-$([Guid]::NewGuid().ToString('N'))"
$releaseRoot = Join-Path $testRoot 'release'
$signedReturnRoot = Join-Path $testRoot 'unsigned-return'
$completionOutput = Join-Path $testRoot 'completion-output'
New-Item -ItemType Directory -Path $testRoot | Out-Null

try {
    Copy-DirectoryContent -Source $candidate -Destination $releaseRoot

    $candidateMetadataPath = Join-Path $releaseRoot 'candidate-metadata.json'
    $candidateMetadata = Get-Content -LiteralPath $candidateMetadataPath -Raw | ConvertFrom-Json
    if ([string]$candidateMetadata.sourceSha -ne $expectedSha) {
        throw "Negative fixture candidate source SHA differs from expected '$expectedSha'."
    }

    $releaseMetadata = [ordered]@{
        schemaVersion = 1
        product = [string]$candidateMetadata.product
        version = [string]$candidateMetadata.version
        sourceSha = $expectedSha
        runtimeIdentifier = [string]$candidateMetadata.runtimeIdentifier
        packageFormat = [string]$candidateMetadata.packageFormat
        signingState = 'signed-return'
        expectedPublisher = $ExpectedPublisher
        dnp3IncludedInProductGraph = [bool]$candidateMetadata.dnp3IncludedInProductGraph
        dnp3CommercialGate = [string]$candidateMetadata.dnp3CommercialGate
        commercialDistributionAuthorized = [bool]$candidateMetadata.commercialDistributionAuthorized
        productDirectory = [string]$candidateMetadata.productDirectory
        authorityDirectory = [string]$candidateMetadata.authorityDirectory
    }
    $releaseMetadata | ConvertTo-Json -Depth 5 | Set-Content `
        -LiteralPath (Join-Path $releaseRoot 'release-metadata.json') `
        -Encoding utf8NoBOM
    Remove-Item -LiteralPath $candidateMetadataPath -Force

    & (Join-Path $PSScriptRoot 'New-WindowsReleaseManifest.ps1') `
        -SignedRoot $releaseRoot `
        -SourceSha $expectedSha `
        -ExpectedPublisher $ExpectedPublisher

    $verificationArguments = @{
        ReleaseRoot = $releaseRoot
        ExpectedSourceSha = $expectedSha
        ExpectedPublisher = $ExpectedPublisher
    }

    Assert-ExpectedFailure `
        -CaseName 'unsigned mandatory PE' `
        -ExpectedMessage 'Authenticode signature is not valid' `
        -Action { & (Join-Path $PSScriptRoot 'Test-WindowsRelease.ps1') @verificationArguments }

    $indexPath = Join-Path $releaseRoot 'product/wwwroot/index.html'
    $indexBytes = [IO.File]::ReadAllBytes($indexPath)
    try {
        [IO.File]::AppendAllText($indexPath, 'wave13-tamper')
        Assert-ExpectedFailure `
            -CaseName 'tampered required content' `
            -ExpectedMessage 'SHA-256 mismatch' `
            -Action { & (Join-Path $PSScriptRoot 'Test-WindowsRelease.ps1') @verificationArguments }
    }
    finally {
        [IO.File]::WriteAllBytes($indexPath, $indexBytes)
    }

    $missingPath = "$indexPath.missing-fixture"
    Move-Item -LiteralPath $indexPath -Destination $missingPath
    try {
        Assert-ExpectedFailure `
            -CaseName 'missing required content' `
            -ExpectedMessage 'Required manifest artifact is missing' `
            -Action { & (Join-Path $PSScriptRoot 'Test-WindowsRelease.ps1') @verificationArguments }
    }
    finally {
        Move-Item -LiteralPath $missingPath -Destination $indexPath
    }

    $unexpectedPe = Join-Path $releaseRoot 'product/unexpected.exe'
    Copy-Item -LiteralPath (Join-Path $releaseRoot 'authority/EliteSCADA.LicenseGenerator.exe') -Destination $unexpectedPe
    try {
        Assert-ExpectedFailure `
            -CaseName 'unexpected PE content' `
            -ExpectedMessage 'Unexpected executable/PE file' `
            -Action { & (Join-Path $PSScriptRoot 'Test-WindowsRelease.ps1') @verificationArguments }
    }
    finally {
        Remove-Item -LiteralPath $unexpectedPe -Force
    }

    $unexpectedContent = Join-Path $releaseRoot 'product/unexpected.txt'
    Set-Content -LiteralPath $unexpectedContent -Value 'undeclared' -Encoding ascii
    try {
        Assert-ExpectedFailure `
            -CaseName 'unexpected non-PE content' `
            -ExpectedMessage 'Unexpected undeclared file' `
            -Action { & (Join-Path $PSScriptRoot 'Test-WindowsRelease.ps1') @verificationArguments }
    }
    finally {
        Remove-Item -LiteralPath $unexpectedContent -Force
    }

    Copy-DirectoryContent -Source $candidate -Destination $signedReturnRoot
    Assert-ExpectedFailure `
        -CaseName 'unsigned signer return' `
        -ExpectedMessage 'does not contain an Authenticode certificate table' `
        -Action {
            & (Join-Path $PSScriptRoot 'Complete-WindowsSignedRelease.ps1') `
                -UnsignedCandidateRoot $candidate `
                -SignedReturnRoot $signedReturnRoot `
                -OutputRoot $completionOutput `
                -ExpectedSourceSha $expectedSha `
                -ExpectedPublisher $ExpectedPublisher
        }

    $unsignedPePath = Join-Path $candidate 'product/Scada.Api.exe'
    $unsignedLayout = Get-WindowsPeSigningLayout -Path $unsignedPePath
    $certificateOffset = [long]([Math]::Ceiling($unsignedLayout.Bytes.LongLength / 8.0) * 8.0)
    $syntheticSignedPath = Join-Path $testRoot 'synthetic-signing-delta.exe'
    $syntheticBytes = [byte[]]::new([int]($certificateOffset + 8))
    [Buffer]::BlockCopy($unsignedLayout.Bytes, 0, $syntheticBytes, 0, $unsignedLayout.Bytes.Length)
    $syntheticBytes[[int]$unsignedLayout.ChecksumOffset] = `
        $syntheticBytes[[int]$unsignedLayout.ChecksumOffset] -bxor 1
    [BitConverter]::GetBytes([uint32]$certificateOffset).CopyTo(
        $syntheticBytes,
        [int]$unsignedLayout.SecurityDirectoryOffset)
    [BitConverter]::GetBytes([uint32]8).CopyTo(
        $syntheticBytes,
        [int]($unsignedLayout.SecurityDirectoryOffset + 4))
    [BitConverter]::GetBytes([uint32]8).CopyTo($syntheticBytes, [int]$certificateOffset)
    [BitConverter]::GetBytes([uint16]0x0200).CopyTo($syntheticBytes, [int]($certificateOffset + 4))
    [BitConverter]::GetBytes([uint16]0x0002).CopyTo($syntheticBytes, [int]($certificateOffset + 6))
    [IO.File]::WriteAllBytes($syntheticSignedPath, $syntheticBytes)
    $unsignedLayout = $null
    $syntheticBytes = $null

    Assert-WindowsAuthenticodeSigningDelta `
        -UnsignedPath $unsignedPePath `
        -SignedPath $syntheticSignedPath `
        -RelativePath 'synthetic-signing-delta.exe'
    Write-Host 'Positive structural case passed: Authenticode-only PE delta.'

    $mutatedSyntheticBytes = [IO.File]::ReadAllBytes($syntheticSignedPath)
    $mutatedSyntheticBytes[2] = $mutatedSyntheticBytes[2] -bxor 1
    [IO.File]::WriteAllBytes($syntheticSignedPath, $mutatedSyntheticBytes)
    $mutatedSyntheticBytes = $null
    Assert-ExpectedFailure `
        -CaseName 'signed-return PE payload mutation' `
        -ExpectedMessage 'differs from the candidate outside Authenticode signing fields' `
        -Action {
            Assert-WindowsAuthenticodeSigningDelta `
                -UnsignedPath $unsignedPePath `
                -SignedPath $syntheticSignedPath `
                -RelativePath 'synthetic-signing-delta.exe'
        }

    $traversalZip = Join-Path $testRoot 'traversal.zip'
    New-TestZip -Path $traversalZip -EntryPaths @('../escape.txt')
    $traversalHash = (Get-FileHash -LiteralPath $traversalZip -Algorithm SHA256).Hash
    Assert-ExpectedFailure `
        -CaseName 'ZIP traversal content' `
        -ExpectedMessage 'unsafe path' `
        -Action {
            & (Join-Path $PSScriptRoot 'Test-WindowsReleasePackage.ps1') `
                -PackagePath $traversalZip `
                -ExpectedPackageSha256 $traversalHash `
                -ExpectedSourceSha $expectedSha `
                -ExpectedPublisher $ExpectedPublisher `
                -PackageRole product
        }
    Assert-ExpectedFailure `
        -CaseName 'ZIP trusted-hash mismatch' `
        -ExpectedMessage 'SHA-256 mismatch' `
        -Action {
            & (Join-Path $PSScriptRoot 'Test-WindowsReleasePackage.ps1') `
                -PackagePath $traversalZip `
                -ExpectedPackageSha256 (('0' * 64) -join '') `
                -ExpectedSourceSha $expectedSha `
                -ExpectedPublisher $ExpectedPublisher `
                -PackageRole product
        }

    $duplicateZip = Join-Path $testRoot 'duplicate.zip'
    New-TestZip -Path $duplicateZip -EntryPaths @('release-metadata.json', 'release-metadata.json')
    $duplicateHash = (Get-FileHash -LiteralPath $duplicateZip -Algorithm SHA256).Hash
    Assert-ExpectedFailure `
        -CaseName 'ZIP duplicate path' `
        -ExpectedMessage 'duplicate path' `
        -Action {
            & (Join-Path $PSScriptRoot 'Test-WindowsReleasePackage.ps1') `
                -PackagePath $duplicateZip `
                -ExpectedPackageSha256 $duplicateHash `
                -ExpectedSourceSha $expectedSha `
                -ExpectedPublisher $ExpectedPublisher `
                -PackageRole product
        }

    $unsafeWindowsZip = Join-Path $testRoot 'windows-unsafe.zip'
    New-TestZip -Path $unsafeWindowsZip -EntryPaths @('product/payload:stream')
    $unsafeWindowsHash = (Get-FileHash -LiteralPath $unsafeWindowsZip -Algorithm SHA256).Hash
    Assert-ExpectedFailure `
        -CaseName 'ZIP Windows-unsafe path' `
        -ExpectedMessage 'Windows-unsafe path' `
        -Action {
            & (Join-Path $PSScriptRoot 'Test-WindowsReleasePackage.ps1') `
                -PackagePath $unsafeWindowsZip `
                -ExpectedPackageSha256 $unsafeWindowsHash `
                -ExpectedSourceSha $expectedSha `
                -ExpectedPublisher $ExpectedPublisher `
                -PackageRole product
        }

    Write-Host 'Wave 13 fail-closed negative verification passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
