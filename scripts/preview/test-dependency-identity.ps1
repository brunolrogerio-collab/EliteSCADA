$ErrorActionPreference = 'Stop'
$modulePath = Join-Path $PSScriptRoot 'PreviewDependencyIdentity.psm1'
Import-Module $modulePath -Force

$normalizedBash = ConvertTo-PreviewBashScriptLf "set -Eeuo pipefail`r`nnext-command`r"
if ($normalizedBash -ne "set -Eeuo pipefail`nnext-command`n" -or $normalizedBash.Contains("`r")) {
    throw 'PowerShell-to-Bash normalization did not convert CRLF/CR to LF.'
}
Write-Output 'PASS: embedded Bash scripts are normalized to LF before execution.'

function Invoke-TestGit {
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & git -C $WorkingDirectory @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "git $($Arguments -join ' ') failed: $(@($output) -join [Environment]::NewLine)"
    }
    return (@($output) -join [Environment]::NewLine).Trim()
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) "elitescada-preview-dependency-test-$([Guid]::NewGuid().ToString('N'))"
$resolvedTempRoot = [IO.Path]::GetFullPath($tempRoot)
$systemTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $resolvedTempRoot.StartsWith($systemTempRoot, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Split-Path -Leaf $resolvedTempRoot).StartsWith('elitescada-preview-dependency-test-', [StringComparison]::Ordinal)) {
    throw "Refusing to use an unexpected temporary test path: $resolvedTempRoot"
}

try {
    New-Item -ItemType Directory -Path $resolvedTempRoot | Out-Null
    $seed = Join-Path $resolvedTempRoot 'seed'
    $lfClone = Join-Path $resolvedTempRoot 'lf'
    $crlfClone = Join-Path $resolvedTempRoot 'crlf'
    New-Item -ItemType Directory -Path $seed | Out-Null

    $null = Invoke-TestGit -WorkingDirectory $seed -Arguments @('init')
    $null = Invoke-TestGit -WorkingDirectory $seed -Arguments @('config', 'user.name', 'Preview identity test')
    $null = Invoke-TestGit -WorkingDirectory $seed -Arguments @('config', 'user.email', 'preview-identity-test@example.invalid')
    $null = Invoke-TestGit -WorkingDirectory $seed -Arguments @('config', 'core.autocrlf', 'false')
    [IO.File]::WriteAllText((Join-Path $seed 'dependency.txt'), "alpha`nbeta`n", [Text.UTF8Encoding]::new($false))
    $null = Invoke-TestGit -WorkingDirectory $seed -Arguments @('add', 'dependency.txt')
    $null = Invoke-TestGit -WorkingDirectory $seed -Arguments @('commit', '-m', 'seed dependency input')

    $null = Invoke-TestGit -WorkingDirectory $resolvedTempRoot -Arguments @('clone', '--no-checkout', $seed, $lfClone)
    $null = Invoke-TestGit -WorkingDirectory $lfClone -Arguments @('config', 'core.autocrlf', 'false')
    $null = Invoke-TestGit -WorkingDirectory $lfClone -Arguments @('checkout', 'HEAD')

    $null = Invoke-TestGit -WorkingDirectory $resolvedTempRoot -Arguments @('clone', '--no-checkout', $seed, $crlfClone)
    $null = Invoke-TestGit -WorkingDirectory $crlfClone -Arguments @('config', 'core.autocrlf', 'true')
    $null = Invoke-TestGit -WorkingDirectory $crlfClone -Arguments @('checkout', 'HEAD')

    $lfBytes = [IO.File]::ReadAllBytes((Join-Path $lfClone 'dependency.txt'))
    $crlfBytes = [IO.File]::ReadAllBytes((Join-Path $crlfClone 'dependency.txt'))
    if ([Convert]::ToBase64String($lfBytes) -eq [Convert]::ToBase64String($crlfBytes)) {
        throw 'The regression fixture did not produce distinct LF and CRLF working-tree bytes.'
    }

    $lfTree = Invoke-TestGit -WorkingDirectory $lfClone -Arguments @('rev-parse', 'HEAD^{tree}')
    $crlfTree = Invoke-TestGit -WorkingDirectory $crlfClone -Arguments @('rev-parse', 'HEAD^{tree}')
    if ($lfTree -ne $crlfTree) { throw 'The regression clones do not have the same committed Git tree.' }
    if ((Invoke-TestGit -WorkingDirectory $lfClone -Arguments @('status', '--porcelain')) -or
        (Invoke-TestGit -WorkingDirectory $crlfClone -Arguments @('status', '--porcelain'))) {
        throw 'The LF/CRLF regression clones must both be clean.'
    }

    $lfIdentity = Get-PreviewDependencyInputIdentity -RepositoryRoot $lfClone -RelativePaths @('dependency.txt')
    $crlfIdentity = Get-PreviewDependencyInputIdentity -RepositoryRoot $crlfClone -RelativePaths @('dependency.txt')
    if ($lfIdentity.InputsSha -ne $crlfIdentity.InputsSha) {
        throw 'Dependency identity changed across LF/CRLF checkouts of the same committed tree.'
    }

    [IO.File]::WriteAllText((Join-Path $lfClone 'dependency.txt'), "alpha`nbeta`ngamma`n", [Text.UTF8Encoding]::new($false))
    $null = Invoke-TestGit -WorkingDirectory $lfClone -Arguments @('add', 'dependency.txt')
    $null = Invoke-TestGit -WorkingDirectory $lfClone -Arguments @('commit', '-m', 'change dependency input')
    $changedIdentity = Get-PreviewDependencyInputIdentity -RepositoryRoot $lfClone -RelativePaths @('dependency.txt')
    if ($changedIdentity.InputsSha -eq $lfIdentity.InputsSha) {
        throw 'Dependency identity did not change when the committed dependency input changed.'
    }

    Write-Output 'PASS: same committed tree has the same dependency identity across LF/CRLF working trees.'
    Write-Output 'PASS: changing a committed dependency input changes the dependency identity.'
} finally {
    if (Test-Path -LiteralPath $resolvedTempRoot) {
        Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force
    }
}
