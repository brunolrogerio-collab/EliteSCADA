$ErrorActionPreference = 'Stop'

$testScripts = @(
    (Join-Path $PSScriptRoot 'test-local-operator.ps1'),
    (Join-Path $PSScriptRoot 'test-dependency-identity.ps1')
)

Write-Output "Local operator checks - PowerShell $($PSVersionTable.PSVersion)"
foreach ($testScript in $testScripts) {
    if (-not (Test-Path -LiteralPath $testScript -PathType Leaf)) {
        throw "Required local regression script is missing: $testScript"
    }
    $testName = [IO.Path]::GetFileName($testScript)
    Write-Output "RUNNING=$testName"
    & $testScript
}

Write-Output 'LOCAL_TESTS=PASSED'
Write-Output 'PRODUCT_CONTAINERS_STARTED_OR_RESET=false'
