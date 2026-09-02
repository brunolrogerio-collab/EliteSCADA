Set-StrictMode -Version Latest

function Test-WindowsPortableExecutable {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($stream.Length -lt 2) { return $false }
        return $stream.ReadByte() -eq 0x4D -and $stream.ReadByte() -eq 0x5A
    }
    finally {
        $stream.Dispose()
    }
}

function Get-WindowsPeSigningLayout {
    param([Parameter(Mandatory = $true)][string]$Path)

    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.LongLength -lt 0x40 -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) {
        throw "File is not a valid PE candidate: $Path"
    }

    $peOffset = [long][BitConverter]::ToUInt32($bytes, 0x3c)
    if ($peOffset -lt 0 -or $peOffset + 24 -gt $bytes.LongLength) {
        throw "PE header offset is invalid: $Path"
    }
    if ([BitConverter]::ToUInt32($bytes, [int]$peOffset) -ne 0x00004550) {
        throw "PE signature is invalid: $Path"
    }

    $optionalHeaderSize = [long][BitConverter]::ToUInt16($bytes, [int]($peOffset + 20))
    $optionalHeaderOffset = $peOffset + 24
    if ($optionalHeaderOffset + $optionalHeaderSize -gt $bytes.LongLength) {
        throw "PE optional header is truncated: $Path"
    }

    $magic = [BitConverter]::ToUInt16($bytes, [int]$optionalHeaderOffset)
    $dataDirectoryOffset = switch ($magic) {
        0x10b { $optionalHeaderOffset + 96 }
        0x20b { $optionalHeaderOffset + 112 }
        default { throw "Unsupported PE optional-header magic '$magic': $Path" }
    }

    $checksumOffset = $optionalHeaderOffset + 64
    $securityDirectoryOffset = $dataDirectoryOffset + (4 * 8)
    if ($checksumOffset + 4 -gt $optionalHeaderOffset + $optionalHeaderSize -or
        $securityDirectoryOffset + 8 -gt $optionalHeaderOffset + $optionalHeaderSize) {
        throw "PE signing fields are outside the optional header: $Path"
    }

    $certificateTableOffset = [long][BitConverter]::ToUInt32($bytes, [int]$securityDirectoryOffset)
    $certificateTableSize = [long][BitConverter]::ToUInt32($bytes, [int]($securityDirectoryOffset + 4))
    if (($certificateTableOffset -eq 0) -ne ($certificateTableSize -eq 0)) {
        throw "PE certificate-table offset/size are inconsistent: $Path"
    }
    if ($certificateTableOffset -ne 0 -and
        ($certificateTableOffset + $certificateTableSize -gt $bytes.LongLength -or $certificateTableSize -lt 8)) {
        throw "PE certificate table is outside the file: $Path"
    }

    [pscustomobject]@{
        Path = $Path
        Bytes = $bytes
        PeOffset = $peOffset
        OptionalHeaderMagic = $magic
        ChecksumOffset = $checksumOffset
        SecurityDirectoryOffset = $securityDirectoryOffset
        CertificateTableOffset = $certificateTableOffset
        CertificateTableSize = $certificateTableSize
    }
}

function Get-WindowsComparablePeDigest {
    param(
        [Parameter(Mandatory = $true)]$Layout,
        [Parameter(Mandatory = $true)][long]$ComparisonLength
    )

    if ($ComparisonLength -lt $Layout.SecurityDirectoryOffset + 8 -or
        $ComparisonLength -gt $Layout.Bytes.LongLength) {
        throw "PE comparison length is invalid for '$($Layout.Path)'."
    }

    $hash = [System.Security.Cryptography.IncrementalHash]::CreateHash(
        [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        $hash.AppendData($Layout.Bytes, 0, [int]$Layout.ChecksumOffset)

        $afterChecksum = [int]($Layout.ChecksumOffset + 4)
        $beforeSecurityLength = [int]($Layout.SecurityDirectoryOffset - $afterChecksum)
        if ($beforeSecurityLength -gt 0) {
            $hash.AppendData($Layout.Bytes, $afterChecksum, $beforeSecurityLength)
        }

        $afterSecurity = [int]($Layout.SecurityDirectoryOffset + 8)
        $remainingLength = [int]($ComparisonLength - $afterSecurity)
        if ($remainingLength -gt 0) {
            $hash.AppendData($Layout.Bytes, $afterSecurity, $remainingLength)
        }

        return [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
    }
    finally {
        $hash.Dispose()
    }
}

function Assert-WindowsAuthenticodeSigningDelta {
    param(
        [Parameter(Mandatory = $true)][string]$UnsignedPath,
        [Parameter(Mandatory = $true)][string]$SignedPath,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    $unsigned = Get-WindowsPeSigningLayout -Path $UnsignedPath
    $signed = Get-WindowsPeSigningLayout -Path $SignedPath

    if ($unsigned.CertificateTableOffset -ne 0 -or $unsigned.CertificateTableSize -ne 0) {
        throw "Unsigned candidate PE already contains an Authenticode certificate table: $RelativePath"
    }
    if ($signed.CertificateTableOffset -eq 0 -or $signed.CertificateTableSize -eq 0) {
        throw "Signed-return PE does not contain an Authenticode certificate table: $RelativePath"
    }
    if ($signed.CertificateTableOffset + $signed.CertificateTableSize -ne $signed.Bytes.LongLength) {
        throw "Signed-return PE certificate table is not the final file content: $RelativePath"
    }
    if ($signed.CertificateTableOffset -lt $unsigned.Bytes.LongLength) {
        throw "Signed-return PE changed or truncated candidate content before the certificate table: $RelativePath"
    }

    $paddingLength = $signed.CertificateTableOffset - $unsigned.Bytes.LongLength
    if ($paddingLength -gt 7) {
        throw "Signed-return PE inserted more than alignment padding before its certificate table: $RelativePath"
    }
    for ($index = $unsigned.Bytes.LongLength; $index -lt $signed.CertificateTableOffset; $index++) {
        if ($signed.Bytes[[int]$index] -ne 0) {
            throw "Signed-return PE inserted non-zero content before its certificate table: $RelativePath"
        }
    }

    if ($unsigned.PeOffset -ne $signed.PeOffset -or
        $unsigned.OptionalHeaderMagic -ne $signed.OptionalHeaderMagic -or
        $unsigned.ChecksumOffset -ne $signed.ChecksumOffset -or
        $unsigned.SecurityDirectoryOffset -ne $signed.SecurityDirectoryOffset) {
        throw "Signed-return PE changed its signing-layout structure: $RelativePath"
    }

    $unsignedDigest = Get-WindowsComparablePeDigest -Layout $unsigned -ComparisonLength $unsigned.Bytes.LongLength
    $signedDigest = Get-WindowsComparablePeDigest -Layout $signed -ComparisonLength $unsigned.Bytes.LongLength
    if ($unsignedDigest -ne $signedDigest) {
        throw "Signed-return PE differs from the candidate outside Authenticode signing fields: $RelativePath"
    }
}

function Get-WindowsRfc3161TimestampEvidence {
    param([Parameter(Mandatory = $true)][string]$Path)

    Add-Type -AssemblyName System.Security.Cryptography.Pkcs
    $rfc3161Oid = '1.2.840.113549.1.9.16.2.14'
    $layout = Get-WindowsPeSigningLayout -Path $Path
    if ($layout.CertificateTableOffset -eq 0) { return $null }

    $stream = [IO.MemoryStream]::new($layout.Bytes, $false)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        $position = [uint64]$layout.CertificateTableOffset
        $end = $position + [uint64]$layout.CertificateTableSize
        while ($position + 8 -le $end) {
            $stream.Position = [int64]$position
            $length = $reader.ReadUInt32()
            $null = $reader.ReadUInt16()
            $certificateType = $reader.ReadUInt16()
            if ($length -lt 8 -or $position + $length -gt $end) { return $null }

            if ($certificateType -eq 0x0002) {
                $content = $reader.ReadBytes([int]$length - 8)
                try {
                    $cms = [System.Security.Cryptography.Pkcs.SignedCms]::new()
                    $cms.Decode($content)
                    foreach ($signerInfo in $cms.SignerInfos) {
                        foreach ($attribute in $signerInfo.UnsignedAttributes) {
                            if ($attribute.Oid.Value -ne $rfc3161Oid) { continue }

                            foreach ($attributeValue in $attribute.Values) {
                                $timestampToken = $null
                                $bytesConsumed = 0
                                if (-not [System.Security.Cryptography.Pkcs.Rfc3161TimestampToken]::TryDecode(
                                    $attributeValue.RawData,
                                    [ref]$timestampToken,
                                    [ref]$bytesConsumed)) {
                                    continue
                                }
                                if ($bytesConsumed -ne $attributeValue.RawData.Length) { continue }

                                $timestampSigner = $null
                                if ($timestampToken.VerifySignatureForSignerInfo(
                                    $signerInfo,
                                    [ref]$timestampSigner,
                                    $cms.Certificates)) {
                                    return [pscustomobject]@{
                                        TimestampUtc = $timestampToken.TokenInfo.Timestamp.ToUniversalTime().ToString(
                                            'O',
                                            [Globalization.CultureInfo]::InvariantCulture)
                                        TokenSha256 = [Convert]::ToHexString(
                                            [System.Security.Cryptography.SHA256]::HashData($attributeValue.RawData)
                                        ).ToLowerInvariant()
                                        SignerCertificateSubject = $timestampSigner.Subject
                                        SignerCertificateThumbprint = $timestampSigner.Thumbprint.ToLowerInvariant()
                                    }
                                }
                            }
                        }
                    }
                }
                catch [System.Security.Cryptography.CryptographicException] {
                    # Continue scanning. Windows trust remains the signature-validity authority.
                }
            }

            $position += [uint64]([Math]::Ceiling([double]$length / 8.0) * 8.0)
        }

        return $null
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

function Test-WindowsRfc3161TimestampToken {
    param([Parameter(Mandatory = $true)][string]$Path)

    return $null -ne (Get-WindowsRfc3161TimestampEvidence -Path $Path)
}
