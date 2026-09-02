[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CandidateRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "WindowsReleaseVerification.ps1")

$candidateRootResolved = (Resolve-Path -LiteralPath $CandidateRoot).Path
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$releaseIdentity = Get-Content -LiteralPath (Join-Path $repositoryRoot 'release/release-identity.json') -Raw | ConvertFrom-Json
$metadataPath = Join-Path $candidateRootResolved "candidate-metadata.json"
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw "Candidate metadata is missing: $metadataPath"
}

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ($metadata.schemaVersion -ne 1) { throw "Unsupported candidate metadata schemaVersion '$($metadata.schemaVersion)'." }
if ([string]$metadata.product -ne 'EliteSCADA') { throw "Candidate product identity must be EliteSCADA." }
if ([string]::IsNullOrWhiteSpace([string]$metadata.version)) { throw "Candidate version is required." }
if ([string]$metadata.sourceSha -notmatch '^[0-9a-f]{40}$') { throw "Candidate sourceSha must be a lowercase full Git SHA." }
if ($metadata.runtimeIdentifier -ne "win-x64") { throw "Candidate runtimeIdentifier must be win-x64." }
if ($metadata.packageFormat -ne "zip") { throw "Candidate packageFormat must be zip." }
if ($metadata.signingState -ne "unsigned-candidate") { throw "Candidate structure validation expects explicit unsigned-candidate state." }
if ($metadata.dnp3IncludedInProductGraph -ne $true) { throw "Candidate metadata must record the audited transitive DNP3 product dependency." }
if ($metadata.dnp3CommercialGate -ne "blocked") { throw "Candidate metadata must preserve the blocked DNP3 commercial-license gate." }
if ($metadata.commercialDistributionAuthorized -ne $false) { throw "Candidate must not be marked for commercial distribution while the DNP3 gate is blocked." }
if ($releaseIdentity.schemaVersion -ne 1 -or
    [string]$metadata.product -ne [string]$releaseIdentity.product -or
    [string]$metadata.version -ne [string]$releaseIdentity.version -or
    [string]$metadata.runtimeIdentifier -ne [string]$releaseIdentity.runtimeIdentifier -or
    [string]$metadata.packageFormat -ne [string]$releaseIdentity.packageFormat -or
    $releaseIdentity.dnp3IncludedInProductGraph -ne $true -or
    $releaseIdentity.dnp3CommercialGate -ne 'blocked' -or
    $releaseIdentity.commercialDistributionAuthorized -ne $false) {
    throw 'Candidate metadata identity differs from release/release-identity.json.'
}
if ([string]$metadata.productDirectory -ne 'product' -or [string]$metadata.authorityDirectory -ne 'authority') {
    throw 'Candidate directory roles must remain product and authority.'
}

$productRoot = Join-Path $candidateRootResolved "product"
$authorityRoot = Join-Path $candidateRootResolved "authority"
$requiredFiles = @(
    (Join-Path $productRoot "Scada.Api.exe"),
    (Join-Path $productRoot "wwwroot/index.html"),
    (Join-Path $productRoot "wwwroot/pyodide/pyodide.js"),
    (Join-Path $authorityRoot "EliteSCADA.LicenseGenerator.exe")
)

foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required candidate file is missing: $requiredFile"
    }
}

$privateMaterialPatterns = @(
    '*.pfx', '*.p12', '*.p8', '*.key', '*.pem'
)
$privateMaterial = foreach ($pattern in $privateMaterialPatterns) {
    Get-ChildItem -LiteralPath $candidateRootResolved -File -Recurse -Filter $pattern
}
if ($privateMaterial) {
    throw "Private-key/certificate container material must not be present in a normal candidate artifact: $($privateMaterial.FullName -join ', ')"
}

$productPeFiles = @(Get-ChildItem -LiteralPath $productRoot -File -Recurse | Where-Object {
    Test-WindowsPortableExecutable -Path $_.FullName
})
if ($productPeFiles.Count -eq 0) { throw "Customer candidate contains no PE files." }

$authorityPeFiles = @(Get-ChildItem -LiteralPath $authorityRoot -File -Recurse | Where-Object {
    Test-WindowsPortableExecutable -Path $_.FullName
})
if ($authorityPeFiles.Count -eq 0) { throw "Authority candidate contains no PE files." }

foreach ($peFile in @($productPeFiles) + @($authorityPeFiles)) {
    $layout = Get-WindowsPeSigningLayout -Path $peFile.FullName
    if ($layout.CertificateTableOffset -ne 0 -or $layout.CertificateTableSize -ne 0) {
        throw "Unsigned candidate PE unexpectedly contains an Authenticode certificate table: $($peFile.FullName)"
    }
}

Write-Host "Candidate structure validation passed."
Write-Host "Product PE files: $($productPeFiles.Count)"
Write-Host "Authority PE files: $($authorityPeFiles.Count)"
Write-Host "Commercial distribution authorized: $($metadata.commercialDistributionAuthorized)"
