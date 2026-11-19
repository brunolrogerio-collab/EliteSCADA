# Run on the backup computer as the owner of the protected recovery directory.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ConfigPath,
    [switch]$NoPopup
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$recoveryPath = Split-Path -Parent (Resolve-Path -LiteralPath $ConfigPath).Path
$statePath = Join-Path $recoveryPath 'backup-sync-status.json'
$statusTemp = Join-Path $recoveryPath ('.sync-status-' + [Guid]::NewGuid().ToString('N'))
$partialPath = $null
$now = [DateTimeOffset]::UtcNow
$lastResult = $null
$problem = 'Não foi possível ler a configuração da cópia.'

try {
    $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    if (Test-Path -LiteralPath $statePath -PathType Leaf) {
        try { $lastResult = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json }
        catch { Write-Warning 'O histórico do último alerta não pôde ser lido.' }
    }
    $port = [int]$config.Port
    if ($config.HostName -notmatch '^[a-zA-Z0-9][a-zA-Z0-9.-]*$' -or
        $config.UserName -notmatch '^[a-zA-Z_][a-zA-Z0-9_.-]*$' -or
        $port -lt 1 -or $port -gt 65535 -or
        !(Test-Path -LiteralPath $config.IdentityFile -PathType Leaf)) {
        throw 'Invalid backup SSH configuration.'
    }
    $target = "$($config.UserName)@$($config.HostName)"
    $sshOptions = @('-i', $config.IdentityFile, '-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes',
        '-o', 'ConnectTimeout=12', '-o', 'ServerAliveInterval=10', '-o', 'ServerAliveCountMax=3')
    $inventory = @'
set -eu
cd /var/backups/scadivo-portal
cat .last-backup-status
name=$(find . -maxdepth 1 -type f -name 'portal-????????T??????Z.tar.gz.enc' -size +0c -printf '%f\n' | LC_ALL=C sort | tail -n 1)
test -n "$name"
sha256sum -- "$name"
'@
    $problem = 'Não foi possível consultar o último backup no VPS.'
    $files = @(ssh.exe @sshOptions -p $port $target $inventory 2>$null)
    if ($LASTEXITCODE -ne 0 -or $files.Count -ne 2 -or $files[0] -notin @('ok', 'failed')) {
        throw 'Backup inventory unavailable.'
    }
    $match = [regex]::Match($files[1], '^([a-f0-9]{64})  (portal-(\d{8}T\d{6}Z)\.tar\.gz\.enc)$')
    if (!$match.Success) { throw 'Invalid backup inventory response.' }
    $hash = $match.Groups[1].Value
    $name = $match.Groups[2].Value
    $stamp = [DateTimeOffset]::ParseExact($match.Groups[3].Value, "yyyyMMdd'T'HHmmss'Z'",
        [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal)

    $problem = 'Falha ao copiar ou verificar o backup no computador.'
    $archives = Join-Path $recoveryPath 'archives'
    New-Item -Path $archives -ItemType Directory -Force | Out-Null
    $destination = Join-Path $archives $name
    if (!(Test-Path -LiteralPath $destination -PathType Leaf) -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash -ne $hash) {
        $partialPath = Join-Path $archives ('.partial-' + [Guid]::NewGuid().ToString('N'))
        scp.exe -q @sshOptions -P $port "${target}:/var/backups/scadivo-portal/$name" $partialPath 2>$null
        if ($LASTEXITCODE -ne 0 -or (Get-FileHash -Algorithm SHA256 -LiteralPath $partialPath).Hash -ne $hash) {
            throw 'Backup transfer did not match SHA-256.'
        }
        $problem = 'A cópia não pôde ser aberta com a chave de recuperação do computador.'
        $decryptionKey = Join-Path $recoveryPath 'portal-backup.key'
        if (!(Test-Path -LiteralPath $decryptionKey -PathType Leaf) -or
            !(Test-Path -LiteralPath $config.OpenSslPath -PathType Leaf)) {
            throw 'Backup verification tools unavailable.'
        }
        $PSNativeCommandUseErrorActionPreference = $true
        $members = @(& $config.OpenSslPath enc -d -aes-256-cbc -pbkdf2 -iter 200000 -pass "file:$decryptionKey" -in $partialPath |
            tar.exe -tzf -)
        if ('database.dump' -notin $members -or 'keys.tar.gz' -notin $members -or 'manifest.txt' -notin $members) {
            throw 'Required backup components are missing.'
        }
        [IO.File]::Move($partialPath, $destination, $true)
        $partialPath = $null
    }
    if ($files[0] -eq 'failed') {
        $problem = 'O último trabalho de backup no VPS falhou. A cópia mais recente disponível foi verificada.'
        throw 'Remote backup failed.'
    }
    if (($now - $stamp).TotalHours -gt 30) {
        $problem = 'O último backup disponível tem mais de 30 horas.'
        throw 'Remote backup is stale.'
    }

    $result = [ordered]@{ checkedAtUtc = $now.ToString('o'); status = 'ok'; archive = $name; sha256 = $hash }
    $exitCode = 0
}
catch {
    $result = [ordered]@{ checkedAtUtc = $now.ToString('o'); status = 'failed'; message = $problem }
    $exitCode = 1
}
finally {
    if ($partialPath -and (Test-Path -LiteralPath $partialPath -PathType Leaf)) {
        Remove-Item -LiteralPath $partialPath
    }
}

if ($exitCode) {
    $result.alertedAtUtc = $lastResult.alertedAtUtc
    if (!$NoPopup -and ($lastResult.message -ne $problem -or !$lastResult.alertedAtUtc -or
        ($now - [DateTimeOffset]$lastResult.alertedAtUtc).TotalHours -ge 24)) {
        try {
            $notification = New-Object -ComObject WScript.Shell
            $null = $notification.Popup($problem, 15, 'SCADIVO — Falha no backup do Portal', 48)
            $result.alertedAtUtc = $now.ToString('o')
        } catch {
            Write-Warning 'Não foi possível exibir o alerta do Windows.'
        }
    }
    Write-Warning $problem
}
$result | ConvertTo-Json -Compress | Set-Content -LiteralPath $statusTemp -Encoding utf8
[IO.File]::Move($statusTemp, $statePath, $true)
if (!$exitCode) { "Backup verificado: $name" }
exit $exitCode
