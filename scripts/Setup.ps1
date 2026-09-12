[CmdletBinding()]
param([switch] $BuildOnly)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build.ps1') -SelfTest
if (-not $BuildOnly) {
    $installer = Join-Path (Split-Path -Parent $PSScriptRoot) 'dist\unsigned\ChromeRemoteDesktopImagePaste-Setup.exe'
    Start-Process -FilePath $installer -WindowStyle Hidden
}
