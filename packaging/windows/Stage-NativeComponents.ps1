param(
    [Parameter(Mandatory)][string]$DependencyDirectory,
    [Parameter(Mandatory)][string]$WorkingDirectory,
    [Parameter(Mandatory)][string]$Payload,
    [Parameter(Mandatory)][string]$BrandDirectory
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$archives = Get-Content "$PSScriptRoot/dependencies.json" -Raw | ConvertFrom-Json
foreach ($dependency in $archives) {
    $file = Join-Path $DependencyDirectory $dependency.file
    if ((Get-FileHash -LiteralPath $file).Hash -ne $dependency.sha256) {
        throw "Dependency SHA-256 mismatch: $($dependency.file)"
    }
}
$pgStage = Join-Path $WorkingDirectory 'pg-extract'
$tsStage = Join-Path $WorkingDirectory 'ts-extract'
$hostDir = Join-Path $Payload 'host'
$pgDir = Join-Path $Payload 'postgres'
$thirdParty = Join-Path $Payload 'notices/windows'
foreach ($directory in $pgStage,$tsStage,$hostDir,$pgDir,$thirdParty) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}
tar.exe -xf "$DependencyDirectory/postgres.zip" -C $pgStage pgsql/bin pgsql/lib pgsql/share pgsql/server_license.txt pgsql/commandlinetools_3rd_party_licenses.txt
tar.exe -xf "$DependencyDirectory/timescale.zip" -C $tsStage
$pgSource = Join-Path $pgStage 'pgsql'
$tsSource = Join-Path $tsStage 'timescaledb'
Copy-Item "$pgSource/lib","$pgSource/share" -Destination $pgDir -Recurse
$pgBin = New-Item -ItemType Directory -Path "$pgDir/bin"
$postgresTools = 'postgres.exe','pg_ctl.exe','initdb.exe','pg_isready.exe','psql.exe','pg_dump.exe','pg_restore.exe','pg_dumpall.exe','pg_controldata.exe'
Get-ChildItem "$pgSource/bin" -File | Where-Object {
    $_.Name -in $postgresTools -or ($_.Extension -eq '.dll' -and $_.Name -notmatch '^wx|^testplug')
} | Copy-Item -Destination $pgBin
Get-ChildItem "$tsSource/*.dll" | Copy-Item -Destination "$pgDir/lib"
Get-ChildItem "$tsSource/*.sql","$tsSource/*.control" | Copy-Item -Destination "$pgDir/share/extension"
foreach ($tool in $postgresTools) {
    if (!(Test-Path -LiteralPath "$pgBin/$tool")) { throw "Missing PostgreSQL executable: $tool" }
}
if (!(Test-Path -LiteralPath "$pgDir/share/extension/timescaledb.control")) { throw 'TimescaleDB extension is missing.' }
Copy-Item "$pgSource/server_license.txt","$pgSource/commandlinetools_3rd_party_licenses.txt" -Destination $thirdParty
Copy-Item "$PSScriptRoot/notices/windows/*" -Destination $thirdParty
Copy-Item "$DependencyDirectory/WinSW.exe" -Destination "$hostDir/ScadivoService.exe"
Copy-Item "$PSScriptRoot/Install-Host.ps1" -Destination $hostDir
Copy-Item "$PSScriptRoot/Primeiros passos.html" -Destination $Payload
Copy-Item "$DependencyDirectory/vc_redist.x64.exe" -Destination $hostDir
Copy-Item "$PSScriptRoot/dependencies.json" -Destination $thirdParty
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /target:winexe /platform:x64 /r:System.ServiceProcess.dll /codepage:65001 /optimize+ /warnaserror "/win32icon:$BrandDirectory/scadivo.ico" "/out:$Payload/ScadivoTray.exe" "$PSScriptRoot/ScadivoTray.cs"
