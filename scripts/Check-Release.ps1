[CmdletBinding()]
param([string] $Directory = '', [switch] $RequireSignature)
$ErrorActionPreference = 'Stop'
if (-not $Directory) { $Directory = Join-Path (Split-Path -Parent $PSScriptRoot) 'dist\unsigned' }
$exe = Join-Path $Directory 'ChromeRemoteDesktopImagePaste-Setup.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Universal installer missing.' }
$files = @(Get-ChildItem -LiteralPath $Directory -File -Recurse)
if (@($files | Where-Object { $_.Name -notin @('ChromeRemoteDesktopImagePaste-Setup.exe','SHA256SUMS.txt') }).Count) { throw 'Unexpected file in release bundle.' }
$assembly = [Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($exe))
if (-not $assembly.GetType('RuntimeSettings') -or -not $assembly.GetType('BridgeSettings')) { throw 'Legacy per-pair build cannot be published.' }
$pairType = $assembly.GetType('Pair')
$fields = @($pairType.GetFields([Reflection.BindingFlags]'Static,Public,NonPublic'))
if ($fields.Count -ne 0) { throw 'Pairing material must not be an embedded field.' }
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if ($info.ProductName -ne 'Chrome Remote Desktop Image Paste' -or $info.FileVersion -ne '0.2.0.0' -or $info.CompanyName -ne 'evan6007') { throw 'Unexpected release identity.' }
$signature = Get-AuthenticodeSignature -LiteralPath $exe
if ($RequireSignature) {
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'SignPath Foundation' -or -not $signature.TimeStamperCertificate) {
        throw 'A valid, timestamped SignPath Foundation signature is required. Unsigned/self-signed outputs cannot be released as signed.'
    }
}
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $Directory 'SHA256SUMS.txt'), ($hash + '  ChromeRemoteDesktopImagePaste-Setup.exe' + "`n"), [Text.Encoding]::ASCII)
Write-Output ('PASS universal release bundle; Authenticode=' + $signature.Status)
