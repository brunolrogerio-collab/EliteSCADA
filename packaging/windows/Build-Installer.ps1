#requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$ProductCommit,
    [Parameter(Mandatory)][string]$CompilerPath,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
if (-not $IsWindows) { throw 'Build on Windows x64 with the .NET SDK, Node.js and Microsoft Edge.' }
$compiler = (Resolve-Path -LiteralPath $CompilerPath).Path
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $out) { throw "Choose a new output directory: $out" }
$recipeChanges = git -C $repo status --porcelain -- packaging/windows
if ($recipeChanges) { throw 'Commit the installer recipe before building to identify it precisely.' }
$recipeCommit = git -C $repo rev-parse HEAD
$resolved = git -C $repo rev-parse --verify "$ProductCommit^{commit}"
if ($resolved -ne $ProductCommit) { throw 'Unknown product commit.' }

$work = Join-Path ([IO.Path]::GetTempPath()) ('sw16-' + [Guid]::NewGuid().ToString('N').Substring(0,12))
$source = Join-Path $work 'src'
$payload = Join-Path $work 'payload'
$web = Join-Path $source 'web/scada-web'
$brand = Join-Path $work 'brand'
$api = Join-Path $payload 'api'
$published = Join-Path $work 'publish'
$ui = Join-Path $payload 'web'
foreach ($dir in @($source, $brand, $api, $ui, $out)) { New-Item -ItemType Directory -Path $dir | Out-Null }

# Only committed product/build inputs enter this private evaluation. Operator data and
# ignored local build products are never release inputs.
$inputs = @('Directory.Build.props', 'global.json', 'src', 'assets/sources', 'assets/licenses', 'assets/catalog/taxonomy.v1.json',
    'web/scada-web/package.json', 'web/scada-web/package-lock.json', 'web/scada-web/tsconfig.json',
    'web/scada-web/vite.config.ts', 'web/scada-web/index.html', 'web/scada-web/scripts/sync-pyodide.mjs',
    'web/scada-web/src', 'web/scada-web/public')
$archive = Join-Path $work 'product.zip'
git -C $repo archive --format=zip -o $archive $ProductCommit -- @inputs
tar.exe -xf $archive -C $source
$properties = ([xml](Get-Content "$source/Directory.Build.props" -Raw)).Project.PropertyGroup
$name = [string]$properties.EliteScadaProductName
$channel = [string]$properties.EliteScadaProductChannel
$version = [string]$properties.EliteScadaProductVersion
if ($name -ne 'SCADIVO' -or $channel -notmatch '^[a-zA-Z0-9-]+$' -or $version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw 'Invalid canonical product identity.'
}
$mark = Get-FileHash "$web/public/scadivo-mark.svg"
$productMark = git -C $repo rev-parse --verify "${ProductCommit}:web/scada-web/public/scadivo-mark.svg"
$approvedMark = git -C $repo rev-parse --verify "${recipeCommit}:website/scadivo/site/scadivo-mark.svg"
if ($productMark -ne $approvedMark) {
    throw 'The product and site marks differ. Review the artwork before building.'
}

$privateName = '(?i)(^\.env($|\.)|\.(pdb|pfx|p12|key|map|sqlite|db)$|^secrets\.json$|^appsettings.*local\.json$)'
$files = Get-ChildItem -LiteralPath $source -File -Recurse
if ($bad = $files | Where-Object Name -Match $privateName | Select-Object -First 1) {
    throw "Unexpected build input: $($bad.Name)"
}
# Inspect authored content which ships as readable code, prior to compilation.
$inspected = $files | Where-Object { $_.Extension -in '.py','.ps1','.sh','.mjs','.svg' -and
    $_.FullName -notmatch '\\(Scada\.LicenseGenerator|MatterWifi|node_modules)\\' }
if ($finding = $inspected | Select-String -Pattern '-----BEGIN (?:[A-Z ]+ )?PRIVATE KEY-----|[A-Z]:\\Users\\[^\\]+\\|(?:^|\s)(?:TODO|FIXME):' -List | Select-Object -First 1) {
    throw "Review build input $([IO.Path]::GetRelativePath($source,$finding.Path)), line $($finding.LineNumber)."
}

Push-Location $source
try {
    $sdk = dotnet --version
    dotnet publish src/Scada.Api/Scada.Api.csproj -c Release -r win-x64 --self-contained true -o $published -p:DebugType=None -p:DebugSymbols=false "-p:SourceRevisionId=$ProductCommit"
} finally { Pop-Location }
Push-Location $web
try {
    $nodeVersion = node --version
    $npmVersion = npm.cmd --version
    npm.cmd ci --prefer-offline --no-audit --no-fund
    npm.cmd run build
    node "$PSScriptRoot/render-brand.mjs" $web $brand
} finally { Pop-Location }

# Publish may include content brought in transitively by project references.
# Select the native payload rather than shipping that tree wholesale.
Get-ChildItem -LiteralPath $published -File | Where-Object {
    $_.Extension -in '.dll','.exe' -or $_.Name -in 'Scada.Api.deps.json','Scada.Api.runtimeconfig.json',
        'Scada.Api.staticwebassets.endpoints.json','Scada.DriverHost.deps.json','Scada.DriverHost.runtimeconfig.json','ServerScriptRunner.py'
} | Copy-Item -Destination $api
$sidecars = New-Item -ItemType Directory -Path "$api/sidecars"
foreach ($driver in 'knx-ip','zigbee-native','zwave-js') {
    Copy-Item -LiteralPath "$published/sidecars/$driver" -Recurse -Destination $sidecars
}
# Only the Vite runtime bundle, operator resources and the browser Python runtime.
foreach ($entry in 'assets','help','installation','index.html','scadivo-mark.svg') {
    Copy-Item -LiteralPath "$web/dist/$entry" -Destination $ui -Recurse
}
$pythonWeb = Join-Path $ui 'pyodide'
New-Item -ItemType Directory -Path $pythonWeb | Out-Null
foreach ($entry in 'pyodide.mjs','pyodide.asm.mjs','pyodide.asm.wasm','python_stdlib.zip','pyodide-lock.json') {
    Copy-Item -LiteralPath "$web/dist/pyodide/$entry" -Destination $pythonWeb
}
$notices = New-Item -ItemType Directory -Path "$payload/notices"
Copy-Item -LiteralPath "$source/assets/licenses/ELITESCADA-ORIGINAL-ASSET-NOTICE.md" -Destination $notices
$pyVersion = (Get-Content "$web/node_modules/pyodide/package.json" -Raw | ConvertFrom-Json).version
Copy-Item -LiteralPath "$PSScriptRoot/notices/pyodide-$pyVersion/LICENSE" -Destination "$notices/pyodide-LICENSE.txt"
$lock = Get-Content "$web/package-lock.json" -Raw | ConvertFrom-Json -AsHashtable
foreach ($entry in $lock.packages.GetEnumerator()) {
    if (!$entry.Key -or $entry.Value.dev) { continue }
    $dir = Join-Path $web $entry.Key
    if (!(Test-Path -LiteralPath $dir -PathType Container)) { continue }
    $texts = Get-ChildItem -LiteralPath $dir -File | Where-Object Name -Match '^(licen[sc]e|notice|copying|ofl)(\.|$)'
    if ($texts) {
        $target = New-Item -ItemType Directory -Path "$notices/npm/$($entry.Key.Replace('node_modules/',''))" -Force
        $texts | Copy-Item -Destination $target
    }
}
$assets = Get-Content "$source/src/Scada.Api/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
$deps = Get-Content "$api/Scada.Api.deps.json" -Raw | ConvertFrom-Json -AsHashtable
foreach ($library in $deps.libraries.GetEnumerator()) {
    if ($library.Value.type -notin 'package','runtimepack') { continue }
    $id = $library.Key -replace '^runtimepack\.',''
    foreach ($folder in $assets.packageFolders.Keys) {
        $dir = Join-Path $folder $id
        if (!(Test-Path -LiteralPath $dir -PathType Container)) { continue }
        $texts = Get-ChildItem -LiteralPath $dir -File | Where-Object Name -Match '^(licen[sc]e|third-party-notices|notice|copying)(\.|$)'
        if ($texts) {
            $target = New-Item -ItemType Directory -Path "$notices/nuget/$id" -Force
            $texts | Copy-Item -Destination $target
        }
        break
    }
}
Copy-Item -LiteralPath "$brand/scadivo.ico" -Destination $payload
foreach ($entry in 'Scada.Api.exe','Scada.Api.dll','Scada.Api.runtimeconfig.json','hostfxr.dll','coreclr.dll') {
    if (!(Test-Path -LiteralPath "$api/$entry" -PathType Leaf)) { throw "Missing win-x64 runtime file: $entry" }
}
if ((Get-Item -LiteralPath "$api/Scada.Api.dll").VersionInfo.FileVersion -ne $version) { throw 'API version mismatch.' }
$inventory = foreach ($file in (Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName)) {
    $relative = [IO.Path]::GetRelativePath($payload, $file.FullName).Replace('\','/')
    if ($file.Name -match $privateName -or $relative -match '(?i)(^|/)(MatterWifi|node_modules|tests-e2e|\.git)(/|$)') {
        throw "Unexpected package file: $relative"
    }
    [ordered]@{ path = $relative; bytes = $file.Length; sha256 = (Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant() }
}
$inventory | ConvertTo-Json -Depth 3 | Set-Content -Encoding utf8NoBOM "$out/files.json"
& $compiler /Q "/DProductName=$name" "/DChannel=$channel" "/DProductVersion=$version" "/DPayloadDir=$payload" "/DBrandDir=$brand" "/O$out" "$PSScriptRoot/scadivo.iss"
$exe = Get-Item -LiteralPath "$out/$name-$channel-$version-win-x64-lab.exe"
if ($exe.VersionInfo.FileVersion.Trim() -ne $version) { throw 'Installer version mismatch.' }
[ordered]@{
    product = "$name $channel $version"; productCommit = $ProductCommit; recipeCommit = $recipeCommit
    profile = 'windows-x64-payload-lab'; signed = $false; dotnetSdk = $sdk; node = $nodeVersion; npm = $npmVersion
    innoSetup = '6.7.3'; webLockSha256 = (Get-FileHash "$web/package-lock.json").Hash
    markSource = 'web/scada-web/public/scadivo-mark.svg'; markSha256 = $mark.Hash
    artwork = @(foreach ($asset in (Get-ChildItem -LiteralPath $brand -File | Sort-Object Name)) {
        [ordered]@{ name = $asset.Name; bytes = $asset.Length; sha256 = (Get-FileHash -LiteralPath $asset.FullName).Hash }
    })
    inventorySha256 = (Get-FileHash "$out/files.json").Hash; files = $inventory.Count
    artifact = $exe.Name; bytes = $exe.Length; sha256 = (Get-FileHash -LiteralPath $exe.FullName).Hash.ToLowerInvariant()
} | ConvertTo-Json | Set-Content -Encoding utf8NoBOM "$out/manifest.json"
Copy-Item -LiteralPath $brand -Destination $out -Recurse
Write-Output "Installer and evidence: $out"
# Only remove the generated short-path working tree of this successful build.
$resolvedWork = (Resolve-Path -LiteralPath $work).Path
if ([IO.Path]::GetDirectoryName($resolvedWork) -eq [IO.Path]::GetTempPath().TrimEnd('\') -and
    [IO.Path]::GetFileName($resolvedWork) -match '^sw16-[a-f0-9]{12}$') {
    Remove-Item -LiteralPath $resolvedWork -Recurse -Force
}
