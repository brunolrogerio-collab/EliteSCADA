[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SignedRoot,
    [Parameter(Mandatory = $true)][string]$SourceSha,
    [Parameter(Mandatory = $true)][string]$ExpectedPublisher,
    [string]$OutputPath = (Join-Path $SignedRoot "release-manifest.json")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "WindowsReleaseVerification.ps1")

$root = (Resolve-Path -LiteralPath $SignedRoot).Path
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$identityPath = Join-Path $repositoryRoot "release/release-identity.json"
$identity = Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json

if ($identity.schemaVersion -ne 1) { throw "Unsupported release identity schemaVersion '$($identity.schemaVersion)'." }
if ($identity.runtimeIdentifier -ne "win-x64") { throw "Wave 13 manifest requires win-x64." }
if ($identity.dnp3IncludedInProductGraph -ne $true) { throw "Release identity must preserve the audited DNP3 product dependency." }
if ($identity.dnp3CommercialGate -ne "blocked") { throw "DNP3 commercial gate must remain blocked until clearance is recorded." }
if ($identity.commercialDistributionAuthorized -ne $false) { throw "Commercial distribution cannot be authorized while the DNP3 gate is blocked." }
if ([string]::IsNullOrWhiteSpace($ExpectedPublisher)) { throw "ExpectedPublisher is required." }
if ($SourceSha -notmatch '^[0-9a-fA-F]{40}$') { throw "SourceSha must be a full 40-character Git commit SHA." }
$SourceSha = $SourceSha.ToLowerInvariant()

$releaseMetadataPath = Join-Path $root "release-metadata.json"
if (-not (Test-Path -LiteralPath $releaseMetadataPath -PathType Leaf)) {
    throw "Signed release metadata is missing: release-metadata.json"
}
$releaseMetadata = Get-Content -LiteralPath $releaseMetadataPath -Raw | ConvertFrom-Json
if ($releaseMetadata.schemaVersion -ne 1) { throw "Unsupported release metadata schemaVersion '$($releaseMetadata.schemaVersion)'." }
if ($releaseMetadata.signingState -ne 'signed-return') { throw "Release metadata signingState must be signed-return." }
if ([string]$releaseMetadata.sourceSha -ne $SourceSha) { throw "Release metadata source SHA does not match SourceSha." }
if ([string]$releaseMetadata.expectedPublisher -ne $ExpectedPublisher) { throw "Release metadata publisher does not match ExpectedPublisher." }
if ([string]$releaseMetadata.product -ne [string]$identity.product -or
    [string]$releaseMetadata.version -ne [string]$identity.version -or
    [string]$releaseMetadata.runtimeIdentifier -ne [string]$identity.runtimeIdentifier -or
    [string]$releaseMetadata.packageFormat -ne [string]$identity.packageFormat -or
    [string]$releaseMetadata.productDirectory -ne 'product' -or
    [string]$releaseMetadata.authorityDirectory -ne 'authority') {
    throw "Release metadata identity differs from release/release-identity.json."
}
if ($releaseMetadata.dnp3IncludedInProductGraph -ne $true -or
    $releaseMetadata.dnp3CommercialGate -ne 'blocked' -or
    $releaseMetadata.commercialDistributionAuthorized -ne $false) {
    throw "Release metadata does not preserve the audited DNP3 commercial gate."
}

$requiredRoles = [ordered]@{
    "product/Scada.Api.exe" = "product-host"
    "product/wwwroot/index.html" = "web-entry"
    "product/wwwroot/pyodide/pyodide.js" = "pyodide-runtime-entry"
    "authority/EliteSCADA.LicenseGenerator.exe" = "license-generator"
    "release-metadata.json" = "release-metadata"
}

foreach ($relativePath in $requiredRoles.Keys) {
    $fullPath = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Required signed-release file is missing: $relativePath"
    }
}

$files = @(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {
    $_.FullName -ne (Join-Path $root "release-manifest.json")
} | Sort-Object FullName)

$artifacts = @($files | ForEach-Object {
    $file = $_
    $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\\', '/')
    $isPe = Test-WindowsPortableExecutable -Path $file.FullName
    $signature = if ($isPe) { Get-AuthenticodeSignature -LiteralPath $file.FullName } else { $null }
    $timestampEvidence = if ($isPe) {
        Get-WindowsRfc3161TimestampEvidence -Path $file.FullName
    }
    else {
        $null
    }
    $role = if ($requiredRoles.Contains($relativePath)) { $requiredRoles[$relativePath] } else { "payload" }
    $packageRole = if ($relativePath.StartsWith('product/', [StringComparison]::Ordinal)) {
        'product'
    }
    elseif ($relativePath.StartsWith('authority/', [StringComparison]::Ordinal)) {
        'authority'
    }
    else {
        'shared'
    }

    [ordered]@{
        path = $relativePath
        role = $role
        packageRole = $packageRole
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        sizeBytes = $file.Length
        pe = $isPe
        authenticodeRequired = $isPe
        expectedPublisher = if ($isPe) { $ExpectedPublisher } else { $null }
        signerCertificateSubject = if ($isPe -and $null -ne $signature.SignerCertificate) {
            $signature.SignerCertificate.Subject
        } else { $null }
        signerCertificateThumbprint = if ($isPe -and $null -ne $signature.SignerCertificate) {
            $signature.SignerCertificate.Thumbprint.ToLowerInvariant()
        } else { $null }
        trustedTimestampRequired = $isPe
        timestampProtocol = if ($isPe) { "RFC3161" } else { $null }
        timestampCertificateSubject = if ($null -ne $timestampEvidence) {
            $timestampEvidence.SignerCertificateSubject
        } else { $null }
        timestampCertificateThumbprint = if ($null -ne $timestampEvidence) {
            $timestampEvidence.SignerCertificateThumbprint
        } else { $null }
        rfc3161TimestampUtc = if ($null -ne $timestampEvidence) {
            $timestampEvidence.TimestampUtc
        } else { $null }
        rfc3161TokenSha256 = if ($null -ne $timestampEvidence) {
            $timestampEvidence.TokenSha256
        } else { $null }
    }
})

$manifest = [ordered]@{
    schemaVersion = 1
    verifierSchemaVersion = 1
    product = [string]$identity.product
    version = [string]$identity.version
    sourceSha = $SourceSha
    runtimeIdentifier = [string]$identity.runtimeIdentifier
    packageFormat = [string]$identity.packageFormat
    signingState = "signed-return"
    dnp3IncludedInProductGraph = [bool]$identity.dnp3IncludedInProductGraph
    dnp3CommercialGate = [string]$identity.dnp3CommercialGate
    commercialDistributionAuthorized = [bool]$identity.commercialDistributionAuthorized
    expectedPublisher = $ExpectedPublisher
    artifacts = $artifacts
}

$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
Write-Host "Release manifest written to $OutputPath"
Write-Host "Manifest artifacts: $($artifacts.Count)"
Write-Host "Commercial distribution authorized: $($manifest.commercialDistributionAuthorized)"
