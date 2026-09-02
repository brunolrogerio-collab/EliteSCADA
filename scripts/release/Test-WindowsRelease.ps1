[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReleaseRoot,
    [Parameter(Mandatory = $true)][string]$ExpectedSourceSha,
    [Parameter(Mandatory = $true)][string]$ExpectedPublisher,
    [ValidateSet('all', 'product', 'authority')][string]$PackageRole = 'all',
    [string]$ManifestPath = (Join-Path $ReleaseRoot "release-manifest.json")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "WindowsReleaseVerification.ps1")

if ($ExpectedSourceSha -notmatch '^[0-9a-fA-F]{40}$') {
    throw "ExpectedSourceSha must be a full 40-character Git commit SHA."
}
if ([string]::IsNullOrWhiteSpace($ExpectedPublisher)) {
    throw "ExpectedPublisher is required."
}
$expectedSha = $ExpectedSourceSha.ToLowerInvariant()

$root = (Resolve-Path -LiteralPath $ReleaseRoot).Path
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$releaseIdentity = Get-Content -LiteralPath (Join-Path $repositoryRoot 'release/release-identity.json') -Raw | ConvertFrom-Json
$manifestFullPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$canonicalManifestPath = [IO.Path]::GetFullPath((Join-Path $root "release-manifest.json"))
if (-not $manifestFullPath.Equals($canonicalManifestPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "ManifestPath must resolve to release-manifest.json at the release root."
}

$manifest = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1) { throw "Unsupported release manifest schemaVersion '$($manifest.schemaVersion)'." }
if ($manifest.verifierSchemaVersion -ne 1) { throw "Unsupported verifierSchemaVersion '$($manifest.verifierSchemaVersion)'." }
if ($manifest.product -ne 'EliteSCADA') { throw "Release manifest product identity must be EliteSCADA." }
if ([string]::IsNullOrWhiteSpace([string]$manifest.version)) { throw "Release manifest version is required." }
if ($manifest.runtimeIdentifier -ne "win-x64") { throw "Release manifest runtimeIdentifier must be win-x64." }
if ($manifest.packageFormat -ne "zip") { throw "Release manifest packageFormat must be zip." }
if ($manifest.signingState -ne 'signed-return') { throw "Release manifest signingState must be signed-return." }
if ($manifest.dnp3IncludedInProductGraph -ne $true) { throw "Manifest must record the audited transitive DNP3 product dependency." }
if ($manifest.dnp3CommercialGate -ne "blocked") { throw "Manifest must preserve the blocked DNP3 commercial-license gate." }
if ($manifest.commercialDistributionAuthorized -ne $false) { throw "Commercial distribution cannot be authorized while the DNP3 gate is blocked." }
if ([string]$manifest.sourceSha -ne $expectedSha) { throw "Manifest sourceSha does not match ExpectedSourceSha." }
if ([string]$manifest.expectedPublisher -ne $ExpectedPublisher) { throw "Manifest publisher does not match ExpectedPublisher." }
if ($releaseIdentity.schemaVersion -ne 1 -or
    [string]$manifest.product -ne [string]$releaseIdentity.product -or
    [string]$manifest.version -ne [string]$releaseIdentity.version -or
    [string]$manifest.runtimeIdentifier -ne [string]$releaseIdentity.runtimeIdentifier -or
    [string]$manifest.packageFormat -ne [string]$releaseIdentity.packageFormat -or
    $releaseIdentity.dnp3IncludedInProductGraph -ne $true -or
    $releaseIdentity.dnp3CommercialGate -ne 'blocked' -or
    $releaseIdentity.commercialDistributionAuthorized -ne $false) {
    throw 'Release manifest identity differs from release/release-identity.json.'
}

$releaseMetadataPath = Join-Path $root 'release-metadata.json'
if (-not (Test-Path -LiteralPath $releaseMetadataPath -PathType Leaf)) {
    throw "Release metadata is missing."
}
$releaseMetadata = Get-Content -LiteralPath $releaseMetadataPath -Raw | ConvertFrom-Json
if ($releaseMetadata.schemaVersion -ne 1 -or $releaseMetadata.signingState -ne 'signed-return') {
    throw "Release metadata schema/signing state is invalid."
}
if ([string]$releaseMetadata.product -ne [string]$manifest.product -or
    [string]$releaseMetadata.version -ne [string]$manifest.version -or
    [string]$releaseMetadata.sourceSha -ne $expectedSha -or
    [string]$releaseMetadata.runtimeIdentifier -ne [string]$manifest.runtimeIdentifier -or
    [string]$releaseMetadata.packageFormat -ne [string]$manifest.packageFormat -or
    [string]$releaseMetadata.expectedPublisher -ne $ExpectedPublisher -or
    [string]$releaseMetadata.productDirectory -ne 'product' -or
    [string]$releaseMetadata.authorityDirectory -ne 'authority') {
    throw "Release metadata identity differs from the trusted manifest expectations."
}
if ($releaseMetadata.dnp3IncludedInProductGraph -ne $true -or
    $releaseMetadata.dnp3CommercialGate -ne 'blocked' -or
    $releaseMetadata.commercialDistributionAuthorized -ne $false) {
    throw "Release metadata does not preserve the audited DNP3 commercial gate."
}

$manifestEntries = @($manifest.artifacts)
if ($manifestEntries.Count -eq 0) { throw "Release manifest contains no artifacts." }

$byPath = @{}
foreach ($artifact in $manifestEntries) {
    $relativePath = [string]$artifact.path
    if ([string]::IsNullOrWhiteSpace($relativePath)) { throw "Manifest contains an empty artifact path." }
    if ($relativePath.Contains('\') -or [IO.Path]::IsPathRooted($relativePath)) {
        throw "Manifest artifact path is not canonical and relative: $relativePath"
    }
    foreach ($segment in $relativePath.Split('/')) {
        if ([string]::IsNullOrEmpty($segment) -or $segment -eq '.' -or $segment -eq '..') {
            throw "Manifest artifact path contains an invalid segment: $relativePath"
        }
    }
    if ($relativePath -eq 'release-manifest.json') {
        throw "Release manifest must not list itself as an artifact."
    }
    if ($byPath.ContainsKey($relativePath)) { throw "Manifest contains duplicate artifact path: $relativePath" }
    if ([string]::IsNullOrWhiteSpace([string]$artifact.role)) { throw "Manifest artifact role is missing: $relativePath" }
    if ([string]$artifact.packageRole -notin @('product', 'authority', 'shared')) {
        throw "Manifest artifact packageRole is invalid for $relativePath."
    }
    if ([string]$artifact.sha256 -notmatch '^[0-9a-f]{64}$') {
        throw "Manifest artifact SHA-256 is invalid for $relativePath."
    }
    if ($artifact.pe -isnot [bool] -or
        $artifact.authenticodeRequired -isnot [bool] -or
        $artifact.trustedTimestampRequired -isnot [bool]) {
        throw "Manifest signature flags must be JSON booleans for $relativePath."
    }
    $byPath[$relativePath] = $artifact
}

$requiredRoles = [ordered]@{
    'product/Scada.Api.exe' = 'product-host'
    'product/wwwroot/index.html' = 'web-entry'
    'product/wwwroot/pyodide/pyodide.js' = 'pyodide-runtime-entry'
    'authority/EliteSCADA.LicenseGenerator.exe' = 'license-generator'
    'release-metadata.json' = 'release-metadata'
}
$requiredPaths = switch ($PackageRole) {
    'product' { @('product/Scada.Api.exe', 'product/wwwroot/index.html', 'product/wwwroot/pyodide/pyodide.js', 'release-metadata.json') }
    'authority' { @('authority/EliteSCADA.LicenseGenerator.exe', 'release-metadata.json') }
    default { @($requiredRoles.Keys) }
}
foreach ($requiredPath in $requiredPaths) {
    if (-not $byPath.ContainsKey($requiredPath)) { throw "Manifest is missing required artifact: $requiredPath" }
    if ([string]$byPath[$requiredPath].role -ne $requiredRoles[$requiredPath]) {
        throw "Manifest required artifact role is invalid for $requiredPath."
    }
}

$selectedByPath = @{}
foreach ($pathKey in $byPath.Keys) {
    $artifact = $byPath[$pathKey]
    if ($PackageRole -eq 'all' -or
        [string]$artifact.packageRole -eq $PackageRole -or
        [string]$artifact.packageRole -eq 'shared') {
        $selectedByPath[$pathKey] = $artifact
    }
}

$actualFiles = @(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {
    -not $_.FullName.Equals($manifestFullPath, [StringComparison]::OrdinalIgnoreCase)
})
$actualByPath = @{}
foreach ($file in $actualFiles) {
    $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
    if ($actualByPath.ContainsKey($relativePath)) { throw "Duplicate release path encountered: $relativePath" }
    $actualByPath[$relativePath] = $file
}

foreach ($manifestPathKey in $selectedByPath.Keys) {
    if (-not $actualByPath.ContainsKey($manifestPathKey)) {
        throw "Required manifest artifact is missing from release: $manifestPathKey"
    }
}
foreach ($actualPathKey in $actualByPath.Keys) {
    if (-not $selectedByPath.ContainsKey($actualPathKey)) {
        $file = $actualByPath[$actualPathKey]
        if (Test-WindowsPortableExecutable -Path $file.FullName) {
            throw "Unexpected executable/PE file is present in release: $actualPathKey"
        }
        throw "Unexpected undeclared file is present in release: $actualPathKey"
    }
}

# Validate the complete content allowlist and hashes before checking signatures. This
# ordering gives missing/tampered/unexpected-content failures their own deterministic
# evidence even when a negative-test fixture is intentionally unsigned.
$pePaths = [Collections.Generic.List[string]]::new()
foreach ($pathKey in ($selectedByPath.Keys | Sort-Object)) {
    $artifact = $selectedByPath[$pathKey]
    $file = $actualByPath[$pathKey]
    $actualHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne [string]$artifact.sha256) {
        throw "SHA-256 mismatch for $pathKey. Expected $($artifact.sha256), actual $actualHash."
    }
    if ([long]$artifact.sizeBytes -ne $file.Length) {
        throw "Size mismatch for $pathKey. Expected $($artifact.sizeBytes), actual $($file.Length)."
    }

    $isPe = Test-WindowsPortableExecutable -Path $file.FullName
    if ([bool]$artifact.pe -ne $isPe) { throw "Manifest PE classification mismatch for $pathKey." }

    if ($isPe) {
        if (-not [bool]$artifact.authenticodeRequired) { throw "PE artifact must require Authenticode: $pathKey" }
        if (-not [bool]$artifact.trustedTimestampRequired) { throw "PE artifact must require a trusted timestamp: $pathKey" }
        if ([string]$artifact.timestampProtocol -ne 'RFC3161') { throw "PE artifact must declare RFC3161 timestamp protocol: $pathKey" }
        if ([string]$artifact.expectedPublisher -ne $ExpectedPublisher) { throw "PE artifact publisher expectation is not trusted for $pathKey." }
        $pePaths.Add($pathKey)
    }
    else {
        if ([bool]$artifact.authenticodeRequired -or [bool]$artifact.trustedTimestampRequired) {
            throw "Non-PE artifact must not declare Authenticode/timestamp requirements: $pathKey"
        }
        if (-not [string]::IsNullOrEmpty([string]$artifact.expectedPublisher) -or
            -not [string]::IsNullOrEmpty([string]$artifact.timestampProtocol) -or
            -not [string]::IsNullOrEmpty([string]$artifact.signerCertificateSubject) -or
            -not [string]::IsNullOrEmpty([string]$artifact.signerCertificateThumbprint) -or
            -not [string]::IsNullOrEmpty([string]$artifact.timestampCertificateSubject) -or
            -not [string]::IsNullOrEmpty([string]$artifact.timestampCertificateThumbprint) -or
            -not [string]::IsNullOrEmpty([string]$artifact.rfc3161TimestampUtc) -or
            -not [string]::IsNullOrEmpty([string]$artifact.rfc3161TokenSha256)) {
            throw "Non-PE artifact contains PE-only signature expectations: $pathKey"
        }
    }
}

foreach ($pathKey in $pePaths) {
    $file = $actualByPath[$pathKey]
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Authenticode signature is not valid for $pathKey. Status: $($signature.Status); message: $($signature.StatusMessage)"
    }
    if ($null -eq $signature.SignerCertificate) { throw "Authenticode signer certificate is missing for $pathKey." }
    if ($signature.SignerCertificate.Subject -ne $ExpectedPublisher) {
        throw "Publisher mismatch for $pathKey. Expected '$ExpectedPublisher', actual '$($signature.SignerCertificate.Subject)'."
    }
    if ([string]$artifact.signerCertificateSubject -ne $signature.SignerCertificate.Subject) {
        throw "Manifest signer-certificate subject evidence differs from the signed PE for $pathKey."
    }
    $signerThumbprint = $signature.SignerCertificate.Thumbprint.ToLowerInvariant()
    if ([string]$artifact.signerCertificateThumbprint -ne $signerThumbprint) {
        throw "Manifest signer-certificate thumbprint evidence differs from the signed PE for $pathKey."
    }
    if ($null -eq $signature.TimeStamperCertificate) { throw "Trusted timestamp evidence is missing for $pathKey." }
    $timestampEvidence = Get-WindowsRfc3161TimestampEvidence -Path $file.FullName
    if ($null -eq $timestampEvidence) {
        throw "RFC3161 signature timestamp token is missing for $pathKey. Legacy Authenticode countersignatures are not accepted."
    }
    $timestampCertificateThumbprint = $signature.TimeStamperCertificate.Thumbprint.ToLowerInvariant()
    if ($timestampEvidence.SignerCertificateSubject -ne $signature.TimeStamperCertificate.Subject -or
        $timestampEvidence.SignerCertificateThumbprint -ne $timestampCertificateThumbprint) {
        throw "RFC3161 token certificate evidence differs from Windows timestamp trust evidence for $pathKey."
    }
    if ([string]$artifact.timestampCertificateSubject -ne $timestampEvidence.SignerCertificateSubject -or
        [string]$artifact.timestampCertificateThumbprint -ne $timestampEvidence.SignerCertificateThumbprint -or
        [string]$artifact.rfc3161TimestampUtc -ne $timestampEvidence.TimestampUtc -or
        [string]$artifact.rfc3161TokenSha256 -ne $timestampEvidence.TokenSha256) {
        throw "Manifest RFC3161 timestamp evidence differs from the signed PE for $pathKey."
    }
}

$forbiddenPrivateMaterial = @(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {
    $_.Extension.ToLowerInvariant() -in @('.pfx', '.p12', '.p8', '.key', '.pem')
})
if ($forbiddenPrivateMaterial.Count -gt 0) {
    throw "Private signing material is present in the release: $($forbiddenPrivateMaterial.FullName -join ', ')"
}

Write-Host "Wave 13 release verification passed."
Write-Host "Product: $($manifest.product) $($manifest.version)"
Write-Host "Source SHA: $expectedSha"
Write-Host "Publisher: $ExpectedPublisher"
Write-Host "Package role: $PackageRole"
Write-Host "Artifacts verified: $($selectedByPath.Count)"
Write-Host "Commercial distribution authorized: $($manifest.commercialDistributionAuthorized)"
