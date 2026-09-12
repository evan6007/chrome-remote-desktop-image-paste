[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Receiver', 'Sender')]
    [string] $Action,
    [switch] $BuildOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ($Action -eq 'Receiver') {
    Write-Output 'STEP 1/3: Building your private receiver and checking the image protocol...'
    & (Join-Path $PSScriptRoot 'Build.ps1') -Role Receiver -SelfTest
    if ($BuildOnly) {
        Write-Output 'Build-only validation finished; nothing was installed or started.'
        exit 0
    }
    Write-Output 'STEP 2/3: Downloading and verifying the official Cloudflare tunnel component...'
    & (Join-Path $PSScriptRoot 'Install-Tunnel.ps1')
    Write-Output 'STEP 3/3: Starting your receiver installer...'
    $installerPath = Join-Path $repoRoot 'dist\RemoteImageBridge-Receiver.exe'
    $installer = Start-Process -FilePath $installerPath -ArgumentList '--install' -WindowStyle Hidden -PassThru
    if (-not $installer.WaitForExit(20000)) {
        throw 'Installer is still running. Inspect the receiver status window before retrying.'
    }
    if ($installer.ExitCode -ne 0) {
        throw 'Receiver installer failed. See %LOCALAPPDATA%\RemoteImageBridge\startup-error.txt.'
    }
    Write-Output ''
    Write-Output 'The receiver window should now be open. This message does not confirm network connectivity.'
    Write-Output 'Wait for the receiver window to show that the connection is verified.'
    Write-Output 'Then double-click 2-Create-Sender.cmd in this SAME project folder.'
} else {
    if ($BuildOnly) {
        & (Join-Path $PSScriptRoot 'Build.ps1') -Role Sender -Endpoint 'https://example-tunnel.trycloudflare.com' -SelfTest
        Write-Output 'Build-only validation finished; the placeholder Sender is not connected to a real receiver.'
        exit 0
    }
    Write-Output 'Creating the sender using this checkout''s private key and the running receiver URL...'
    & (Join-Path $PSScriptRoot 'Build.ps1') -Role Sender -UseRunningReceiver -SelfTest
    Write-Output ''
    Write-Output 'READY TO TRANSFER: dist\RemoteImageBridge-Sender.exe'
    Write-Output 'Download that file to your LOCAL/HOME computer using Chrome Remote Desktop file transfer.'
    Write-Output 'Double-click the Sender EXE on the LOCAL/HOME computer, not on this remote receiver.'
    Write-Output 'Only share this private EXE with your own paired computer. Share the GitHub source with friends.'
}
