[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$OutputRoot = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path "artifacts/wave13"),
    [string]$SourceSha
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

$identityPath = Join-Path $RepositoryRoot "release/release-identity.json"
if (-not (Test-Path -LiteralPath $identityPath -PathType Leaf)) {
    throw "Release identity not found: $identityPath"
}

$identity = Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json
if ($identity.schemaVersion -ne 1) { throw "Unsupported release identity schemaVersion '$($identity.schemaVersion)'." }
if ($identity.runtimeIdentifier -ne "win-x64") { throw "Wave 13 requires runtimeIdentifier win-x64." }
if ($identity.packageFormat -ne "zip") { throw "Wave 13 candidate builder currently supports packageFormat zip only." }
if ($identity.customerPackageIncludesDnp3 -ne $false) { throw "DNP3 must remain excluded from the Wave 13 customer package until the commercial gate is cleared." }

if ([string]::IsNullOrWhiteSpace($SourceSha)) {
    Push-Location $RepositoryRoot
    try {
        $SourceSha = (& git rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($SourceSha)) {
            throw "Unable to resolve source SHA from git."
        }
    }
    finally {
        Pop-Location
    }
}

$version = [string]$identity.version
$rid = [string]$identity.runtimeIdentifier
$candidateRoot = Join-Path $OutputRoot "candidate"
$productRoot = Join-Path $candidateRoot "product"
$authorityRoot = Join-Path $candidateRoot "authority"
$webRoot = Join-Path $RepositoryRoot "web/scada-web"
$webDist = Join-Path $webRoot "dist"

if (Test-Path -LiteralPath $candidateRoot) {
    Remove-Item -LiteralPath $candidateRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $productRoot -Force | Out-Null
New-Item -ItemType Directory -Path $authorityRoot -Force | Out-Null

Write-Host "Building EliteSCADA Web payload..."
Push-Location $webRoot
try {
    Invoke-Checked -FilePath "npm" -Arguments @("ci")
    Invoke-Checked -FilePath "npm" -Arguments @("run", "build")
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath (Join-Path $webDist "index.html") -PathType Leaf)) {
    throw "Vite build did not produce dist/index.html."
}
if (-not (Test-Path -LiteralPath (Join-Path $webDist "pyodide/pyodide.js") -PathType Leaf)) {
    throw "Pinned Pyodide runtime was not included in the Web build."
}

Write-Host "Publishing EliteSCADA product host for $rid..."
$productPublishArguments = @(
    "publish",
    (Join-Path $RepositoryRoot "src/Scada.Api/Scada.Api.csproj"),
    "-c", "Release",
    "-r", $rid,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-p:Version=$version",
    "-o", $productRoot
)
Invoke-Checked -FilePath "dotnet" -Arguments $productPublishArguments

$publishedProductExe = Join-Path $productRoot "Scada.Api.exe"
if (-not (Test-Path -LiteralPath $publishedProductExe -PathType Leaf)) {
    throw "Expected product executable was not published: $publishedProductExe"
}

$packagedWebRoot = Join-Path $productRoot "wwwroot"
New-Item -ItemType Directory -Path $packagedWebRoot -Force | Out-Null
Copy-Item -Path (Join-Path $webDist "*") -Destination $packagedWebRoot -Recurse -Force

Write-Host "Publishing graphical License Generator for $rid..."
$authorityPublishArguments = @(
    "publish",
    (Join-Path $RepositoryRoot "src/Scada.LicenseGenerator/Scada.LicenseGenerator.csproj"),
    "-c", "Release",
    "-r", $rid,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-p:Version=$version",
    "-o", $authorityRoot
)
Invoke-Checked -FilePath "dotnet" -Arguments $authorityPublishArguments

$licenseGeneratorExe = Join-Path $authorityRoot "EliteSCADA.LicenseGenerator.exe"
if (-not (Test-Path -LiteralPath $licenseGeneratorExe -PathType Leaf)) {
    throw "Expected License Generator executable was not published: $licenseGeneratorExe"
}

$forbiddenProductFiles = Get-ChildItem -LiteralPath $productRoot -File -Recurse | Where-Object {
    $_.Name -match "(?i)dnp3" -or $_.FullName -match "(?i)Scada\.Drivers\.Dnp3"
}
if ($forbiddenProductFiles) {
    $paths = $forbiddenProductFiles.FullName -join [Environment]::NewLine
    throw "Commercially gated DNP3 content was found in the customer candidate package:`n$paths"
}

$metadata = [ordered]@{
    schemaVersion = 1
    product = [string]$identity.product
    version = $version
    sourceSha = $SourceSha
    runtimeIdentifier = $rid
    packageFormat = [string]$identity.packageFormat
    signingState = "unsigned-candidate"
    customerPackageIncludesDnp3 = $false
    productDirectory = "product"
    authorityDirectory = "authority"
}
$metadataPath = Join-Path $candidateRoot "candidate-metadata.json"
$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding utf8NoBOM

Write-Host "Wave 13 unsigned candidate created at $candidateRoot"
Write-Host "Product executable: $publishedProductExe"
Write-Host "Authority executable: $licenseGeneratorExe"
Write-Host "Source SHA: $SourceSha"
Write-Host "Version: $version"
