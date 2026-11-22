#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Prepare','Install','Remove')][string]$Action,
    [Parameter(Mandatory)][string]$Release,
    [ValidateRange(1024,65535)][int]$WebPort = 18080,
    [ValidateRange(1024,65535)][int]$ApiPort = 15120,
    [ValidateRange(1024,65535)][int]$DbPort = 15432
)

$ErrorActionPreference = 'Stop'
$root = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'SCADIVO Windows'
$programRoot = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'SCADIVO\releases'
$Release = [IO.Path]::GetFullPath($Release)
if ([IO.Path]::GetDirectoryName($Release) -ne $programRoot -or [IO.Path]::GetFileName($Release) -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw 'Unexpected SCADIVO release directory.'
}
$apiService = 'ScadivoRuntime'
$dbService = 'ScadivoPostgres'
$settings = Join-Path $root 'instance.json'
$pgData = Join-Path $root 'postgres'
$logs = Join-Path $root 'logs'
$apiConfig = Join-Path $root 'config'
$apiSettings = Join-Path $apiConfig 'appsettings.json'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator access is required.' }

function New-PrivateDirectory([string]$path, [switch]$database) {
    $acl = New-Object Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $identity = 'S-1-5-18','S-1-5-32-544'
    if ($database) { $identity += 'S-1-5-20' }
    $acl.SetOwner((New-Object Security.Principal.SecurityIdentifier('S-1-5-32-544')))
    foreach ($sid in $identity) {
        $rule = New-Object Security.AccessControl.FileSystemAccessRule(
            (New-Object Security.Principal.SecurityIdentifier($sid)),
            'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    if ([IO.Directory]::Exists($path)) {
        $folder = Get-Item -LiteralPath $path -Force
        if ($folder.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unexpected junction in the SCADIVO data root.' }
        $existing = Get-Acl -LiteralPath $path
        $owner = $existing.GetOwner([Security.Principal.SecurityIdentifier]).Value
        $rules = $existing.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])
        if ($owner -notin 'S-1-5-18','S-1-5-32-544' -or !$existing.AreAccessRulesProtected -or
            ($rules | Where-Object { $_.IdentityReference.Value -notin $identity })) {
            throw 'SCADIVO data directory permissions need administrator review.'
        }
        return
    }
    [void][IO.Directory]::CreateDirectory($path,$acl)
}

function Invoke-Checked([string]$exe,[string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$(Split-Path $exe -Leaf) failed with exit code $LASTEXITCODE." }
}

function Stop-ManagedService([string]$name) {
    $service = Get-Service -Name $name -ErrorAction SilentlyContinue
    if (!$service) { return }
    if ($service.Status -ne 'Stopped') {
        Stop-Service -InputObject $service -ErrorAction Stop
        $service.WaitForStatus('Stopped',[TimeSpan]::FromSeconds(75))
    }
}

function Confirm-ServiceOwnership([string]$name, [string]$suffix) {
    $entry = Get-CimInstance Win32_Service -Filter "Name='$name'"
    if ($entry) {
        $pattern = '^"?' + [regex]::Escape($programRoot) + '\\\d+\.\d+\.\d+\.\d+\\' + [regex]::Escape($suffix) + '(?:"|\s|$)'
        if (!$entry.PathName -or $entry.PathName -notmatch $pattern) { throw "Service name already in use: $name." }
    }
}

function New-SecretBytes {
    $bytes = New-Object byte[] 32
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    return ,$bytes
}

function New-HexSecret { return [BitConverter]::ToString((New-SecretBytes)).Replace('-','').ToLowerInvariant() }

function Save-Json([string]$path, $object) {
    $staging = "$path.new"
    [IO.File]::WriteAllText($staging, ($object | ConvertTo-Json -Depth 9))
    if ([IO.File]::Exists($path)) { [IO.File]::Replace($staging,$path,$null) }
    else { [IO.File]::Move($staging,$path) }
}
function Set-NativeServicePath([string]$name, [string]$imagePath) {
    $entry = Get-CimInstance Win32_Service -Filter "Name='$name'"
    $result = Invoke-CimMethod -InputObject $entry -MethodName Change -Arguments @{PathName=$imagePath;StartMode='Automatic'}
    if ($result.ReturnValue) { throw "Service $name could not be configured ($($result.ReturnValue))." }
}
function Assert-Ports([int[]]$ports) {
    if (@($ports | Select-Object -Unique).Count -ne $ports.Count) { throw 'Choose three different ports.' }
    foreach ($port in $ports) {
        $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback,$port)
        $listener.ExclusiveAddressUse = $true
        try { $listener.Start() } catch { throw "127.0.0.1:$port is in use. Choose another port." }
        finally { $listener.Stop() }
    }
}
function Wait-Http([int]$port,[string]$route) {
    $deadline = (Get-Date).AddSeconds(100)
    do {
        try {
            $response = Invoke-WebRequest -UseBasicParsing "http://127.0.0.1:$port$route" -TimeoutSec 3
            if ($response.StatusCode -eq 200) { return }
        } catch {}
        Start-Sleep -Milliseconds 600
    } until ((Get-Date) -ge $deadline)
    throw "SCADIVO did not respond on port $port. Consult the service logs."
}
function Set-ServiceVersion([string]$versionRoot) {
    $pgCtl = Join-Path $versionRoot 'postgres\bin\pg_ctl.exe'
    if (Get-Service $dbService -ErrorAction SilentlyContinue) {
        Set-NativeServicePath $dbService ('"{0}" runservice -N "{1}" -D "{2}" -w' -f $pgCtl,$dbService,$pgData)
    } else {
        Invoke-Checked $pgCtl @('register','-N',$dbService,'-D',$pgData,'-S','auto','-U','NT AUTHORITY\NetworkService','-w')
    }
    $wrapper = Join-Path $versionRoot 'host\ScadivoService.exe'
    if (Get-Service $apiService -ErrorAction SilentlyContinue) {
        Set-NativeServicePath $apiService ('"{0}"' -f $wrapper)
    } else {
        Invoke-Checked $wrapper @('install')
    }
}
function Write-HostConfiguration([string]$versionRoot, $instance) {
    $connection = "Host=127.0.0.1;Port=$($instance.DbPort);Database=scadivo;Username=scadivo;Password=$($instance.ApiPassword)"
    Save-Json $apiSettings ([ordered]@{
        Kestrel = @{ Endpoints = @{
            Web = @{ Url="http://127.0.0.1:$($instance.WebPort)" }
            Api = @{ Url="http://127.0.0.1:$($instance.ApiPort)" }
        }}
        ConnectionStrings = @{ EliteScada=$connection; Historian=$connection }
        Authentication = @{ Enabled=$true; Jwt=@{Issuer='scadivo-local';Audience='scadivo-local';SigningKey=$instance.JwtKey}
            Local=@{Enabled=$true;SecureCookie=$true}}
        DatabaseTopology = @{ RequireDurable=$true; LocalManagedPort=$instance.DbPort
            StateFile=(Join-Path $root 'database-topology.json')
            Secrets=@{ StoreFile=(Join-Path $root 'database-secrets.json'); MasterKeyBase64=$instance.MaterialKey }}
        InstallationNetwork = @{ WebRootPath=(Join-Path $versionRoot 'web')
            WebPublishedPort=$instance.WebPort; ApiPublishedPort=$instance.ApiPort; DatabasePublishedPort=$instance.DbPort }
        Historian = @{ Provider='timescaledb'; Archive=@{RootPath=(Join-Path $root 'historian-archives')} }
        Licensing = @{ LicenseFile=(Join-Path $root 'SCADIVO.license'); Online=@{StateDirectory=(Join-Path $root 'online-licensing')} }
        ProtectedMaterial = @{ Store=@{Path=(Join-Path $root 'protected-material');ProtectionKeyFile=(Join-Path $apiConfig 'material.key')} }
    })
    [IO.File]::WriteAllText((Join-Path $apiConfig 'material.key'),$instance.MaterialKey)
    $xmlPath = Join-Path $versionRoot 'host\ScadivoService.xml'
    $xml = [xml]@"
<service>
  <id>$apiService</id>
  <name>SCADIVO Runtime</name>
  <description>SCADIVO Engineering and Runtime</description>
  <executable></executable>
  <arguments></arguments>
  <workingdirectory></workingdirectory>
  <logpath></logpath>
  <log mode="roll-by-size"><sizeThreshold>4096</sizeThreshold><keepFiles>5</keepFiles></log>
  <depend>$dbService</depend>
  <startmode>Automatic</startmode>
  <stoptimeout>45 sec</stoptimeout>
  <onfailure action="restart" delay="10 sec" />
  <onfailure action="none" />
</service>
"@
    $xml.service.executable = Join-Path $versionRoot 'api\Scada.Api.exe'
    $xml.service.arguments = '--contentRoot "' + $apiConfig + '" --environment Production'
    $xml.service.workingdirectory = $apiConfig
    $xml.service.logpath = $logs
    $xml.Save($xmlPath)
}

try {
    Confirm-ServiceOwnership $apiService 'host\ScadivoService.exe'
    Confirm-ServiceOwnership $dbService 'postgres\bin\pg_ctl.exe'
    if ($Action -eq 'Remove') {
        Stop-ManagedService $apiService
        Stop-ManagedService $dbService
        foreach ($name in $apiService,$dbService) {
            if (Get-Service $name -ErrorAction SilentlyContinue) { Invoke-Checked "$env:WINDIR\System32\sc.exe" @('delete',$name) }
        }
        $xmlPath = Join-Path $Release 'host\ScadivoService.xml'
        if ([IO.File]::Exists($xmlPath)) { [IO.File]::Delete($xmlPath) }
        exit 0
    }
    New-PrivateDirectory $root
    New-PrivateDirectory $pgData -database
    New-PrivateDirectory $logs
    New-PrivateDirectory $apiConfig
    $instance = $null
    if (Test-Path -LiteralPath $settings) { $instance = Get-Content -LiteralPath $settings -Raw | ConvertFrom-Json }
    if ($instance) {
        $previous = $instance.ActiveRelease
        if ($previous -and ([version](Split-Path $Release -Leaf)) -lt ([version](Split-Path $previous -Leaf))) {
            throw 'Downgrade requires restoring a compatible backup.'
        }
        $WebPort = $instance.WebPort; $ApiPort = $instance.ApiPort; $DbPort = $instance.DbPort
    }
    if ($Action -eq 'Prepare') {
        if (!$instance) { Assert-Ports @($WebPort,$ApiPort,$DbPort) }
        Stop-ManagedService $apiService
        Stop-ManagedService $dbService
        exit 0
    }
    Assert-Ports @($WebPort,$ApiPort,$DbPort)
    $redistKey = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64' -ErrorAction SilentlyContinue
    if (!$redistKey -or $redistKey.Installed -ne 1 -or [version]($redistKey.Version.TrimStart('v')) -lt [version]'14.40') {
        $redist = Start-Process -FilePath "$Release\host\vc_redist.x64.exe" -ArgumentList '/install /quiet /norestart' -WindowStyle Hidden -Wait -PassThru
        if ($redist.ExitCode -notin 0,1638) { throw "Visual C++ prerequisite returned $($redist.ExitCode). Restart Windows if required and run the installer again." }
    }
    if (!$instance) {
        if (Get-ChildItem -LiteralPath $pgData -Force | Select-Object -First 1) { throw 'Existing PostgreSQL data needs its original SCADIVO configuration.' }
        $instance = [pscustomobject]@{
            WebPort=$WebPort; ApiPort=$ApiPort; DbPort=$DbPort; ActiveRelease=$null
            DbPassword=(New-HexSecret); ApiPassword=(New-HexSecret); JwtKey=(New-HexSecret)
            MaterialKey=[Convert]::ToBase64String((New-SecretBytes))
        }
        Save-Json $settings $instance
    }
    $pgBin = Join-Path $Release 'postgres\bin'
    if (!(Test-Path -LiteralPath "$pgData\PG_VERSION")) {
        if (Get-ChildItem -LiteralPath $pgData -Force | Select-Object -First 1) {
            throw 'The PostgreSQL data directory contains an incomplete initialization.'
        }
        $passwordFile = Join-Path $apiConfig 'initdb.pw'
        try {
            [IO.File]::WriteAllText($passwordFile,$instance.DbPassword)
            Invoke-Checked "$pgBin\initdb.exe" @('-D',$pgData,'-U','postgres','--encoding=UTF8','--locale=C','--auth=scram-sha-256',"--pwfile=$passwordFile")
        } finally { [IO.File]::Delete($passwordFile) }
        [IO.File]::AppendAllText("$pgData\postgresql.conf",@"

listen_addresses = '127.0.0.1'
port = $DbPort
shared_preload_libraries = 'timescaledb'
shared_buffers = '128MB'
logging_collector = on
log_directory = 'log'
"@)
    }
    Write-HostConfiguration $Release $instance
    try {
        Set-ServiceVersion $Release
        Start-Service $dbService
        $env:PGPASSWORD = $instance.DbPassword
        try {
            $postgres = @('-h','127.0.0.1','-p',"$DbPort",'-U','postgres','-X','-v','ON_ERROR_STOP=1')
            $role = & "$pgBin\psql.exe" @postgres -Atq -d postgres -c "select 1 from pg_roles where rolname='scadivo'"
            if ($LASTEXITCODE) { throw 'Cannot access local PostgreSQL.' }
            if ($role -ne '1') {
                "CREATE ROLE scadivo LOGIN PASSWORD '$($instance.ApiPassword)';" | & "$pgBin\psql.exe" @postgres -q -d postgres
                if ($LASTEXITCODE) { throw 'Cannot provision SCADIVO database identity.' }
            }
            $database = & "$pgBin\psql.exe" @postgres -Atq -d postgres -c "select 1 from pg_database where datname='scadivo'"
            if ($LASTEXITCODE) { throw 'Cannot verify SCADIVO database.' }
            if ($database -ne '1') { Invoke-Checked "$pgBin\psql.exe" ($postgres + @('-q','-d','postgres','-c','CREATE DATABASE scadivo OWNER scadivo')) }
            Invoke-Checked "$pgBin\psql.exe" ($postgres + @('-q','-d','scadivo','-c','CREATE EXTENSION IF NOT EXISTS timescaledb'))
        } finally { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
        Start-Service $apiService
        Wait-Http $ApiPort '/health'
        Wait-Http $WebPort '/'
    } catch {
        $original = $_
        Stop-ManagedService $apiService
        Stop-ManagedService $dbService
        if ($previous -and $previous -ne $Release -and (Test-Path -LiteralPath (Join-Path $previous 'host\ScadivoService.exe'))) {
            Write-HostConfiguration $previous $instance
            Set-ServiceVersion $previous
            Start-Service $dbService
            Start-Service $apiService
        }
        throw $original
    }
    $instance.ActiveRelease = $Release
    Save-Json $settings $instance
    $key = New-Item -Path 'HKLM:\SOFTWARE\SCADIVO\Windows' -Force
    foreach ($item in 'WebPort','ApiPort','DbPort') {
        New-ItemProperty -LiteralPath $key.PSPath -Name $item -Value $instance.$item -PropertyType DWord -Force | Out-Null
    }
    exit 0
} catch {
    $summary = $_.Exception.Message
    if ([IO.Directory]::Exists($logs)) { Add-Content -LiteralPath "$logs\installer.log" -Value "$(Get-Date -Format s) $Action $summary" }
    Write-Error $summary -ErrorAction Continue
    exit 1
}
