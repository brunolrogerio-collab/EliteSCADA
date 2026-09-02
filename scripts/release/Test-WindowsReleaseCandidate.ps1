[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CandidateRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$candidateRootResolved = (Resolve-Path -LiteralPath $CandidateRoot).Path
$metadataPath = Join-Path $candidateRootResolved "candidate-metadata.json"
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw "Candidate metadata is missing: $metadataPath"
}

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ($metadata.schemaVersion -ne 1) { throw "Unsupported candidate metadata schemaVersion '$($metadata.schemaVersion)'." }
if ($metadata.runtimeIdentifier -ne "win-x64") { throw "Candidate runtimeIdentifier must be win-x64." }
if ($metadata.signingState -ne "unsigned-candidate") { throw "Candidate structure validation expects explicit unsigned-candidate state." }
if ($metadata.customerPackageIncludesDnp3 -ne $false) { throw "Candidate metadata must explicitly exclude DNP3." }

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

$forbidden = Get-ChildItem -LiteralPath $productRoot -File -Recurse | Where-Object {
    $_.Name -match "(?i)dnp3" -or $_.FullName -match "(?i)Scada\.Drivers\.Dnp3"
}
if ($forbidden) {
    throw "DNP3 content is prohibited from this customer candidate: $($forbidden.FullName -join ', ')"
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

$productPeFiles = Get-ChildItem -LiteralPath $productRoot -File -Recurse | Where-Object { $_.Extension -in '.exe', '.dll' }
if (-not $productPeFiles) { throw "Customer candidate contains no PE files." }

$authorityPeFiles = Get-ChildItem -LiteralPath $authorityRoot -File -Recurse | Where-Object { $_.Extension -in '.exe', '.dll' }
if (-not $authorityPeFiles) { throw "Authority candidate contains no PE files." }

Write-Host "Candidate structure validation passed."
Write-Host "Product PE files: $($productPeFiles.Count)"
Write-Host "Authority PE files: $($authorityPeFiles.Count)"
