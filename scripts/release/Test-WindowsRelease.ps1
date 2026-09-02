[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReleaseRoot,
    [string]$ManifestPath = (Join-Path $ReleaseRoot "release-manifest.json")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Test-PortableExecutable {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($stream.Length -lt 2) { return $false }
        $first = $stream.ReadByte()
        $second = $stream.ReadByte()
        return $first -eq 0x4D -and $second -eq 0x5A
    }
    finally {
        $stream.Dispose()
    }
}

$root = (Resolve-Path -LiteralPath $ReleaseRoot).Path
$manifestFullPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$manifest = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json

if ($manifest.schemaVersion -ne 1) { throw "Unsupported release manifest schemaVersion '$($manifest.schemaVersion)'." }
if ($manifest.verifierSchemaVersion -ne 1) { throw "Unsupported verifierSchemaVersion '$($manifest.verifierSchemaVersion)'." }
if ($manifest.runtimeIdentifier -ne "win-x64") { throw "Release manifest runtimeIdentifier must be win-x64." }
if ($manifest.packageFormat -ne "zip") { throw "Release manifest packageFormat must be zip." }
if ($manifest.customerPackageIncludesDnp3 -ne $false) { throw "Wave 13 customer release must exclude DNP3 until commercial clearance is recorded." }
if ([string]::IsNullOrWhiteSpace([string]$manifest.expectedPublisher)) { throw "Manifest expectedPublisher is required." }
if ([string]$manifest.sourceSha -notmatch '^[0-9a-f]{40}$') { throw "Manifest sourceSha must be a full Git SHA." }

$manifestEntries = @($manifest.artifacts)
if ($manifestEntries.Count -eq 0) { throw "Release manifest contains no artifacts." }

$byPath = @{}
foreach ($artifact in $manifestEntries) {
    $relativePath = ([string]$artifact.path).Replace('\\', '/')
    if ([string]::IsNullOrWhiteSpace($relativePath)) { throw "Manifest contains an empty artifact path." }
    if ($relativePath.StartsWith('/') -or $relativePath.Contains('../') -or $relativePath.Contains('..\\')) {
        throw "Manifest artifact path escapes the release root: $relativePath"
    }
    if ($byPath.ContainsKey($relativePath)) { throw "Manifest contains duplicate artifact path: $relativePath" }
    $byPath[$relativePath] = $artifact
}

$requiredPaths = @(
    'product/Scada.Api.exe',
    'product/wwwroot/index.html',
    'product/wwwroot/pyodide/pyodide.js',
    'authority/EliteSCADA.LicenseGenerator.exe',
    'candidate-metadata.json'
)
foreach ($requiredPath in $requiredPaths) {
    if (-not $byPath.ContainsKey($requiredPath)) { throw "Manifest is missing required artifact: $requiredPath" }
}

$actualFiles = Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {
    $_.FullName -ne $manifestFullPath
}
$actualByPath = @{}
foreach ($file in $actualFiles) {
    $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\\', '/')
    if ($actualByPath.ContainsKey($relativePath)) { throw "Duplicate release path encountered: $relativePath" }
    $actualByPath[$relativePath] = $file
}

foreach ($manifestPathKey in $byPath.Keys) {
    if (-not $actualByPath.ContainsKey($manifestPathKey)) {
        throw "Required manifest artifact is missing from release: $manifestPathKey"
    }
}
foreach ($actualPathKey in $actualByPath.Keys) {
    if (-not $byPath.ContainsKey($actualPathKey)) {
        $file = $actualByPath[$actualPathKey]
        if (Test-PortableExecutable -Path $file.FullName) {
            throw "Unexpected executable/PE file is present in release: $actualPathKey"
        }
        throw "Unexpected undeclared file is present in release: $actualPathKey"
    }
}

foreach ($pathKey in ($byPath.Keys | Sort-Object)) {
    $artifact = $byPath[$pathKey]
    $file = $actualByPath[$pathKey]
    $actualHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $expectedHash = ([string]$artifact.sha256).ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "SHA-256 mismatch for $pathKey. Expected $expectedHash, actual $actualHash."
    }

    $isPe = Test-PortableExecutable -Path $file.FullName
    if ([bool]$artifact.pe -ne $isPe) {
        throw "Manifest PE classification mismatch for $pathKey."
    }

    if ($isPe -and -not [bool]$artifact.authenticodeRequired) {
        throw "PE artifact must require Authenticode: $pathKey"
    }

    if ([bool]$artifact.authenticodeRequired) {
        if (-not $isPe) { throw "Authenticode requirement was declared for non-PE artifact: $pathKey" }

        $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
            throw "Authenticode signature is not valid for $pathKey. Status: $($signature.Status); message: $($signature.StatusMessage)"
        }
        if ($null -eq $signature.SignerCertificate) {
            throw "Authenticode signer certificate is missing for $pathKey."
        }

        $expectedPublisher = [string]$artifact.expectedPublisher
        if ([string]::IsNullOrWhiteSpace($expectedPublisher)) {
            throw "Expected publisher is missing from manifest entry: $pathKey"
        }
        if ($signature.SignerCertificate.Subject -ne $expectedPublisher) {
            throw "Publisher mismatch for $pathKey. Expected '$expectedPublisher', actual '$($signature.SignerCertificate.Subject)'."
        }
        if ($expectedPublisher -ne [string]$manifest.expectedPublisher) {
            throw "Artifact publisher expectation differs from release publisher for $pathKey."
        }

        if (-not [bool]$artifact.trustedTimestampRequired) {
            throw "PE artifact must require a trusted timestamp: $pathKey"
        }
        if ($null -eq $signature.TimeStamperCertificate) {
            throw "Trusted timestamp evidence is missing for $pathKey."
        }
    }
}

$forbiddenPrivateMaterial = Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {
    $_.Extension.ToLowerInvariant() -in @('.pfx', '.p12', '.p8', '.key', '.pem')
}
if ($forbiddenPrivateMaterial) {
    throw "Private signing material is present in the release: $($forbiddenPrivateMaterial.FullName -join ', ')"
}

$forbiddenDnp3 = Get-ChildItem -LiteralPath (Join-Path $root 'product') -File -Recurse | Where-Object {
    $_.Name -match '(?i)dnp3' -or $_.FullName -match '(?i)Scada\.Drivers\.Dnp3'
}
if ($forbiddenDnp3) {
    throw "Commercially gated DNP3 content is present in the customer release: $($forbiddenDnp3.FullName -join ', ')"
}

Write-Host "Wave 13 release verification passed."
Write-Host "Product: $($manifest.product) $($manifest.version)"
Write-Host "Source SHA: $($manifest.sourceSha)"
Write-Host "Publisher: $($manifest.expectedPublisher)"
Write-Host "Artifacts verified: $($manifestEntries.Count)"
