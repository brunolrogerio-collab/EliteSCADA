[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SignedRoot,
    [Parameter(Mandatory = $true)][string]$SourceSha,
    [Parameter(Mandatory = $true)][string]$ExpectedPublisher,
    [string]$OutputPath = (Join-Path $SignedRoot "release-manifest.json")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = (Resolve-Path -LiteralPath $SignedRoot).Path
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$identityPath = Join-Path $repositoryRoot "release/release-identity.json"
$identity = Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json

if ($identity.schemaVersion -ne 1) { throw "Unsupported release identity schemaVersion '$($identity.schemaVersion)'." }
if ($identity.runtimeIdentifier -ne "win-x64") { throw "Wave 13 manifest requires win-x64." }
if ([string]::IsNullOrWhiteSpace($ExpectedPublisher)) { throw "ExpectedPublisher is required." }
if ($SourceSha -notmatch '^[0-9a-fA-F]{40}$') { throw "SourceSha must be a full 40-character Git commit SHA." }

$requiredRoles = [ordered]@{
    "product/Scada.Api.exe" = "product-host"
    "product/wwwroot/index.html" = "web-entry"
    "product/wwwroot/pyodide/pyodide.js" = "pyodide-runtime-entry"
    "authority/EliteSCADA.LicenseGenerator.exe" = "license-generator"
    "candidate-metadata.json" = "candidate-metadata"
}

foreach ($relativePath in $requiredRoles.Keys) {
    $fullPath = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Required signed-release file is missing: $relativePath"
    }
}

$files = Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {
    $_.FullName -ne (Join-Path $root "release-manifest.json")
} | Sort-Object FullName

$artifacts = foreach ($file in $files) {
    $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\\', '/')
    $extension = $file.Extension.ToLowerInvariant()
    $isPe = $extension -in @('.exe', '.dll')
    $role = if ($requiredRoles.Contains($relativePath)) { $requiredRoles[$relativePath] } else { "payload" }

    [ordered]@{
        path = $relativePath
        role = $role
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        sizeBytes = $file.Length
        pe = $isPe
        authenticodeRequired = $isPe
        expectedPublisher = if ($isPe) { $ExpectedPublisher } else { $null }
        trustedTimestampRequired = $isPe
    }
}

$manifest = [ordered]@{
    schemaVersion = 1
    verifierSchemaVersion = 1
    product = [string]$identity.product
    version = [string]$identity.version
    sourceSha = $SourceSha.ToLowerInvariant()
    runtimeIdentifier = [string]$identity.runtimeIdentifier
    packageFormat = [string]$identity.packageFormat
    customerPackageIncludesDnp3 = [bool]$identity.customerPackageIncludesDnp3
    expectedPublisher = $ExpectedPublisher
    artifacts = @($artifacts)
}

$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
Write-Host "Release manifest written to $OutputPath"
Write-Host "Manifest artifacts: $($artifacts.Count)"
