[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('launch', 'prepare', 'start', 'status', 'pause', 'stop', 'resume', 'restart', 'diagnose', 'reset')]
    [string]$Command,

    [Parameter()]
    [ValidateSet('audit', 'development')]
    [string]$Profile = 'audit',

    [Parameter()]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$AcceptedHarnessSha,

    [Parameter()]
    [ValidateRange(1024, 65535)]
    [int]$Port = 5173,

    [Parameter()]
    [string]$Checkpoint,

    [Parameter()]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$TrustedRootThumbprint,

    [Parameter()]
    [switch]$Force,

    [Parameter()]
    [switch]$LocalOwnerLaunch
)

$ErrorActionPreference = 'Stop'
if ($Command -eq 'launch') { $Profile = 'development' }
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$productBaseSha = '1f14a57491805a5d976bc9d0bf51393cf1b3ebcd'
$projectName = if ($Profile -eq 'development') { 'elitescada-preview-dev' } else { 'elitescada-preview-a' }
$composeFiles = @(
    (Join-Path $repoRoot 'ci\local\docker-compose.preview.yml'),
    (Join-Path $repoRoot 'ci\local\docker-compose.preview.local.yml')
)
$sessionDirectoryName = if ($Profile -eq 'development') { 'preview-dev' } else { 'preview-audit-a' }
$archiveDirectoryName = if ($Profile -eq 'development') { 'preview-dev-archive' } else { 'preview-audit-archive' }
if ($Profile -eq 'development') {
    $composeFiles += (Join-Path $repoRoot 'ci\local\docker-compose.preview.dev.yml')
}
$artifactRoot = Join-Path $repoRoot 'ci\local\artifacts'
$sessionRoot = Join-Path $artifactRoot $sessionDirectoryName
$sessionFile = Join-Path $sessionRoot 'session.json'
$archiveRoot = Join-Path $artifactRoot $archiveDirectoryName
$dependencyManifestRoot = Join-Path $artifactRoot 'preview-dependencies'
$repoPrefix = $repoRoot.TrimEnd('\') + '\'
Import-Module (Join-Path $PSScriptRoot 'PreviewDependencyIdentity.psm1') -Force

if ($Command -ne 'start' -and $LocalOwnerLaunch) { throw '-LocalOwnerLaunch is valid only for the internal start dispatched by launch.' }

function Invoke-Git([string[]]$GitArguments) {
    $result = Invoke-NativeCommand -FilePath 'git' -Arguments (@('-C', $repoRoot) + $GitArguments)
    if ($LASTEXITCODE -ne 0) { throw "git $($GitArguments -join ' ') failed." }
    return $result
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & $FilePath @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    $global:LASTEXITCODE = $exitCode
    foreach ($item in $output) {
        if ($item -is [System.Management.Automation.ErrorRecord]) {
            Write-Output $item.ToString()
        } else {
            Write-Output $item
        }
    }
}

function Get-HarnessIdentity {
    param([switch]$AllowUnclean)

    $head = (Invoke-Git -GitArguments @('rev-parse', 'HEAD')).Trim()
    $tree = (Invoke-Git -GitArguments @('rev-parse', 'HEAD^{tree}')).Trim()
    $branch = (Invoke-Git -GitArguments @('branch', '--show-current')).Trim()
    $base = (Invoke-Git -GitArguments @('rev-parse', "$productBaseSha^{commit}")).Trim()
    if ($base -ne $productBaseSha) { throw "Expected product checkpoint $productBaseSha is unavailable." }

    $mergeBase = (Invoke-Git -GitArguments @('merge-base', $productBaseSha, 'HEAD')).Trim()
    if ($mergeBase -ne $productBaseSha) {
        throw 'This checkout does not descend from the exact accepted product checkpoint; refusing to run the audit.'
    }

    $allowedRoots = @('.devcontainer/', 'ci/local/', 'scripts/preview/')
    $allowedFiles = @(
        '.gitattributes',
        'docs/LOCAL-FIRST-PROJECT-PREVIEW-HARNESS.md',
        'docs/VISUAL-STUDIO-AI-LOCAL-ELITESCADA-BOOTSTRAP.md',
        'docs/LOCAL-ELITESCADA-OPERATIONS-EVIDENCE.md'
    )
    $changedPaths = @(Invoke-Git -GitArguments @('diff', '--name-only', $productBaseSha, 'HEAD'))
    $productScopeChanges = @()
    foreach ($path in $changedPaths) {
        $normalized = $path.Replace('\', '/')
        $isAllowed = ($allowedFiles -contains $normalized) -or
            @($allowedRoots | Where-Object { $normalized.StartsWith($_, [System.StringComparison]::Ordinal) }).Count -gt 0
        if (-not $isAllowed) {
            $productScopeChanges += $normalized
        }
    }

    $dirty = @(Invoke-Git -GitArguments @('status', '--porcelain', '--untracked-files=all'))
    if ($Profile -eq 'audit' -and $productScopeChanges.Count -gt 0 -and -not $AllowUnclean) {
        throw "Product-scope changes are not allowed in the local preview harness: $($productScopeChanges -join ', ')"
    }
    if ($Profile -eq 'audit' -and $dirty.Count -gt 0 -and -not $AllowUnclean) {
        throw 'The audit requires a clean, committed harness worktree; commit or safely preserve local changes before continuing.'
    }

    return [pscustomobject]@{
        Branch = $branch
        Head = $head
        Tree = $tree
        ProductBase = $productBaseSha
        DirtyPaths = $dirty
        ProductScopeChanges = $productScopeChanges
    }
}

function Get-ComposeArguments {
    $composeArgs = @('--project-name', $projectName)
    foreach ($file in $composeFiles) { $composeArgs += @('--file', $file) }
    return $composeArgs
}

function Invoke-PreviewCompose([string[]]$ComposeArguments) {
    $composeArgs = (Get-ComposeArguments) + $ComposeArguments
    Invoke-NativeCommand -FilePath 'docker' -Arguments (@('compose') + $composeArgs)
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed: $($ComposeArguments -join ' ')" }
}

function Get-StackResources {
    $containers = @(Invoke-NativeCommand -FilePath 'docker' -Arguments @('ps', '--all', '--quiet', '--filter', "label=com.docker.compose.project=$projectName"))
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Docker containers for the dedicated preview project.' }
    $volumes = @(Invoke-NativeCommand -FilePath 'docker' -Arguments @('volume', 'ls', '--quiet', '--filter', "label=com.docker.compose.project=$projectName"))
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Docker volumes for the dedicated preview project.' }
    $networks = @(Invoke-NativeCommand -FilePath 'docker' -Arguments @('network', 'ls', '--quiet', '--filter', "label=com.docker.compose.project=$projectName"))
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Docker networks for the dedicated preview project.' }
    return [pscustomobject]@{ Containers = $containers; Volumes = $volumes; Networks = $networks }
}

function Assert-OtherProfileStopped {
    $otherProject = if ($Profile -eq 'development') { 'elitescada-preview-a' } else { 'elitescada-preview-dev' }
    $otherContainers = @(Invoke-NativeCommand -FilePath 'docker' -Arguments @('ps', '--quiet', '--filter', "label=com.docker.compose.project=$otherProject"))
    if ($LASTEXITCODE -ne 0) { throw 'Could not check the other local Preview profile before using the shared host Web port.' }
    if ($otherContainers.Count -gt 0) {
        throw "PROFILE_PORT_CONFLICT: the other local Preview profile '$otherProject' has running containers. Stop that profile explicitly before starting/resuming this one; no profile was changed."
    }
}

function Get-StackContainers {
    $lines = @(Invoke-NativeCommand -FilePath 'docker' -Arguments @(
        'ps', '--all', '--filter', "label=com.docker.compose.project=$projectName",
        '--format', '{{.ID}}|{{.Names}}|{{.State}}|{{.Status}}'
    ))
    if ($LASTEXITCODE -ne 0) {
        return [pscustomobject]@{ Available = $false; Error = (@($lines) -join ' ').Trim(); Containers = @() }
    }

    $containers = @()
    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace([string]$line)) { continue }
        $parts = ([string]$line).Split('|', 4)
        if ($parts.Count -ne 4) { continue }
        $details = @(Invoke-NativeCommand -FilePath 'docker' -Arguments @(
            'inspect', '--format', '{{index .Config.Labels "com.docker.compose.service"}}|{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}', $parts[0]
        ))
        if ($LASTEXITCODE -ne 0) { $details = @('unknown|unknown') }
        $detailParts = ([string]($details | Select-Object -First 1)).Split('|', 2)
        $containers += [pscustomobject]@{
            id = $parts[0]
            name = $parts[1]
            service = $detailParts[0]
            state = $parts[2]
            status = $parts[3]
            health = if ($detailParts.Count -gt 1) { $detailParts[1] } else { 'unknown' }
        }
    }
    return [pscustomobject]@{ Available = $true; Error = ''; Containers = $containers }
}

function Stop-PreviewContainers {
    $snapshot = Get-StackContainers
    if (-not $snapshot.Available) { throw "Docker Engine is unavailable; cannot safely stop the local workbench. $($snapshot.Error)" }
    foreach ($container in $snapshot.Containers) {
        if ($container.state -eq 'running') {
            Invoke-NativeCommand -FilePath 'docker' -Arguments @('container', 'stop', '--time', '30', $container.id) | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Could not gracefully stop local workbench service '$($container.service)' ($($container.name))." }
        }
    }
    return $snapshot.Containers.Count
}

function Test-HttpEndpoint([string]$Uri) {
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $response = Invoke-WebRequest -Uri $Uri -Method Get -TimeoutSec 5 -UseBasicParsing
        $stopwatch.Stop()
        return [pscustomobject]@{ state = if ([int]$response.StatusCode -ge 200 -and [int]$response.StatusCode -lt 400) { 'healthy' } else { 'unhealthy' }; statusCode = [int]$response.StatusCode; durationMs = $stopwatch.ElapsedMilliseconds }
    } catch {
        $stopwatch.Stop()
        $statusCode = $null
        if ($null -ne $_.Exception.Response -and $null -ne $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        return [pscustomobject]@{ state = if ($null -eq $statusCode) { 'unreachable' } else { 'unhealthy' }; statusCode = $statusCode; durationMs = $stopwatch.ElapsedMilliseconds }
    }
}

function Get-Sha256Text([string]$Text) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function ConvertTo-BashCommandArgument([string]$Script) {
    $normalizedScript = ConvertTo-PreviewBashScriptLf $Script
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($normalizedScript))
    return "echo $encoded | base64 -d | bash"
}

function New-ExplicitTrustedRootPem([string]$Thumbprint) {
    $matching = @(Get-ChildItem Cert:\LocalMachine\Root, Cert:\CurrentUser\Root |
        Where-Object { $_.Thumbprint -eq $Thumbprint.ToUpperInvariant() })
    if ($matching.Count -eq 0) {
        throw 'ENVIRONMENT_PREP_BLOCKED_TLS: the explicitly selected root is absent from the Windows trusted-root stores.'
    }
    $certificate = $matching[0]
    $caExtension = @($certificate.Extensions | Where-Object { $_ -is [Security.Cryptography.X509Certificates.X509BasicConstraintsExtension] } | Select-Object -First 1)
    if ($certificate.Subject -ne $certificate.Issuer -or $caExtension.Count -ne 1 -or -not $caExtension[0].CertificateAuthority -or
        [DateTime]::Now -lt $certificate.NotBefore -or [DateTime]::Now -gt $certificate.NotAfter) {
        throw 'ENVIRONMENT_PREP_BLOCKED_TLS: the selected Windows certificate is not a currently valid root CA.'
    }
    $pemPath = Join-Path ([IO.Path]::GetTempPath()) ("elitescada-preview-root-$([Guid]::NewGuid().ToString('N')).pem")
    $base64 = [Convert]::ToBase64String($certificate.RawData, [Base64FormattingOptions]::InsertLineBreaks)
    [IO.File]::WriteAllText($pemPath, "-----BEGIN CERTIFICATE-----`n$base64`n-----END CERTIFICATE-----`n", [Text.Encoding]::ASCII)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $derSha256 = ([BitConverter]::ToString($sha.ComputeHash($certificate.RawData))).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
    return [pscustomobject]@{ Path = $pemPath; Thumbprint = $Thumbprint.ToLowerInvariant(); DerSha256 = $derSha256 }
}

function Get-PreviewDependencyIdentity($Identity) {
    $tracked = @(Invoke-Git -GitArguments @('ls-files'))
    $manifestPaths = @(
        'global.json',
        'web/scada-web/package.json',
        'web/scada-web/package-lock.json',
        'ci/local/Dockerfile.linux-e2e',
        'ci/local/docker-compose.preview.yml',
        'ci/local/docker-compose.preview.local.yml',
        'ci/local/docker-compose.preview.dev.yml',
        'scripts/preview/run-product-preview.sh'
    )
    $manifestPaths += @($tracked | Where-Object {
        $_ -match '(^|/)([^/]+\.csproj|packages\.lock\.json|NuGet\.Config|Directory\.Build\.(props|targets)|Directory\.Packages\.props)$'
    })
    $manifestPaths = @($manifestPaths | Sort-Object -Unique)

    foreach ($relativePath in $manifestPaths) {
        $filePath = Join-Path $repoRoot ($relativePath.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            throw "Dependency provenance input is missing: $relativePath"
        }
    }

    $dependencyInputs = Get-PreviewDependencyInputIdentity -RepositoryRoot $repoRoot -RelativePaths $manifestPaths
    $inputsSha = $dependencyInputs.InputsSha
    $keyMaterial = "productBase=$productBaseSha`nharnessSha=$($Identity.Head)`nharnessTree=$($Identity.Tree)`ninputsSha=$inputsSha"
    $key = Get-Sha256Text $keyMaterial
    return [pscustomobject]@{
        Key = $key
        InputsSha = $inputsSha
        HarnessSha = $Identity.Head
        HarnessTree = $Identity.Tree
        ProductBase = $Identity.ProductBase
        NodeModulesVolume = "elitescada-preview-deps-node-$key"
        NuGetVolume = "elitescada-preview-deps-nuget-$key"
        ManifestPath = Join-Path $dependencyManifestRoot "$key.json"
    }
}

function Assert-DevelopmentDependencyInputsClean($Identity) {
    if ($Profile -ne 'development') { return }
    $dependencyPattern = '(?i)(^|/)(global\.json|package\.json|package-lock\.json|packages\.lock\.json|NuGet\.Config|Directory\.Build\.(props|targets)|Directory\.Packages\.props|[^/]+\.csproj|Dockerfile\.linux-e2e|docker-compose\.preview(\.local|\.dev)?\.yml|run-product-preview\.sh)$'
    $dirtyDependencyInputs = @($Identity.DirtyPaths | Where-Object { $_ -match $dependencyPattern })
    if ($dirtyDependencyInputs.Count -gt 0) {
        throw "DEV_DEPENDENCY_INPUTS_DIRTY: commit the dependency-input changes before launch/prepare so the exact Git blobs can be prepared. Product source edits may remain uncommitted. Paths: $($dirtyDependencyInputs -join ', ')"
    }
}

function Set-PreviewDependencyEnvironment($Dependency) {
    $env:ELITESCADA_PREVIEW_DEPENDENCY_KEY = $Dependency.Key
    $env:ELITESCADA_PREVIEW_DEPENDENCY_INPUTS_SHA = $Dependency.InputsSha
    $env:ELITESCADA_PREVIEW_HARNESS_SHA = $Dependency.HarnessSha
    $env:ELITESCADA_PREVIEW_PRODUCT_BASE_SHA = $Dependency.ProductBase
    $env:ELITESCADA_PREVIEW_NODE_MODULES_VOLUME = $Dependency.NodeModulesVolume
    $env:ELITESCADA_PREVIEW_NUGET_VOLUME = $Dependency.NuGetVolume
    $env:ELITESCADA_PREVIEW_IMAGE_NAME = Get-PreviewImageName $Dependency
    $env:ELITESCADA_PREVIEW_PORT = "$Port"
}

function Set-SessionComposeEnvironment($Session, $Dependency = $null) {
    $dependencyInputsSha = [string]$Session.dependencyInputsSha
    if ([string]::IsNullOrWhiteSpace($dependencyInputsSha) -and $null -ne $Dependency) {
        $dependencyInputsSha = [string]$Dependency.InputsSha
    }
    if ([string]::IsNullOrWhiteSpace($dependencyInputsSha)) {
        throw 'RESUME_BLOCKED_VERSION_MISMATCH: saved session lacks dependency provenance required by the current operator.'
    }
    $env:ELITESCADA_PREVIEW_DEPENDENCY_KEY = [string]$Session.dependencyKey
    $env:ELITESCADA_PREVIEW_DEPENDENCY_INPUTS_SHA = $dependencyInputsSha
    $env:ELITESCADA_PREVIEW_HARNESS_SHA = [string]$Session.harnessSha
    $env:ELITESCADA_PREVIEW_PRODUCT_BASE_SHA = [string]$Session.productBaseSha
    $env:ELITESCADA_PREVIEW_NODE_MODULES_VOLUME = [string]$Session.nodeModulesVolume
    $env:ELITESCADA_PREVIEW_NUGET_VOLUME = [string]$Session.nugetVolume
    $env:ELITESCADA_PREVIEW_IMAGE_NAME = "$projectName-preview:$($Session.dependencyKey)"
    $env:ELITESCADA_PREVIEW_PORT = [string]$Session.webPort
}

function Get-ExpectedVolumeLabels($Dependency, [string]$Role) {
    return @{
        'com.elitescada.preview.cache-key' = $Dependency.Key
        'com.elitescada.preview.dependency-inputs-sha' = $Dependency.InputsSha
        'com.elitescada.preview.harness-sha' = $Dependency.HarnessSha
        'com.elitescada.preview.product-base-sha' = $Dependency.ProductBase
        'com.elitescada.preview.role' = $Role
    }
}

function Read-DockerVolume([string]$Name) {
    $volumeNames = @(Invoke-NativeCommand -FilePath 'docker' -Arguments @('volume', 'ls', '--quiet'))
    if ($LASTEXITCODE -ne 0) { throw 'Could not list Docker volumes while checking dependency provenance.' }
    if ($volumeNames -notcontains $Name) { return $null }

    $output = Invoke-NativeCommand -FilePath 'docker' -Arguments @('volume', 'inspect', $Name)
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect existing dependency volume '$Name'." }
    try {
        $json = ConvertFrom-Json -InputObject ($output -join [Environment]::NewLine)
        return @($json)[0]
    } catch {
        throw "Could not read provenance for dependency volume '$Name'."
    }
}

function Assert-DockerVolumeProvenance($Volume, [string]$Name, $Dependency, [string]$Role) {
    if ($null -eq $Volume) { throw "ENVIRONMENT_PREP_REQUIRED: dependency volume '$Name' is missing; run local-audit.ps1 prepare." }
    $expectedLabels = Get-ExpectedVolumeLabels $Dependency $Role
    foreach ($labelName in $expectedLabels.Keys) {
        $property = $Volume.Labels.PSObject.Properties[$labelName]
        $actualValue = if ($null -eq $property) { '' } else { [string]$property.Value }
        if ($actualValue -ne $expectedLabels[$labelName]) {
            throw "ENVIRONMENT_PREP_PROVENANCE_MISMATCH: dependency volume '$Name' has an unexpected '$labelName' label."
        }
    }
}

function Ensure-DockerVolume([string]$Name, $Dependency, [string]$Role) {
    $existing = Read-DockerVolume $Name
    if ($null -ne $existing) {
        Assert-DockerVolumeProvenance $existing $Name $Dependency $Role
        return
    }

    $arguments = @('volume', 'create')
    foreach ($label in (Get-ExpectedVolumeLabels $Dependency $Role).GetEnumerator()) {
        $arguments += @('--label', "$($label.Key)=$($label.Value)")
    }
    $arguments += $Name
    Invoke-NativeCommand -FilePath 'docker' -Arguments $arguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not create dedicated dependency volume '$Name'." }
    Assert-DockerVolumeProvenance (Read-DockerVolume $Name) $Name $Dependency $Role
}

function Get-PreviewImageName($Dependency) { return "$projectName-preview:$($Dependency.Key)" }

function Get-PreviewImageId($Dependency) {
    $imageName = Get-PreviewImageName $Dependency
    $imageIds = @(Invoke-NativeCommand -FilePath 'docker' -Arguments @('image', 'ls', '--quiet', '--no-trunc', '--filter', "reference=$imageName"))
    if ($LASTEXITCODE -ne 0) { throw "Could not list Docker images while checking '$imageName'." }
    if ($imageIds.Count -eq 0) { return $null }

    $imageId = Invoke-NativeCommand -FilePath 'docker' -Arguments @('image', 'inspect', '--format', '{{.Id}}', $imageName)
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect existing preview image '$imageName'." }
    return ($imageId | Select-Object -First 1).Trim()
}

function Assert-PreviewImageProvenance($Dependency) {
    $imageName = Get-PreviewImageName $Dependency
    $imageId = Get-PreviewImageId $Dependency
    if ([string]::IsNullOrWhiteSpace($imageId)) {
        throw 'ENVIRONMENT_PREP_REQUIRED: the pinned preview tool image is missing; run local-audit.ps1 prepare.'
    }
    $labelsJson = Invoke-NativeCommand -FilePath 'docker' -Arguments @('image', 'inspect', '--format', '{{json .Config.Labels}}', $imageName)
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect provenance labels for image '$imageName'." }
    $labels = ConvertFrom-Json -InputObject ($labelsJson -join [Environment]::NewLine)
    $expectedLabels = @{
        'com.elitescada.preview.dependency-key' = $Dependency.Key
        'com.elitescada.preview.dependency-inputs-sha' = $Dependency.InputsSha
        'com.elitescada.preview.harness-sha' = $Dependency.HarnessSha
        'com.elitescada.preview.product-base-sha' = $Dependency.ProductBase
    }
    foreach ($labelName in $expectedLabels.Keys) {
        $property = $labels.PSObject.Properties[$labelName]
        $actualValue = if ($null -eq $property) { '' } else { [string]$property.Value }
        if ($actualValue -ne $expectedLabels[$labelName]) {
            throw "ENVIRONMENT_PREP_PROVENANCE_MISMATCH: preview image '$imageName' has an unexpected '$labelName' label."
        }
    }
    return $imageId
}

function Test-PreparedDependencyContents($Dependency, [string]$ImageId) {
    $assetsRoot = Join-Path $repoRoot 'src\Scada.Api\obj'
    $assetsPath = Join-Path $assetsRoot 'project.assets.json'
    $assetsMarker = Join-Path $assetsRoot '.preview-dependency-key'
    if (-not (Test-Path -LiteralPath $assetsPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $assetsMarker -PathType Leaf) -or
        (Get-Content -Raw -LiteralPath $assetsMarker).Trim() -ne $Dependency.Key) {
        return $false
    }

    $verifyCommand = @'
set -Eeuo pipefail
[[ -x /node_modules/.bin/vite ]]
[[ -s /node_modules/.preview-dependency-key ]]
[[ "$(< /node_modules/.preview-dependency-key)" == "$PREVIEW_DEPENDENCY_KEY" ]]
[[ -s /nuget/.preview-dependency-key ]]
[[ "$(< /nuget/.preview-dependency-key)" == "$PREVIEW_DEPENDENCY_KEY" ]]
'@
    $arguments = @(
        'run', '--rm', '--network', 'none',
        '--mount', "type=volume,source=$($Dependency.NodeModulesVolume),target=/node_modules,readonly",
        '--mount', "type=volume,source=$($Dependency.NuGetVolume),target=/nuget,readonly",
        '--env', "PREVIEW_DEPENDENCY_KEY=$($Dependency.Key)",
        $ImageId, 'bash', '-lc', (ConvertTo-BashCommandArgument $verifyCommand)
    )
    Invoke-NativeCommand -FilePath 'docker' -Arguments $arguments *> $null
    return ($LASTEXITCODE -eq 0)
}

function Assert-PreparedDependencies {
    param(
        $Identity,
        $Dependency,
        [switch]$VerifyContents
    )
    if ($null -eq $Dependency) { $Dependency = Get-PreviewDependencyIdentity $Identity }
    if (-not (Test-Path -LiteralPath $Dependency.ManifestPath -PathType Leaf)) {
        throw "ENVIRONMENT_PREP_REQUIRED: no provenance manifest exists for this harness/dependency set; run local-audit.ps1 prepare."
    }
    try {
        $manifest = Get-Content -Raw -LiteralPath $Dependency.ManifestPath | ConvertFrom-Json
    } catch {
        throw 'ENVIRONMENT_PREP_PROVENANCE_MISMATCH: the dependency preparation manifest is unreadable.'
    }
    foreach ($field in @('schemaVersion', 'key', 'inputsSha', 'harnessSha', 'harnessTree', 'productBase', 'imageName', 'nodeModulesVolume', 'nugetVolume')) {
        $expected = switch ($field) {
            'schemaVersion' { '1' }
            'key' { $Dependency.Key }
            'inputsSha' { $Dependency.InputsSha }
            'harnessSha' { $Dependency.HarnessSha }
            'harnessTree' { $Dependency.HarnessTree }
            'productBase' { $Dependency.ProductBase }
            'imageName' { Get-PreviewImageName $Dependency }
            'nodeModulesVolume' { $Dependency.NodeModulesVolume }
            'nugetVolume' { $Dependency.NuGetVolume }
        }
        if ([string]$manifest.$field -ne [string]$expected) {
            throw "ENVIRONMENT_PREP_PROVENANCE_MISMATCH: prepared manifest field '$field' does not match the current exact harness/dependency set."
        }
    }

    $imageId = Assert-PreviewImageProvenance $Dependency
    if ([string]$manifest.imageId -ne $imageId) {
        throw 'ENVIRONMENT_PREP_PROVENANCE_MISMATCH: the prepared tool image changed after dependency preparation.'
    }
    Assert-DockerVolumeProvenance (Read-DockerVolume $Dependency.NodeModulesVolume) $Dependency.NodeModulesVolume $Dependency 'node_modules'
    Assert-DockerVolumeProvenance (Read-DockerVolume $Dependency.NuGetVolume) $Dependency.NuGetVolume $Dependency 'nuget'
    if ($VerifyContents -and -not (Test-PreparedDependencyContents $Dependency $imageId)) {
        throw 'ENVIRONMENT_PREP_REQUIRED: prepared dependency markers or offline assets are absent/mismatched; run local-audit.ps1 prepare.'
    }
    return $Dependency
}

function Write-PreparedDependencyManifest($Dependency, [string]$ImageId, $TrustedRoot) {
    New-Item -ItemType Directory -Path $dependencyManifestRoot -Force | Out-Null
    $temporaryPath = "$($Dependency.ManifestPath).tmp"
    $manifest = [pscustomobject]@{
        schemaVersion = 1
        key = $Dependency.Key
        inputsSha = $Dependency.InputsSha
        harnessSha = $Dependency.HarnessSha
        harnessTree = $Dependency.HarnessTree
        productBase = $Dependency.ProductBase
        imageName = Get-PreviewImageName $Dependency
        imageId = $ImageId
        nodeModulesVolume = $Dependency.NodeModulesVolume
        nugetVolume = $Dependency.NuGetVolume
        explicitTrustedRootThumbprint = if ($null -eq $TrustedRoot) { $null } else { $TrustedRoot.Thumbprint }
        explicitTrustedRootDerSha256 = if ($null -eq $TrustedRoot) { $null } else { $TrustedRoot.DerSha256 }
        preparedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $temporaryPath -Encoding utf8
    Move-Item -LiteralPath $temporaryPath -Destination $Dependency.ManifestPath -Force
}

function Invoke-PreparedDependencyBootstrap($Identity, $Dependency, [string]$RootThumbprint) {
    if (Test-Path -LiteralPath $Dependency.ManifestPath -PathType Leaf) {
        try {
            $null = Assert-PreparedDependencies $Identity $Dependency -VerifyContents
            Write-Output 'PREPARATION=READY_REUSED'
            Write-Output "DEPENDENCY_KEY=$($Dependency.Key)"
            Write-Output "TOOL_IMAGE=$((Get-PreviewImageId $Dependency))"
            return
        } catch {
            Write-Output "PREPARATION_CACHE_REVALIDATION=$($_.Exception.Message)"
        }
    }

    if (Test-Path -LiteralPath $Dependency.ManifestPath -PathType Leaf) {
        Remove-Item -LiteralPath $Dependency.ManifestPath -Force
    }

    Ensure-DockerVolume $Dependency.NodeModulesVolume $Dependency 'node_modules'
    Ensure-DockerVolume $Dependency.NuGetVolume $Dependency 'nuget'

    $imageId = Get-PreviewImageId $Dependency
    if ([string]::IsNullOrWhiteSpace($imageId)) {
        $composeArgs = (Get-ComposeArguments) + @('build', 'preview')
        $buildOutput = Invoke-NativeCommand -FilePath 'docker' -Arguments (@('compose') + $composeArgs)
        $buildExitCode = $LASTEXITCODE
        $buildText = ($buildOutput | Out-String)
        $buildOutput | ForEach-Object { Write-Output $_ }
        if ($buildExitCode -ne 0) {
            if ($buildText -match '(?i)UNABLE_TO_VERIFY_LEAF_SIGNATURE|CERTIFICATE_VERIFY_FAILED|CERT_HAS_EXPIRED|unable to verify|unable to get local issuer certificate|certificate.*(expired|invalid|verify|trust|chain)|SSL.*certificate|self-signed|remote certificate|x509:|tls.*(verify|certificate)') {
                throw "ENVIRONMENT_PREP_BLOCKED_TLS: preview tool image preparation could not validate the required TLS certificate chain. $($buildText.Trim())"
            }
            throw "Preview tool image preparation failed with exit code $buildExitCode."
        }
    }

    $imageId = Assert-PreviewImageProvenance $Dependency

    $prepareCommand = @'
set -Eeuo pipefail
rm -f \
  web/scada-web/node_modules/.preview-dependency-key \
  /root/.nuget/packages/.preview-dependency-key \
  src/Scada.Api/obj/.preview-dependency-key
npm --prefix web/scada-web ci --no-audit --no-fund
dotnet restore src/Scada.Api/Scada.Api.csproj
printf '%s\n' "$PREVIEW_DEPENDENCY_KEY" > web/scada-web/node_modules/.preview-dependency-key
printf '%s\n' "$PREVIEW_DEPENDENCY_KEY" > /root/.nuget/packages/.preview-dependency-key
mkdir -p src/Scada.Api/obj
printf '%s\n' "$PREVIEW_DEPENDENCY_KEY" > src/Scada.Api/obj/.preview-dependency-key
'@
    $trustedRoot = $null
    try {
        $runArguments = @(
            'run', '--rm', '--network', 'bridge', '--workdir', '/workspace',
            '--mount', "type=bind,source=$repoRoot,target=/workspace",
            '--mount', "type=volume,source=$($Dependency.NodeModulesVolume),target=/workspace/web/scada-web/node_modules",
            '--mount', "type=volume,source=$($Dependency.NuGetVolume),target=/root/.nuget/packages",
            '--env', "PREVIEW_DEPENDENCY_KEY=$($Dependency.Key)"
        )
        if (-not [string]::IsNullOrWhiteSpace($RootThumbprint)) {
            $trustedRoot = New-ExplicitTrustedRootPem $RootThumbprint
            $runArguments += @(
                '--mount', "type=bind,source=$($trustedRoot.Path),target=/tmp/explicit-trusted-root.pem,readonly",
                '--env', 'NODE_EXTRA_CA_CERTS=/tmp/explicit-trusted-root.pem',
                '--env', 'SSL_CERT_FILE=/tmp/explicit-trusted-root.pem'
            )
        }
        $runArguments += @($imageId, 'bash', '-lc', (ConvertTo-BashCommandArgument $prepareCommand))
        $prepareOutput = Invoke-NativeCommand -FilePath 'docker' -Arguments $runArguments
    } finally {
        if ($null -ne $trustedRoot -and (Test-Path -LiteralPath $trustedRoot.Path)) {
            Remove-Item -LiteralPath $trustedRoot.Path -Force
        }
    }
    $prepareExitCode = $LASTEXITCODE
    $prepareText = ($prepareOutput | Out-String)
    $prepareOutput | ForEach-Object { Write-Output $_ }
    if ($prepareExitCode -ne 0) {
        if ($prepareText -match '(?i)UNABLE_TO_VERIFY_LEAF_SIGNATURE|CERTIFICATE_VERIFY_FAILED|CERT_HAS_EXPIRED|unable to verify|unable to get local issuer certificate|certificate.*(expired|invalid|verify|trust|chain)|SSL.*certificate|self-signed|remote certificate|x509:|tls.*(verify|certificate)') {
            throw "ENVIRONMENT_PREP_BLOCKED_TLS: dependency preparation could not validate the required TLS certificate chain. $($prepareText.Trim())"
        }
        throw "Dependency preparation failed with exit code $prepareExitCode."
    }

    Write-PreparedDependencyManifest $Dependency $imageId $trustedRoot
    $null = Assert-PreparedDependencies $Identity $Dependency -VerifyContents
    Write-Output 'PREPARATION=READY'
    Write-Output "DEPENDENCY_KEY=$($Dependency.Key)"
    Write-Output "TOOL_IMAGE=$imageId"
}

function Remove-PreviewProjectResources {
    $resources = Get-StackResources
    foreach ($containerId in $resources.Containers) {
        $running = Invoke-NativeCommand -FilePath 'docker' -Arguments @('container', 'inspect', '--format', '{{.State.Running}}', $containerId)
        if ($LASTEXITCODE -ne 0) { throw "Could not inspect dedicated preview container '$containerId'." }
        if (($running | Select-Object -First 1).Trim() -eq 'true') {
            Invoke-NativeCommand -FilePath 'docker' -Arguments @('container', 'stop', '--time', '30', $containerId) | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Could not stop dedicated preview container '$containerId'." }
        }
        Invoke-NativeCommand -FilePath 'docker' -Arguments @('container', 'rm', $containerId) | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not remove dedicated preview container '$containerId'." }
    }
    foreach ($networkId in $resources.Networks) {
        Invoke-NativeCommand -FilePath 'docker' -Arguments @('network', 'rm', $networkId) | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not remove dedicated preview network '$networkId'." }
    }
    foreach ($volumeName in $resources.Volumes) {
        if ($volumeName -like 'elitescada-preview-deps-node-*' -or
            $volumeName -like 'elitescada-preview-deps-nuget-*') {
            Write-Output "PRESERVED_DEPENDENCY_VOLUME=$volumeName"
            continue
        }
        Invoke-NativeCommand -FilePath 'docker' -Arguments @('volume', 'rm', $volumeName) | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not remove dedicated preview volume '$volumeName'." }
    }
}

function Read-Session {
    if (-not (Test-Path -LiteralPath $sessionFile)) { return $null }
    return (Get-Content -Raw -LiteralPath $sessionFile | ConvertFrom-Json)
}

function Save-Session($Session) {
    New-Item -ItemType Directory -Path $sessionRoot -Force | Out-Null
    $Session | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $sessionFile -Encoding utf8
}

function Set-OptionalNoteProperty($Object, [string]$Name, $Value) {
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        $Object | Add-Member -MemberType NoteProperty -Name $Name -Value $Value
        return
    }

    $property.Value = $Value
}

function Add-Transition($Session, [string]$State, [string]$Message, [string]$CheckpointNote) {
    $Session.state = $State
    $Session.updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    if ($Message) { Set-OptionalNoteProperty $Session 'lastTransitionMessage' $Message }
    if ($CheckpointNote) { Set-OptionalNoteProperty $Session 'lastCheckpoint' $CheckpointNote }
    if ($null -eq $Session.transitions) { $Session | Add-Member -NotePropertyName transitions -NotePropertyValue @() }
    $Session.transitions += [pscustomobject]@{
        state = $State
        atUtc = $Session.updatedAtUtc
    }
    Save-Session $Session
}

function Assert-ResumableSession($Session, $Identity, $Dependency) {
    $authorizationMatches = if ($Session.authorizationMode -eq 'LOCAL_OWNER_DEVELOPMENT') {
        $Session.localDevelopmentSha -eq $Session.harnessSha
    } else {
        $Session.acceptedHarnessSha -eq $Session.harnessSha
    }
    if (-not $authorizationMatches -or
        $Session.productBaseSha -ne $Identity.ProductBase -or
        $Session.harnessSha -ne $Identity.Head -or
        $Session.harnessTree -ne $Identity.Tree -or
        $Session.dependencyKey -ne $Dependency.Key -or
        $Session.nodeModulesVolume -ne $Dependency.NodeModulesVolume -or
        $Session.nugetVolume -ne $Dependency.NuGetVolume) {
        throw 'RESUME_BLOCKED_VERSION_MISMATCH: the current product/harness identity differs from the saved session. Nothing was reset or migrated.'
    }
}

function Wait-PreviewHealth([int]$WebPort) {
    $webUri = "http://localhost:$WebPort/"
    $apiUri = "http://localhost:$WebPort/health"
    for ($attempt = 0; $attempt -lt 24; $attempt++) {
        $web = Test-HttpEndpoint $webUri
        $api = Test-HttpEndpoint $apiUri
        if ($web.state -eq 'healthy' -and $api.state -eq 'healthy') {
            return
        }
        Start-Sleep -Seconds 5
    }
    throw "The preview did not become healthy. Web=$($web.state) at $webUri; API=$($api.state) at $apiUri. Run 'elite-local.ps1 diagnose' for a sanitized report."
}

function Test-InternalApiHealth($PreviewContainer) {
    if ($null -eq $PreviewContainer -or $PreviewContainer.state -ne 'running') {
        return [pscustomobject]@{ state = 'not-running'; statusCode = $null }
    }
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $output = @(Invoke-NativeCommand -FilePath 'docker' -Arguments @(
        'exec', $PreviewContainer.id, 'node', '-e', "fetch('http://127.0.0.1:5080/health').then(r => process.exit(r.ok ? 0 : 1)).catch(() => process.exit(1))"
    ))
    $stopwatch.Stop()
    return [pscustomobject]@{ state = if ($LASTEXITCODE -eq 0) { 'healthy' } else { 'unhealthy' }; statusCode = $null; durationMs = $stopwatch.ElapsedMilliseconds }
}

function Get-SessionStatusSnapshot($Session, $Identity) {
    $dependency = $null
    $preparationState = 'UNKNOWN'
    $preparationDetail = ''
    try {
        $dependency = Get-PreviewDependencyIdentity $Identity
        Set-PreviewDependencyEnvironment $dependency
        $null = Assert-PreparedDependencies $Identity $dependency -VerifyContents
        $preparationState = 'READY'
    } catch {
        $preparationState = 'REQUIRED_OR_UNAVAILABLE'
        $preparationDetail = Protect-DiagnosticText (($_.Exception.Message -replace '\s+', ' ').Trim())
    }

    $containerSnapshot = Get-StackContainers
    $containers = @($containerSnapshot.Containers)
    $timescale = $containers | Where-Object { $_.service -eq 'timescaledb' } | Select-Object -First 1
    $preview = $containers | Where-Object { $_.service -eq 'preview' } | Select-Object -First 1
    $port = if ($null -ne $Session) { [int]$Session.webPort } else { $Port }
    $webUrl = "http://localhost:$port/"
    $apiUrl = "http://localhost:$port/health"
    $webHealth = if ($null -ne $preview -and $preview.state -eq 'running') { Test-HttpEndpoint $webUrl } else { [pscustomobject]@{ state = 'not-running'; statusCode = $null } }
    $apiHealth = Test-InternalApiHealth $preview
    $databaseHealth = if ($null -eq $timescale -or $timescale.state -ne 'running') { 'not-running' } elseif ($timescale.health -eq 'none') { 'running' } else { $timescale.health }
    $dockerEngine = if ($containerSnapshot.Available) { 'available' } else { 'unavailable' }

    if ($null -eq $Session) {
        $hasResources = $containers.Count -gt 0
        if (-not $hasResources -and $containerSnapshot.Available) {
            try {
                $resources = Get-StackResources
                $hasResources = $resources.Volumes.Count -gt 0 -or $resources.Networks.Count -gt 0
            } catch { $hasResources = $false }
        }
        $state = if ($hasResources) { 'ORPHANED_PREVIEW_STATE_REVIEW_REQUIRED' } else { 'NOT_STARTED' }
        $differs = $null
        $resumable = $false
        $sessionId = $null
        $sessionHarnessSha = $null
        $sessionHarnessTree = $null
        $workbenchBranch = $null
        $lastCheckpoint = ''
    } else {
        $differs = $Identity.Head -ne $Session.harnessSha -or
            $Identity.Tree -ne $Session.harnessTree -or
            $Identity.Branch -ne $Session.branch -or
            $Identity.DirtyPaths.Count -gt 0
        $identityMatches = $Identity.Head -eq $Session.harnessSha -and $Identity.Tree -eq $Session.harnessTree
        $authorizationMatches = if ($Session.authorizationMode -eq 'LOCAL_OWNER_DEVELOPMENT') {
            $Session.localDevelopmentSha -eq $Session.harnessSha
        } else {
            $Session.acceptedHarnessSha -eq $Session.harnessSha
        }
        $resumable = $identityMatches -and $authorizationMatches -and $Session.productBaseSha -eq $Identity.ProductBase
        if (-not $identityMatches) {
            $state = 'RESUME_BLOCKED_CHECKOUT_DIFFERS'
        } elseif (-not $authorizationMatches) {
            $state = 'RESUME_BLOCKED_AUTHORIZATION_MISMATCH'
        } elseif (-not $containerSnapshot.Available) {
            $state = 'DOCKER_UNAVAILABLE_RESUMABLE'
        } elseif ($preview -and $preview.state -eq 'running' -and $webHealth.state -eq 'healthy' -and $apiHealth.state -eq 'healthy' -and $databaseHealth -eq 'healthy') {
            $state = 'RUNNING_HEALTHY'
        } elseif ($preview -and $preview.state -eq 'running') {
            $state = 'RUNNING_UNHEALTHY'
        } elseif ($resumable) {
            $state = 'PAUSED_RESUMABLE'
        } else {
            $state = [string]$Session.state
        }
        $sessionId = $Session.sessionId
        $sessionHarnessSha = $Session.harnessSha
        $sessionHarnessTree = $Session.harnessTree
        $workbenchBranch = $Session.branch
        $lastCheckpoint = Protect-DiagnosticText (($Session.lastCheckpoint -replace '\s+', ' ').Trim())
    }

    return [pscustomobject]@{
        state = $state
        composeProject = $projectName
        profile = $Profile
        authorizationMode = if ($null -ne $Session) { $Session.authorizationMode } else { $null }
        currentBranch = $Identity.Branch
        currentHead = $Identity.Head
        currentTree = $Identity.Tree
        currentCheckoutDirty = $Identity.DirtyPaths.Count -gt 0
        currentDirtyPaths = @($Identity.DirtyPaths)
        currentProductScopeChanges = @($Identity.ProductScopeChanges)
        workbenchBranch = $workbenchBranch
        workbenchHarnessSha = $sessionHarnessSha
        workbenchHarnessTree = $sessionHarnessTree
        workbenchCheckoutDiffers = $differs
        sessionId = $sessionId
        resumable = $resumable
        lastCheckpoint = $lastCheckpoint
        dependencyPreparation = $preparationState
        dependencyKey = if ($null -ne $dependency) { $dependency.Key } elseif ($null -ne $Session) { $Session.dependencyKey } else { $null }
        dependencyDetail = $preparationDetail
        dockerEngine = $dockerEngine
        timescaleDbHealth = $databaseHealth
        apiHealth = $apiHealth.state
        apiHealthDurationMs = $apiHealth.durationMs
        webHealth = $webHealth.state
        webHealthStatusCode = $webHealth.statusCode
        webHealthDurationMs = $webHealth.durationMs
        webUrl = $webUrl
        apiUrl = $apiUrl
        apiInternalUrl = 'http://127.0.0.1:5080/health inside preview container; not published to host'
        containers = @($containers | Select-Object name, service, state, status, health)
        evidenceDirectory = $sessionRoot
        observedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
}

function Print-SessionStatus($Session, $Identity) {
    $snapshot = Get-SessionStatusSnapshot $Session $Identity
    Write-Output "STATE=$($snapshot.state)"
    Write-Output "PROFILE=$($snapshot.profile)"
    if ($snapshot.authorizationMode) { Write-Output "AUTHORIZATION_MODE=$($snapshot.authorizationMode)" }
    Write-Output "BRANCH=$($snapshot.currentBranch)"
    Write-Output "HEAD=$($snapshot.currentHead)"
    Write-Output "TREE=$($snapshot.currentTree)"
    Write-Output "DIRTY=$($snapshot.currentCheckoutDirty.ToString().ToLowerInvariant())"
    Write-Output "SESSION_ID=$($snapshot.sessionId)"
    Write-Output "WORKBENCH_HEAD=$($snapshot.workbenchHarnessSha)"
    Write-Output "WORKBENCH_TREE=$($snapshot.workbenchHarnessTree)"
    $checkoutDiffers = if ($null -eq $snapshot.workbenchCheckoutDiffers) { 'unknown' } else { $snapshot.workbenchCheckoutDiffers.ToString().ToLowerInvariant() }
    Write-Output "CHECKOUT_DIFFERS_FROM_WORKBENCH=$checkoutDiffers"
    Write-Output "RESUMABLE=$($snapshot.resumable.ToString().ToLowerInvariant())"
    Write-Output "PREPARATION=$($snapshot.dependencyPreparation)"
    Write-Output "DEPENDENCY_KEY=$($snapshot.dependencyKey)"
    if ($snapshot.dependencyDetail) { Write-Output "PREPARATION_DETAIL=$($snapshot.dependencyDetail)" }
    Write-Output "DOCKER_ENGINE=$($snapshot.dockerEngine)"
    Write-Output "TIMESCALEDB_HEALTH=$($snapshot.timescaleDbHealth)"
    Write-Output "API_HEALTH=$($snapshot.apiHealth)"
    Write-Output "WEB_HEALTH=$($snapshot.webHealth)"
    Write-Output "WEB_URL=$($snapshot.webUrl)"
    Write-Output "API_URL=$($snapshot.apiUrl)"
    Write-Output "CONTAINERS_JSON=$(ConvertTo-Json -InputObject @($snapshot.containers) -Compress -Depth 4)"
    Write-Output "STATUS_JSON=$(ConvertTo-Json -InputObject $snapshot -Compress -Depth 6)"
}

function Protect-DiagnosticText([string]$Text) {
    $safe = $Text -replace '(?i)\bBearer\s+[A-Za-z0-9._~+/-]+=*', 'Bearer [REDACTED]'
    $safe = $safe -replace '(?im)(password|passwd|pwd|token|secret|signing[_-]?key|authorization|api[_-]?key)(\s*[:=]\s*["'']?)([^\s,"'';&]+)', '$1$2[REDACTED]'
    $safe = $safe -replace '\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b', '[REDACTED_JWT]'
    return ($safe -replace '(?i)([?&](?:access_token|token|code|key|secret|password)=)[^&\s]+', '$1[REDACTED]')
}

function Write-LocalDiagnosticReport($Session, $Identity) {
    $snapshot = Get-SessionStatusSnapshot $Session $Identity
    $reportLines = @(
        '# EliteSCADA local diagnostic snapshot',
        "Generated UTC: $($snapshot.observedAtUtc)",
        '',
        '## Identity and health',
        (ConvertTo-Json -InputObject $snapshot -Depth 8)
    )
    $containerSnapshot = Get-StackContainers
    if ($containerSnapshot.Available -and $containerSnapshot.Containers.Count -gt 0) {
        $reportLines += @('', '## Recent service logs (last 30 minutes; redacted)')
        foreach ($container in $containerSnapshot.Containers) {
            $reportLines += @('', "### $($container.service): $($container.name)")
            $logLines = @(Invoke-NativeCommand -FilePath 'docker' -Arguments @('logs', '--since', '30m', '--tail', '120', $container.id))
            if ($LASTEXITCODE -ne 0) {
                $reportLines += 'Log collection unavailable.'
            } else {
                $reportLines += Protect-DiagnosticText ($logLines -join [Environment]::NewLine)
            }
        }
    } else {
        $reportLines += @('', '## Recent service logs', 'No preview containers are available.')
    }

    $diagnosticDirectory = Join-Path $sessionRoot 'diagnostics'
    $resolvedDirectory = [IO.Path]::GetFullPath($diagnosticDirectory)
    if (-not $resolvedDirectory.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to write diagnostics outside the repository-local evidence directory.'
    }
    New-Item -ItemType Directory -Path $resolvedDirectory -Force | Out-Null
    $fileName = 'local-diagnostic-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff') + 'Z.md'
    $reportPath = Join-Path $resolvedDirectory $fileName
    [IO.File]::WriteAllText($reportPath, ($reportLines -join [Environment]::NewLine) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    Write-Output "DIAGNOSTIC_REPORT=$reportPath"
    Write-Output "DIAGNOSTIC_SECRETS_REDACTED=true"
    Print-SessionStatus $Session $Identity
}

if ($Command -notin @('prepare', 'launch') -and -not [string]::IsNullOrWhiteSpace($TrustedRootThumbprint)) {
    throw '-TrustedRootThumbprint is permitted only for prepare or launch automatic preparation.'
}
if ($Command -eq 'reset' -and -not $Force) {
    throw 'RESET_CONFIRMATION_REQUIRED: reset deletes the local product database/session. Re-run only after the Product Owner explicitly requests a fresh install, with -Force.'
}

switch ($Command) {
    'launch' {
        if ($Profile -ne 'development') { throw 'Local launch always uses the isolated development profile.' }
        $identity = Get-HarnessIdentity
        Assert-DevelopmentDependencyInputsClean $identity
        $session = Read-Session
        $snapshot = Get-SessionStatusSnapshot $session $identity
        if ($snapshot.dockerEngine -ne 'available') { throw 'DOCKER_UNAVAILABLE: start Docker Desktop, then retry launch. No workbench state was changed.' }

        if ($null -ne $session) {
            if ($snapshot.state -eq 'RUNNING_HEALTHY') {
                Print-SessionStatus $session $identity
                Write-Output "WEB_URL=http://localhost:$($session.webPort)"
                return
            }
            if ($snapshot.state -eq 'RUNNING_UNHEALTHY') {
                throw 'LOCAL_WORKBENCH_UNHEALTHY: run status and diagnose; launch will not blindly recreate a running but unhealthy workbench.'
            }
            if (-not $snapshot.resumable) {
                throw "LOCAL_WORKBENCH_NOT_RESUMABLE: status=$($snapshot.state). Run status and diagnose; launch will not reset, migrate, or overwrite this workbench."
            }
            Assert-OtherProfileStopped
            & $PSCommandPath resume -Profile development -Port $Port
            if (-not $?) { throw 'Could not resume the saved local development workbench.' }
            return
        }

        if ($snapshot.state -ne 'NOT_STARTED') {
            throw "LOCAL_WORKBENCH_REVIEW_REQUIRED: status=$($snapshot.state). Run status and diagnose; launch will not remove orphaned containers, networks, volumes, or evidence."
        }
        Assert-OtherProfileStopped
        $identity = Get-HarnessIdentity
        $dependency = Get-PreviewDependencyIdentity $identity
        Set-PreviewDependencyEnvironment $dependency
        $prepared = $false
        try {
            $null = Assert-PreparedDependencies $identity $dependency -VerifyContents
            $prepared = $true
        } catch {
            $prepared = $false
        }
        if (-not $prepared) {
            Invoke-PreparedDependencyBootstrap $identity $dependency $TrustedRootThumbprint
            $null = Assert-PreparedDependencies $identity $dependency -VerifyContents
        }
        & $PSCommandPath start -Profile development -LocalOwnerLaunch -Port $Port
        if (-not $?) { throw 'Local launch did not complete successfully.' }
        return
    }
    'status' {
        $identity = Get-HarnessIdentity -AllowUnclean
        Print-SessionStatus (Read-Session) $identity
    }
    'prepare' {
        $identity = Get-HarnessIdentity
        Assert-DevelopmentDependencyInputsClean $identity
        $dependency = Get-PreviewDependencyIdentity $identity
        Set-PreviewDependencyEnvironment $dependency
        if ($null -ne (Read-Session)) {
            throw "Preparation is allowed only with no saved $Profile session; stop/reset that profile first."
        }
        $resources = Get-StackResources
        if ($resources.Containers.Count -or $resources.Volumes.Count -or $resources.Networks.Count) {
            throw 'Preparation is allowed only when the dedicated product/audit stack is absent; inspect or explicitly reset it first.'
        }
        Invoke-PreparedDependencyBootstrap $identity $dependency $TrustedRootThumbprint
    }
    'start' {
        $identity = Get-HarnessIdentity
        Assert-DevelopmentDependencyInputsClean $identity
        $existing = Read-Session
        if ($null -ne $existing) {
            if (-not [string]::IsNullOrWhiteSpace($AcceptedHarnessSha) -and $AcceptedHarnessSha.ToLowerInvariant() -ne $identity.Head) {
                throw "Accepted SHA does not match this exact harness HEAD ($($identity.Head)); refusing to resume the saved workbench."
            }
            $dependency = Get-PreviewDependencyIdentity $identity
            Assert-ResumableSession $existing $identity $dependency
            Set-SessionComposeEnvironment $existing $dependency
            $null = Assert-PreparedDependencies $identity $dependency -VerifyContents
            Assert-OtherProfileStopped
            Invoke-PreviewCompose -ComposeArguments @('up', '--no-build', '--pull', 'never', '--detach', '--wait', '--wait-timeout', '240')
            Wait-PreviewHealth ([int]$existing.webPort)
            Add-Transition $existing 'RUNNING' 'Existing workbench resumed by start; database and product state were preserved.'
            Print-SessionStatus $existing $identity
            Write-Output "WEB_URL=http://localhost:$($existing.webPort)"
            return
        }
        if ($Profile -eq 'development') {
            if (-not $LocalOwnerLaunch) { throw 'For the local development workbench, use `elite-local.ps1 launch`; only that command records a separate Product Owner local-development authorization.' }
            if (-not [string]::IsNullOrWhiteSpace($AcceptedHarnessSha)) { throw 'Do not pass Main audit acceptance SHA to the isolated local development profile.' }
        } else {
            if ($LocalOwnerLaunch) { throw 'Local development authorization cannot be used for the Main-gated ENV_A audit profile.' }
            if ([string]::IsNullOrWhiteSpace($AcceptedHarnessSha)) {
                throw "A new audit workbench requires -AcceptedHarnessSha <40-hex SHA> copied from Main's live ENV_A harness-acceptance order."
            }
            if ($AcceptedHarnessSha.ToLowerInvariant() -ne $identity.Head) {
                throw "Accepted SHA does not match this exact harness HEAD ($($identity.Head)); refusing to start."
            }
        }
        $resources = Get-StackResources
        if ($resources.Containers.Count -or $resources.Volumes.Count -or $resources.Networks.Count) {
            throw 'ORPHANED_PREVIEW_STATE: dedicated project containers/volumes exist without a manifest. Inspect them and use explicit reset; start will not reuse them.'
        }
        $dependency = Get-PreviewDependencyIdentity $identity
        Set-PreviewDependencyEnvironment $dependency
        $null = Assert-PreparedDependencies $identity $dependency -VerifyContents
        Assert-OtherProfileStopped

        New-Item -ItemType Directory -Path $sessionRoot -Force | Out-Null
        $authorizationMode = if ($Profile -eq 'development') { 'LOCAL_OWNER_DEVELOPMENT' } else { 'MAIN_ENV_A_ACCEPTANCE' }
        $session = [pscustomobject]@{
            schemaVersion = 1
            sessionId = [Guid]::NewGuid().ToString('D')
            state = 'STARTING'
            productBaseSha = $identity.ProductBase
            authorizationMode = $authorizationMode
            acceptedHarnessSha = if ($Profile -eq 'audit') { $identity.Head } else { $null }
            localDevelopmentSha = if ($Profile -eq 'development') { $identity.Head } else { $null }
            harnessSha = $identity.Head
            harnessTree = $identity.Tree
            branch = $identity.Branch
            dependencyKey = $dependency.Key
            dependencyInputsSha = $dependency.InputsSha
            nodeModulesVolume = $dependency.NodeModulesVolume
            nugetVolume = $dependency.NuGetVolume
            createdAtUtc = [DateTime]::UtcNow.ToString('o')
            updatedAtUtc = [DateTime]::UtcNow.ToString('o')
            webPort = $Port
            lastCheckpoint = if ($Profile -eq 'development') { 'New local development session; no project/user state was pre-seeded by the harness.' } else { 'New clean Environment A session; no project/user state was pre-seeded by the harness.' }
            transitions = @([pscustomobject]@{ state = 'STARTING'; atUtc = [DateTime]::UtcNow.ToString('o') })
        }
        Save-Session $session
        try {
            Invoke-PreviewCompose -ComposeArguments @('up', '--no-build', '--pull', 'never', '--detach', '--wait', '--wait-timeout', '240')
            Wait-PreviewHealth $Port
            $readyMessage = if ($Profile -eq 'development') { 'Clean local development stack healthy; this session is not Main-accepted ENV_A audit evidence.' } else { 'Clean application stack healthy; product remains available for the accepted audit.' }
            Add-Transition $session 'RUNNING' $readyMessage
            Print-SessionStatus $session $identity
            Write-Output "WEB_URL=http://localhost:$Port"
        } catch {
            Add-Transition $session 'START_FAILED_RESUMABLE' 'Startup failed; inspect logs, then use status/resume or explicit reset.'
            throw
        }
    }
    'pause' {
        $identity = Get-HarnessIdentity -AllowUnclean
        $session = Read-Session
        $count = Stop-PreviewContainers
        if ($null -ne $session -and $session.state -notin @('PAUSED_RESUMABLE', 'STOPPED_PRESERVED')) {
            Add-Transition $session 'PAUSED_RESUMABLE' 'Runtime and database containers stopped cleanly; database volume, user project and local evidence were preserved.' $Checkpoint
        }
        if ($null -eq $session) { Write-Output "STOPPED_CONTAINERS=$count"; Write-Output 'STATE=NO_SAVED_SESSION; no data was deleted.' }
        Print-SessionStatus $session $identity
    }
    'stop' {
        $identity = Get-HarnessIdentity -AllowUnclean
        $session = Read-Session
        $count = Stop-PreviewContainers
        if ($null -ne $session -and $session.state -notin @('PAUSED_RESUMABLE', 'STOPPED_PRESERVED')) {
            Add-Transition $session 'PAUSED_RESUMABLE' 'Stopped local services; database volume, user project, evidence and provenance were preserved.' $Checkpoint
        }
        Write-Output "STOPPED_CONTAINERS=$count"
        Write-Output 'PRODUCT_STATE_PRESERVED=true'
        Print-SessionStatus $session $identity
    }
    'resume' {
        $identity = Get-HarnessIdentity
        Assert-DevelopmentDependencyInputsClean $identity
        $dependency = Get-PreviewDependencyIdentity $identity
        $session = Read-Session
        if ($null -eq $session) { throw 'No saved session exists. Resume never creates a workbench; use the approved start/launch path for the selected profile.' }
        Assert-ResumableSession $session $identity $dependency
        Set-SessionComposeEnvironment $session $dependency
        $null = Assert-PreparedDependencies $identity $dependency -VerifyContents
        Assert-OtherProfileStopped
        Invoke-PreviewCompose -ComposeArguments @('up', '--no-build', '--pull', 'never', '--detach', '--wait', '--wait-timeout', '240')
        Wait-PreviewHealth ([int]$session.webPort)
        Add-Transition $session 'RESUMED' 'Same saved Environment A identity reopened; product/database state was not reset or reseeded.'
        Print-SessionStatus $session $identity
        Write-Output "WEB_URL=http://localhost:$($session.webPort)"
    }
    'restart' {
        $identity = Get-HarnessIdentity
        Assert-DevelopmentDependencyInputsClean $identity
        $dependency = Get-PreviewDependencyIdentity $identity
        $session = Read-Session
        if ($null -eq $session) { throw 'No saved workbench exists to restart. A new start requires Main acceptance.' }
        Assert-ResumableSession $session $identity $dependency
        Set-SessionComposeEnvironment $session $dependency
        $null = Assert-PreparedDependencies $identity $dependency -VerifyContents
        Assert-OtherProfileStopped
        Invoke-PreviewCompose -ComposeArguments @('up', '--no-build', '--pull', 'never', '--detach', '--force-recreate', '--wait', '--wait-timeout', '240')
        Wait-PreviewHealth ([int]$session.webPort)
        Add-Transition $session 'RUNNING' 'Application and database containers were recreated against the same named product volumes.'
        Write-Output 'PRODUCT_STATE_PRESERVED=true'
        Print-SessionStatus $session $identity
        Write-Output "WEB_URL=http://localhost:$($session.webPort)"
    }
    'diagnose' {
        $identity = Get-HarnessIdentity -AllowUnclean
        Write-LocalDiagnosticReport (Read-Session) $identity
    }
    'reset' {
        $session = Read-Session
        Remove-PreviewProjectResources
        if (Test-Path -LiteralPath $sessionRoot) {
            $resolvedSession = (Resolve-Path -LiteralPath $sessionRoot).Path
            if (-not $resolvedSession.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw 'Refusing to archive audit evidence outside this repository.'
            }
            New-Item -ItemType Directory -Path $archiveRoot -Force | Out-Null
            $archiveName = $sessionDirectoryName + '-' + [Guid]::NewGuid().ToString('N')
            $archivePath = Join-Path $archiveRoot $archiveName
            if (-not $archivePath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw 'Refusing to archive evidence outside this repository.'
            }
            Move-Item -LiteralPath $resolvedSession -Destination $archivePath
            Write-Output "ARCHIVED_EVIDENCE=$archivePath"
        }
        Write-Output "STATE=NOT_STARTED"
        Write-Output "RESET_SCOPE=only profile=$Profile Docker Compose project $projectName and its dedicated containers, network and product volumes; provenance-bound dependency volumes and tool image are preserved; evidence archived under ci/local/artifacts/$archiveDirectoryName"
    }
}
