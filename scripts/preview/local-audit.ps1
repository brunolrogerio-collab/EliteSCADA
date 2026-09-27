[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('start', 'pause', 'resume', 'status', 'reset')]
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
$repoPrefix = $repoRoot.TrimEnd('\') + '\'

function Invoke-Git([string[]]$GitArguments) {
    $result = & git -C $repoRoot @GitArguments
    if ($LASTEXITCODE -ne 0) { throw "git $($GitArguments -join ' ') failed." }
    return $result
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
    & docker compose @composeArgs
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed: $($ComposeArguments -join ' ')" }
}

function Get-StackResources {
    $containers = @(& docker ps --all --quiet --filter "label=com.docker.compose.project=$projectName")
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Docker containers for the dedicated preview project.' }
    $volumes = @(& docker volume ls --quiet --filter "label=com.docker.compose.project=$projectName")
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Docker volumes for the dedicated preview project.' }
    return [pscustomobject]@{ Containers = $containers; Volumes = $volumes }
}

function Read-Session {
    if (-not (Test-Path -LiteralPath $sessionFile)) { return $null }
    return (Get-Content -Raw -LiteralPath $sessionFile | ConvertFrom-Json)
}

function Save-Session($Session) {
    New-Item -ItemType Directory -Path $sessionRoot -Force | Out-Null
    $Session | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $sessionFile -Encoding utf8
}

function Add-Transition($Session, [string]$State, [string]$Message, [string]$CheckpointNote) {
    $Session.state = $State
    $Session.updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    if ($Message) { $Session.lastTransitionMessage = $Message }
    if ($CheckpointNote) { $Session.lastCheckpoint = $CheckpointNote }
    if ($null -eq $Session.transitions) { $Session | Add-Member -NotePropertyName transitions -NotePropertyValue @() }
    $Session.transitions += [pscustomobject]@{
        state = $State
        atUtc = $Session.updatedAtUtc
    }
    Save-Session $Session
}

function Assert-ResumableSession($Session, $Identity) {
    if ($Session.productBaseSha -ne $Identity.ProductBase -or
        $Session.acceptedHarnessSha -ne $Session.harnessSha -or
        $Session.harnessSha -ne $Identity.Head -or
        $Session.harnessTree -ne $Identity.Tree) {
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
    if ($null -eq $Session) {
        $resources = Get-StackResources
        $state = if ($resources.Containers.Count -or $resources.Volumes.Count) { 'ORPHANED_PREVIEW_STATE / RESET_REQUIRED' } else { 'NOT_STARTED' }
        Write-Output "STATE=$state"
        Write-Output "PROJECT=$projectName"
        Write-Output "CURRENT_HARNESS_SHA=$($Identity.Head)"
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
}

switch ($Command) {
    'status' {
        $identity = Get-HarnessIdentity
        Print-SessionStatus (Read-Session) $identity
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
        if ($resources.Containers.Count -or $resources.Volumes.Count) {
            throw 'ORPHANED_PREVIEW_STATE: dedicated project containers/volumes exist without a manifest. Inspect them and use explicit reset; start will not reuse them.'
        }

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
            createdAtUtc = [DateTime]::UtcNow.ToString('o')
            updatedAtUtc = [DateTime]::UtcNow.ToString('o')
            webPort = $Port
            lastCheckpoint = 'New clean Environment A session; no project/user state was pre-seeded by the harness.'
            transitions = @([pscustomobject]@{ state = 'STARTING'; atUtc = [DateTime]::UtcNow.ToString('o') })
        }
        Save-Session $session
        try {
            $env:ELITESCADA_PREVIEW_PORT = "$Port"
            Invoke-PreviewCompose -ComposeArguments @('up', '--build', '--detach', '--wait', '--wait-timeout', '240')
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
        $session = Read-Session
        if ($null -eq $session) { throw 'No audit session exists to pause.' }
        Assert-ResumableSession $session $identity
        Invoke-PreviewCompose -ComposeArguments @('stop', '--timeout', '30')
        Add-Transition $session 'PAUSED_RESUMABLE' 'Runtime and database containers stopped cleanly; database volume, user project and local evidence were preserved.' $Checkpoint
        Print-SessionStatus $session $identity
    }
    'resume' {
        $identity = Get-HarnessIdentity
        $session = Read-Session
        if ($null -eq $session) { throw 'No saved audit session exists. A new start requires Main acceptance; resume never creates one.' }
        Assert-ResumableSession $session $identity
        $env:ELITESCADA_PREVIEW_PORT = "$($session.webPort)"
        Invoke-PreviewCompose -ComposeArguments @('up', '--detach', '--wait', '--wait-timeout', '240')
        Wait-PreviewHealth ([int]$session.webPort)
        Add-Transition $session 'RESUMED' 'Same saved Environment A identity reopened; product/database state was not reset or reseeded.'
        Print-SessionStatus $session $identity
        Write-Output "WEB_URL=http://localhost:$($session.webPort)"
    }
    'reset' {
        $env:ELITESCADA_PREVIEW_PORT = "$Port"
        Invoke-PreviewCompose -ComposeArguments @('down', '--volumes', '--remove-orphans')
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
        Write-Output "RESET_SCOPE=only Docker Compose project $projectName and its dedicated volumes; evidence archived under ci/local/artifacts/preview-audit-archive"
    }
}
