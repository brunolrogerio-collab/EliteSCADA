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
foreach ($command in @('prepare', 'start', 'status', 'pause', 'resume', 'stop', 'restart', 'diagnose', 'reset')) {
    if ($help -notmatch "(?m)^\s+$command\s+") { throw "Operator help does not document '$command'." }
}
Write-Output 'PASS: operator help lists every required lifecycle command.'

foreach ($entry in @(
    @{ Path = $operatorScript; Name = 'elite-local' },
    @{ Path = $implementationScript; Name = 'local-audit' }
)) {
    try {
        & $entry.Path reset | Out-Null
        throw "FAIL: $($entry.Name) accepted reset without -Force."
    } catch {
        if ($_.Exception.Message -notmatch 'RESET_CONFIRMATION_REQUIRED') { throw }
    }
}
Write-Output 'PASS: reset is refused before Docker/state access unless -Force is explicit.'

$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($implementationScript, [ref]$tokens, [ref]$errors)
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
