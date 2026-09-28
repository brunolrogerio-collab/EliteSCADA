$ErrorActionPreference = 'Stop'
$operatorScript = Join-Path $PSScriptRoot 'elite-local.ps1'
$implementationScript = Join-Path $PSScriptRoot 'local-audit.ps1'

foreach ($path in @($operatorScript, $implementationScript)) {
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count -gt 0) {
        throw "PowerShell syntax errors in '$path': $($errors -join '; ')"
    }
}
Write-Output 'PASS: local operator PowerShell syntax.'

$help = (& $operatorScript help | Out-String)
foreach ($command in @('launch', 'prepare', 'start', 'status', 'pause', 'resume', 'stop', 'restart', 'diagnose', 'reset')) {
    if ($help -notmatch "(?m)^\s+$command\s+") { throw "Operator help does not document '$command'." }
}
Write-Output 'PASS: operator help lists every required lifecycle command.'

$scopeTokens = $null
$scopeParseErrors = $null
$scopeAst = [System.Management.Automation.Language.Parser]::ParseFile($implementationScript, [ref]$scopeTokens, [ref]$scopeParseErrors)
$identityFunction = $scopeAst.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-HarnessIdentity' }, $true)
if ($null -eq $identityFunction) { throw 'Harness identity scope guard was not found.' }
$script:repoRoot = 'mock-repository-root'
$script:productBaseSha = '1111111111111111111111111111111111111111'
$script:Profile = 'audit'
$script:scopeChangedPaths = @()
function Invoke-Git {
    param([string[]]$GitArguments)
    if ($GitArguments[0] -eq 'diff') { return @($script:scopeChangedPaths) }
    if ($GitArguments[0] -eq 'status') { return @() }
    if ($GitArguments[0] -eq 'branch') { return 'preview/w15-vs-local-runner' }
    if ($GitArguments[0] -eq 'merge-base') { return $script:productBaseSha }
    if ($GitArguments[0] -eq 'rev-parse') {
        if ($GitArguments[1] -eq 'HEAD') { return '2222222222222222222222222222222222222222' }
        if ($GitArguments[1] -eq 'HEAD^{tree}') { return '3333333333333333333333333333333333333333' }
        if ($GitArguments[1] -eq "$script:productBaseSha^{commit}") { return $script:productBaseSha }
    }
    throw "Unexpected mocked Git invocation: $($GitArguments -join ' ')"
}
. ([scriptblock]::Create($identityFunction.Extent.Text))
$pr362ChangedPaths = @(
    'ci/local/docker-compose.preview.dev.yml',
    'ci/local/docker-compose.preview.local.yml',
    'docs/LOCAL-ELITESCADA-OPERATIONS-EVIDENCE.md',
    'docs/LOCAL-FIRST-PROJECT-PREVIEW-HARNESS.md',
    'docs/VISUAL-STUDIO-AI-LOCAL-ELITESCADA-BOOTSTRAP.md',
    'scripts/preview/elite-local.ps1',
    'scripts/preview/local-audit.ps1',
    'scripts/preview/test-local-operator.ps1'
)
$script:scopeChangedPaths = $pr362ChangedPaths
$acceptedIdentity = Get-HarnessIdentity
if ($acceptedIdentity.ProductScopeChanges.Count -ne 0) {
    throw "The exact PR #362 changed-path set was rejected: $($acceptedIdentity.ProductScopeChanges -join ', ')"
}
Write-Output 'PASS: the exact PR #362 changed-path set is allowed by the strict harness scope guard.'

foreach ($rejectedPath in @('src/Scada.Security/Authorization/CapabilityAuthorizationService.cs', 'docs/UNAPPROVED.md')) {
    $script:scopeChangedPaths = @($pr362ChangedPaths + $rejectedPath)
    try {
        $null = Get-HarnessIdentity
        throw "FAIL: harness scope guard accepted '$rejectedPath'."
    } catch {
        if ($_.Exception.Message -notmatch 'Product-scope changes are not allowed' -or
            $_.Exception.Message -notmatch [regex]::Escape($rejectedPath)) {
            throw
        }
    }
}
Write-Output 'PASS: product source and non-allowlisted documentation remain rejected by the strict harness scope guard.'

foreach ($entry in @(
    @{ Path = $operatorScript; Name = 'elite-local' },
    @{ Path = $implementationScript; Name = 'local-audit' }
)) {
    foreach ($profile in @('audit', 'development')) {
        try {
            & $entry.Path reset -Profile $profile | Out-Null
            throw "FAIL: $($entry.Name) accepted reset for '$profile' without -Force."
        } catch {
            if ($_.Exception.Message -notmatch 'RESET_CONFIRMATION_REQUIRED') { throw }
        }
    }
}
Write-Output 'PASS: reset is refused before Docker/state access unless -Force is explicit.'

$auditCompose = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '..\..\ci\local\docker-compose.preview.local.yml')
$developmentCompose = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '..\..\ci\local\docker-compose.preview.dev.yml')
if ($auditCompose -notmatch '127\.0\.0\.1:\$\{ELITESCADA_PREVIEW_PORT' -or
    $developmentCompose -notmatch 'preview-dev:/preview-evidence') {
    throw 'Local development Compose profile is not isolated from audit evidence or loopback-bound.'
}
Write-Output 'PASS: development profile uses its own evidence mount and remains loopback-bound by the shared Compose base.'

$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($implementationScript, [ref]$tokens, [ref]$errors)
$stackFunction = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-StackContainers' }, $true)
if ($null -eq $stackFunction) { throw 'Docker container snapshot function was not found.' }
$script:projectName = 'elitescada-preview-dev'
$script:previewInspectJson = '{"Config":{"Labels":{"com.docker.compose.service":"preview"}},"State":{"Health":{"Status":"healthy"}}}'
$script:databaseInspectJson = '{"Config":{"Labels":{"com.docker.compose.service":"timescaledb"}},"State":{"Health":{"Status":"healthy"}}}'
function Invoke-NativeCommand {
    param([string]$FilePath, [string[]]$Arguments)
    $global:LASTEXITCODE = 0
    if ($Arguments[0] -eq 'ps') {
        return @(
            'preview-id|elitescada-preview-dev-preview-1|running|Up 1 minute (healthy)',
            'database-id|elitescada-preview-dev-timescaledb-1|running|Up 1 minute (healthy)'
        )
    }
    if ($Arguments[0] -eq 'inspect') {
        if ($Arguments[1] -eq 'preview-id') { return $script:previewInspectJson }
        if ($Arguments[1] -eq 'database-id') { return $script:databaseInspectJson }
    }
    $global:LASTEXITCODE = 1
    return
}
. ([scriptblock]::Create($stackFunction.Extent.Text))
$containerSnapshot = Get-StackContainers
if (-not $containerSnapshot.Available -or $containerSnapshot.Containers.Count -ne 2) {
    throw 'Docker container inspection did not return both fixture services.'
}
$previewFixture = $containerSnapshot.Containers | Where-Object { $_.service -eq 'preview' } | Select-Object -First 1
$databaseFixture = $containerSnapshot.Containers | Where-Object { $_.service -eq 'timescaledb' } | Select-Object -First 1
if ($null -eq $previewFixture -or $previewFixture.health -ne 'healthy' -or
    $null -eq $databaseFixture -or $databaseFixture.health -ne 'healthy') {
    throw 'Docker inspection JSON did not preserve service names and health states.'
}
Write-Output 'PASS: Docker inspection JSON yields service and health state in Windows PowerShell 5.1 and PowerShell 7.'

$redactor = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Protect-DiagnosticText' }, $true)
if ($null -eq $redactor) { throw 'Diagnostic redaction function was not found.' }
. ([scriptblock]::Create($redactor.Extent.Text))
$sample = 'Authorization: Bearer opaque-token password=local-db-secret token=refresh-secret https://localhost/?access_token=query-secret'
$sanitized = Protect-DiagnosticText $sample
foreach ($secret in @('opaque-token', 'local-db-secret', 'refresh-secret', 'query-secret')) {
    if ($sanitized.Contains($secret)) { throw "Diagnostic redaction leaked '$secret'." }
}
Write-Output 'PASS: diagnostic log redaction removes common authorization, password, token and query-secret values.'

Write-Output 'Local operator checks passed. Docker/product lifecycle was not started or reset.'
