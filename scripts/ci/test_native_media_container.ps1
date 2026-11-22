[CmdletBinding()]
param([string]$RunnerImage='python:3.12-slim')
$ErrorActionPreference='Stop'
$repository=(& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Run inside the SCADIVO repository.' }
$image='bluenviron/mediamtx@sha256:dc22a8b09668e7f5b493cfa848c395349ad16fd47fef1cc7b7eb5b87a1186232'
$nonce=[Guid]::NewGuid().ToString('N')
$source='scadivo-media-binary-'+$nonce
$runner='scadivo-media-proof-'+$nonce
$folder=Join-Path ([IO.Path]::GetTempPath()) ('scadivo-media-proof-'+$nonce)
$created=$false
New-Item -ItemType Directory -Path $folder | Out-Null
try {
    & docker create --name $source $image | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot prepare pinned media binary.' }
    $created=$true
    & docker cp "${source}:/mediamtx" (Join-Path $folder 'mediamtx')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot extract media binary.' }
    # No published ports or persistent state; both mounts are read-only.
    & docker run --rm --name $runner --mount "type=bind,source=$repository,target=/work,readonly" --mount "type=bind,source=$folder,target=/media-proof,readonly" --workdir /work --env PYTHONDONTWRITEBYTECODE=1 --env SCADIVO_MEDIA_BINARY=/media-proof/mediamtx --entrypoint python3 $RunnerImage -m unittest tests.ci.test_native_media_ports tests.ci.test_native_media_integration -v
    if ($LASTEXITCODE -ne 0) { throw 'Native media evidence failed.' }
} finally {
    if ($created) { & docker rm $source | Out-Null }
    # Delete only this invocation's verified temporary directory.
    $expected=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('scadivo-media-proof-'+$nonce)))
    if ([IO.Path]::GetFullPath($folder) -ne $expected) { throw 'Unsafe cleanup target.' }
    Remove-Item -LiteralPath $folder -Recurse -Force
}
