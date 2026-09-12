[CmdletBinding()]
param()

# Run explicitly on the receiving computer. Does not start a service or alter the firewall.
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$version = '2026.9.1'
$assetName = 'cloudflared-windows-amd64.exe'
$installRoot = Join-Path $env:LOCALAPPDATA 'RemoteImageBridge'
$targetPath = Join-Path $installRoot 'cloudflared.exe'
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
$temporaryPath = Join-Path $installRoot ('cloudflared-download-' + [Guid]::NewGuid().ToString('N') + '.tmp')

try {
    $release = Invoke-RestMethod -Uri ('https://api.github.com/repos/cloudflare/cloudflared/releases/tags/' + $version) -Headers @{ 'User-Agent' = 'RemoteImageBridge-installer' }
    $assets = @($release.assets | Where-Object { $_.name -eq $assetName })
    if ($assets.Count -ne 1 -or $assets[0].digest -notmatch '^sha256:[0-9a-fA-F]{64}$') { throw 'Official release is missing its expected SHA-256 digest.' }
    $downloadUrl = 'https://github.com/cloudflare/cloudflared/releases/download/' + $version + '/' + $assetName
    if ($assets[0].browser_download_url -ne $downloadUrl) { throw 'Unexpected release download URL.' }
    Invoke-WebRequest -UseBasicParsing -Uri $downloadUrl -OutFile $temporaryPath
    $expectedHash = $assets[0].digest.Substring(7)
    if ((Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Download checksum mismatch.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $temporaryPath
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Cloudflare') { throw 'Cloudflare Windows signature verification failed.' }
    Move-Item -LiteralPath $temporaryPath -Destination $targetPath -Force
    Write-Output ('Verified cloudflared ' + $version + ' installed at ' + $targetPath)
} finally {
    if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
}
