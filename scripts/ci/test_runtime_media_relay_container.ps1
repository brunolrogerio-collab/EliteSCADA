[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo=(& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Repository required.' }
$tag=[Guid]::NewGuid().ToString('N')
$network='scadivo-relay-net-'+$tag
$camera='scadivo-relay-camera-'+$tag
$gateway='scadivo-relay-gateway-'+$tag
$image='bluenviron/mediamtx@sha256:dc22a8b09668e7f5b493cfa848c395349ad16fd47fef1cc7b7eb5b87a1186232'
$previousCamera=$env:ELITESCADA_MEDIA_LAB_CAMERA
foreach ($port in @(19997,19888)) {
    $probe=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port)
    try { $probe.Start() } finally { $probe.Stop() }
}
try {
    & docker network create $network | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Lab network creation failed.' }
    & docker run -d --name $camera --network $network --mount "type=bind,source=$repo/docker/media-lab/camera.yml,target=/mediamtx.yml,readonly" $image | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Lab camera creation failed.' }
    & docker run -d --name $gateway --network $network --mount "type=bind,source=$repo/docker/stable/mediamtx.yml,target=/mediamtx.yml,readonly" -p '127.0.0.1:19997:9997' -p '127.0.0.1:19888:8888' $image | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Lab gateway creation failed.' }
    $address=(& docker inspect $camera --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}').Trim()
    if ($LASTEXITCODE -ne 0 -or -not $address) { throw 'Lab camera address unavailable.' }
    $env:ELITESCADA_MEDIA_LAB_CAMERA="rtsp://${address}:8554/synthetic"
    & dotnet test "$repo/tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj" --no-restore --filter 'FullyQualifiedName~RuntimeMediaGatewayLabTests|FullyQualifiedName~RuntimeMediaRelayTests' --verbosity normal
    if ($LASTEXITCODE -ne 0) { throw 'Runtime media relay evidence failed.' }
} finally {
    $env:ELITESCADA_MEDIA_LAB_CAMERA=$previousCamera
    & docker rm -f -v $camera $gateway 2>$null | Out-Null
    & docker network rm $network 2>$null | Out-Null
}
