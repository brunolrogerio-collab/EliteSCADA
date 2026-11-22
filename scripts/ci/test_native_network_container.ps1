[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repository = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Run inside the SCADIVO repository.' }
$image='postgres:18-alpine@sha256:898c60cb390d1085bc97bdd6a1ae968757421df394b071848b52b1c09c89d6a5'
$name='scadivo-native-network-proof-'+[Guid]::NewGuid().ToString('N')
$command="apk add --no-cache python3 >/dev/null; su postgres -s /bin/sh -c 'python3 -m unittest tests.ci.test_native_postgresql_integration tests.ci.test_native_network_adapter tests.ci.test_native_database_port tests.ci.test_native_operation_coordination tests.ci.test_native_profile_provisioning -v'"
# No host ports, external volumes, or existing container operations.
# --rm also removes the image-declared anonymous volume after the run.
& docker run --rm --name $name --mount "type=bind,source=$repository,target=/work,readonly" --workdir /work --env SCADIVO_NATIVE_PG_BIN=/usr/local/bin --env PYTHONDONTWRITEBYTECODE=1 --entrypoint /bin/sh $image -ec $command
if ($LASTEXITCODE -ne 0) { throw 'Native network container evidence failed.' }
