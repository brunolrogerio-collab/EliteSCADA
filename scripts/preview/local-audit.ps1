[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('prepare', 'start', 'pause', 'resume', 'status', 'reset')]
    [string]$Command,

    [Parameter()]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$AcceptedHarnessSha,

    [Parameter()]
    [ValidateRange(1024, 65535)]
    [int]$Port = 5173,

    [Parameter()]
    [string]$Checkpoint
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$productBaseSha = '1f14a57491805a5d976bc9d0bf51393cf1b3ebcd'
$projectName = 'elitescada-preview-a'
$composeFiles = @(
    (Join-Path $repoRoot 'ci\local\docker-compose.preview.yml'),
    (Join-Path $repoRoot 'ci\local\docker-compose.preview.local.yml')
)
$artifactRoot = Join-Path $repoRoot 'ci\local\artifacts'
$sessionRoot = Join-Path $artifactRoot 'preview-audit-a'
$sessionFile = Join-Path $sessionRoot 'session.json'
$archiveRoot = Join-Path $artifactRoot 'preview-audit-archive'
$dependencyManifestRoot = Join-Path $artifactRoot 'preview-dependencies'
$repoPrefix = $repoRoot.TrimEnd('\') + '\'

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
    $allowedFiles = @('.gitattributes', 'docs/LOCAL-FIRST-PROJECT-PREVIEW-HARNESS.md')
    $changedPaths = @(Invoke-Git -GitArguments @('diff', '--name-only', $productBaseSha, 'HEAD'))
    foreach ($path in $changedPaths) {
        $normalized = $path.Replace('\', '/')
        $isAllowed = ($allowedFiles -contains $normalized) -or
            @($allowedRoots | Where-Object { $normalized.StartsWith($_, [System.StringComparison]::Ordinal) }).Count -gt 0
        if (-not $isAllowed) {
            throw "Product-scope change detected since the approved base: $normalized"
        }
    }

    $dirty = @(Invoke-Git -GitArguments @('status', '--porcelain', '--untracked-files=all'))
    if ($dirty.Count -gt 0) {
        throw 'The audit requires a clean, committed harness worktree; commit or safely preserve local changes before continuing.'
    }

    return [pscustomobject]@{
        Branch = $branch
        Head = $head
        Tree = $tree
        ProductBase = $productBaseSha
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

function Get-Sha256Text([string]$Text) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
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
        'scripts/preview/run-product-preview.sh'
    )
    $manifestPaths += @($tracked | Where-Object {
        $_ -match '(^|/)([^/]+\.csproj|packages\.lock\.json|NuGet\.Config|Directory\.Build\.(props|targets)|Directory\.Packages\.props)$'
    })
    $manifestPaths = @($manifestPaths | Sort-Object -Unique)

    $manifestHashes = @()
    foreach ($relativePath in $manifestPaths) {
        $filePath = Join-Path $repoRoot ($relativePath.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            throw "Dependency provenance input is missing: $relativePath"
        }
        $hash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $manifestHashes += "$relativePath=$hash"
    }

    $inputsSha = Get-Sha256Text ($manifestHashes -join "`n")
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

function Set-PreviewDependencyEnvironment($Dependency) {
    $env:ELITESCADA_PREVIEW_DEPENDENCY_KEY = $Dependency.Key
    $env:ELITESCADA_PREVIEW_DEPENDENCY_INPUTS_SHA = $Dependency.InputsSha
    $env:ELITESCADA_PREVIEW_HARNESS_SHA = $Dependency.HarnessSha
    $env:ELITESCADA_PREVIEW_PRODUCT_BASE_SHA = $Dependency.ProductBase
    $env:ELITESCADA_PREVIEW_NODE_MODULES_VOLUME = $Dependency.NodeModulesVolume
    $env:ELITESCADA_PREVIEW_NUGET_VOLUME = $Dependency.NuGetVolume
    $env:ELITESCADA_PREVIEW_PORT = "$Port"
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
        $ImageId, 'bash', '-lc', $verifyCommand
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

function Write-PreparedDependencyManifest($Dependency, [string]$ImageId) {
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
        preparedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $temporaryPath -Encoding utf8
    Move-Item -LiteralPath $temporaryPath -Destination $Dependency.ManifestPath -Force
}

function Invoke-PreparedDependencyBootstrap($Identity, $Dependency) {
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
    $runArguments = @(
        'run', '--rm', '--network', 'bridge', '--workdir', '/workspace',
        '--mount', "type=bind,source=$repoRoot,target=/workspace",
        '--mount', "type=volume,source=$($Dependency.NodeModulesVolume),target=/workspace/web/scada-web/node_modules",
        '--mount', "type=volume,source=$($Dependency.NuGetVolume),target=/root/.nuget/packages",
        '--env', "PREVIEW_DEPENDENCY_KEY=$($Dependency.Key)",
        $imageId, 'bash', '-lc', $prepareCommand
    )
    $prepareOutput = Invoke-NativeCommand -FilePath 'docker' -Arguments $runArguments
    $prepareExitCode = $LASTEXITCODE
    $prepareText = ($prepareOutput | Out-String)
    $prepareOutput | ForEach-Object { Write-Output $_ }
    if ($prepareExitCode -ne 0) {
        if ($prepareText -match '(?i)UNABLE_TO_VERIFY_LEAF_SIGNATURE|CERTIFICATE_VERIFY_FAILED|CERT_HAS_EXPIRED|unable to verify|unable to get local issuer certificate|certificate.*(expired|invalid|verify|trust|chain)|SSL.*certificate|self-signed|remote certificate|x509:|tls.*(verify|certificate)') {
            throw "ENVIRONMENT_PREP_BLOCKED_TLS: dependency preparation could not validate the required TLS certificate chain. $($prepareText.Trim())"
        }
        throw "Dependency preparation failed with exit code $prepareExitCode."
    }

    Write-PreparedDependencyManifest $Dependency $imageId
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
    if ($Session.productBaseSha -ne $Identity.ProductBase -or
        $Session.acceptedHarnessSha -ne $Session.harnessSha -or
        $Session.harnessSha -ne $Identity.Head -or
        $Session.harnessTree -ne $Identity.Tree -or
        $Session.dependencyKey -ne $Dependency.Key -or
        $Session.nodeModulesVolume -ne $Dependency.NodeModulesVolume -or
        $Session.nugetVolume -ne $Dependency.NuGetVolume) {
        throw 'RESUME_BLOCKED_VERSION_MISMATCH: the current product/harness identity differs from the saved session. Nothing was reset or migrated.'
    }
}

function Wait-PreviewHealth([int]$WebPort) {
    $uri = "http://localhost:$WebPort/health"
    for ($attempt = 0; $attempt -lt 24; $attempt++) {
        try {
            $null = Invoke-RestMethod -Uri $uri -TimeoutSec 3
            return
        } catch {
            Start-Sleep -Seconds 5
        }
    }
    throw "The preview did not become healthy at $uri. Use 'docker compose logs preview timescaledb' to inspect the environment."
}

function Print-SessionStatus($Session, $Identity) {
    $dependency = Get-PreviewDependencyIdentity $Identity
    Set-PreviewDependencyEnvironment $dependency
    try {
        $null = Assert-PreparedDependencies $Identity $dependency -VerifyContents
        $preparationState = 'READY'
        $preparationDetail = ''
    } catch {
        $preparationState = 'REQUIRED'
        $preparationDetail = ($_.Exception.Message -replace '\s+', ' ').Trim()
    }

    if ($null -eq $Session) {
        $resources = Get-StackResources
        $state = if ($resources.Containers.Count -or $resources.Volumes.Count -or $resources.Networks.Count) { 'ORPHANED_PREVIEW_STATE / RESET_REQUIRED' } else { 'NOT_STARTED' }
        Write-Output "STATE=$state"
        Write-Output "PROJECT=$projectName"
        Write-Output "CURRENT_HARNESS_SHA=$($Identity.Head)"
        Write-Output "PREPARATION=$preparationState"
        Write-Output "DEPENDENCY_KEY=$($dependency.Key)"
        if ($preparationDetail) { Write-Output "PREPARATION_DETAIL=$preparationDetail" }
        return
    }

    $state = $Session.state
    if ($Session.harnessSha -ne $Identity.Head -or $Session.harnessTree -ne $Identity.Tree) {
        $state = 'RESUME_BLOCKED_VERSION_MISMATCH'
    } elseif ($Session.state -in @('RUNNING', 'RESUMED')) {
        try {
            Wait-PreviewHealth ([int]$Session.webPort)
            $state = 'RUNNING'
        } catch {
            $state = 'INTERRUPTED_RESUMABLE'
        }
    }

    Write-Output "STATE=$state"
    Write-Output "SESSION_ID=$($Session.sessionId)"
    Write-Output "PRODUCT_BASE_SHA=$($Session.productBaseSha)"
    Write-Output "ACCEPTED_HARNESS_SHA=$($Session.acceptedHarnessSha)"
    Write-Output "BRANCH=$($Session.branch)"
    Write-Output "WEB_PORT=$($Session.webPort)"
    Write-Output "RESUMABLE=$($state -in @('PAUSED_RESUMABLE', 'INTERRUPTED_RESUMABLE', 'RUNNING'))"
    Write-Output "LAST_CHECKPOINT=$(($Session.lastCheckpoint -replace '\s+', ' ').Trim())"
    Write-Output "EVIDENCE_DIR=$sessionRoot"
    Write-Output "PREPARATION=$preparationState"
    Write-Output "DEPENDENCY_KEY=$($dependency.Key)"
    if ($preparationDetail) { Write-Output "PREPARATION_DETAIL=$preparationDetail" }
}

switch ($Command) {
    'status' {
        $identity = Get-HarnessIdentity
        Print-SessionStatus (Read-Session) $identity
    }
    'prepare' {
        $identity = Get-HarnessIdentity
        $dependency = Get-PreviewDependencyIdentity $identity
        Set-PreviewDependencyEnvironment $dependency
        if ($null -ne (Read-Session)) {
            throw 'Preparation is allowed only with no active audit session; pause/reset the product session first.'
        }
        $resources = Get-StackResources
        if ($resources.Containers.Count -or $resources.Volumes.Count -or $resources.Networks.Count) {
            throw 'Preparation is allowed only when the dedicated product/audit stack is absent; inspect or explicitly reset it first.'
        }
        Invoke-PreparedDependencyBootstrap $identity $dependency
    }
    'start' {
        if ([string]::IsNullOrWhiteSpace($AcceptedHarnessSha)) {
            throw "start requires -AcceptedHarnessSha <40-hex SHA> copied from Main's live ENV_A harness-acceptance order."
        }
        $identity = Get-HarnessIdentity
        if ($AcceptedHarnessSha.ToLowerInvariant() -ne $identity.Head) {
            throw "Accepted SHA does not match this exact harness HEAD ($($identity.Head)); refusing to start."
        }
        $existing = Read-Session
        if ($null -ne $existing) {
            throw "A session already exists in state '$($existing.state)'. Use resume, pause, status or explicit reset; start never replaces a session."
        }
        $resources = Get-StackResources
        if ($resources.Containers.Count -or $resources.Volumes.Count -or $resources.Networks.Count) {
            throw 'ORPHANED_PREVIEW_STATE: dedicated project containers/volumes exist without a manifest. Inspect them and use explicit reset; start will not reuse them.'
        }
        $dependency = Get-PreviewDependencyIdentity $identity
        Set-PreviewDependencyEnvironment $dependency
        $null = Assert-PreparedDependencies $identity $dependency -VerifyContents

        New-Item -ItemType Directory -Path $sessionRoot -Force | Out-Null
        $session = [pscustomobject]@{
            schemaVersion = 1
            sessionId = [Guid]::NewGuid().ToString('D')
            state = 'STARTING'
            productBaseSha = $identity.ProductBase
            acceptedHarnessSha = $identity.Head
            harnessSha = $identity.Head
            harnessTree = $identity.Tree
            branch = $identity.Branch
            dependencyKey = $dependency.Key
            nodeModulesVolume = $dependency.NodeModulesVolume
            nugetVolume = $dependency.NuGetVolume
            createdAtUtc = [DateTime]::UtcNow.ToString('o')
            updatedAtUtc = [DateTime]::UtcNow.ToString('o')
            webPort = $Port
            lastCheckpoint = 'New clean Environment A session; no project/user state was pre-seeded by the harness.'
            transitions = @([pscustomobject]@{ state = 'STARTING'; atUtc = [DateTime]::UtcNow.ToString('o') })
        }
        Save-Session $session
        try {
            Invoke-PreviewCompose -ComposeArguments @('up', '--no-build', '--pull', 'never', '--detach', '--wait', '--wait-timeout', '240')
            Wait-PreviewHealth $Port
            Add-Transition $session 'RUNNING' 'Clean application stack healthy; product remains available for the accepted audit.'
            Print-SessionStatus $session $identity
            Write-Output "WEB_URL=http://localhost:$Port"
        } catch {
            Add-Transition $session 'START_FAILED_RESUMABLE' 'Startup failed; inspect logs, then use status/resume or explicit reset.'
            throw
        }
    }
    'pause' {
        $identity = Get-HarnessIdentity
        $dependency = Get-PreviewDependencyIdentity $identity
        Set-PreviewDependencyEnvironment $dependency
        $session = Read-Session
        if ($null -eq $session) { throw 'No audit session exists to pause.' }
        Assert-ResumableSession $session $identity $dependency
        Invoke-PreviewCompose -ComposeArguments @('stop', '--timeout', '30')
        Add-Transition $session 'PAUSED_RESUMABLE' 'Runtime and database containers stopped cleanly; database volume, user project and local evidence were preserved.' $Checkpoint
        Print-SessionStatus $session $identity
    }
    'resume' {
        $identity = Get-HarnessIdentity
        $dependency = Get-PreviewDependencyIdentity $identity
        Set-PreviewDependencyEnvironment $dependency
        $session = Read-Session
        if ($null -eq $session) { throw 'No saved audit session exists. A new start requires Main acceptance; resume never creates one.' }
        Assert-ResumableSession $session $identity $dependency
        $null = Assert-PreparedDependencies $identity $dependency -VerifyContents
        $env:ELITESCADA_PREVIEW_PORT = "$($session.webPort)"
        Invoke-PreviewCompose -ComposeArguments @('up', '--no-build', '--pull', 'never', '--detach', '--wait', '--wait-timeout', '240')
        Wait-PreviewHealth ([int]$session.webPort)
        Add-Transition $session 'RESUMED' 'Same saved Environment A identity reopened; product/database state was not reset or reseeded.'
        Print-SessionStatus $session $identity
        Write-Output "WEB_URL=http://localhost:$($session.webPort)"
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
            $archiveName = 'preview-audit-a-' + [Guid]::NewGuid().ToString('N')
            $archivePath = Join-Path $archiveRoot $archiveName
            if (-not $archivePath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw 'Refusing to archive evidence outside this repository.'
            }
            Move-Item -LiteralPath $resolvedSession -Destination $archivePath
            Write-Output "ARCHIVED_EVIDENCE=$archivePath"
        }
        Write-Output "STATE=NOT_STARTED"
        Write-Output "RESET_SCOPE=only Docker Compose project $projectName and its dedicated product/audit containers, network and volumes; provenance-bound dependency volumes and tool image are preserved; evidence archived under ci/local/artifacts/preview-audit-archive"
    }
}
