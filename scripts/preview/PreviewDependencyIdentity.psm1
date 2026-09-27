function Get-PreviewGitOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & git -C $RepositoryRoot @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "git $($Arguments -join ' ') failed while calculating dependency provenance."
    }
    return (@($output) -join [Environment]::NewLine).Trim()
}

function Get-PreviewDependencyInputIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string[]]$RelativePaths
    )

    $root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $normalizedPaths = @($RelativePaths | ForEach-Object { $_.Replace('\', '/') } | Sort-Object -Unique)
    $entries = @()

    foreach ($relativePath in $normalizedPaths) {
        if ([string]::IsNullOrWhiteSpace($relativePath) -or
            [IO.Path]::IsPathRooted($relativePath) -or
            $relativePath.Split('/') -contains '..') {
            throw "Invalid repository-relative dependency path: $relativePath"
        }

        $blobId = Get-PreviewGitOutput -RepositoryRoot $root -Arguments @('rev-parse', '--verify', "HEAD:$relativePath")
        $objectType = Get-PreviewGitOutput -RepositoryRoot $root -Arguments @('cat-file', '-t', $blobId)
        if ($objectType -ne 'blob') {
            throw "Dependency provenance input is not a committed Git blob: $relativePath"
        }
        $entries += "$relativePath=$blobId"
    }

    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($entries -join "`n")
        $inputsSha = ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }

    return [pscustomobject]@{
        InputsSha = $inputsSha
        Entries = $entries
    }
}

Export-ModuleMember -Function Get-PreviewDependencyInputIdentity
