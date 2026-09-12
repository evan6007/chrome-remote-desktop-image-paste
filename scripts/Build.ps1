[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Receiver', 'Sender')]
    [string] $Role,
    [string] $Endpoint = '',
    [switch] $UseRunningReceiver,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$privateRoot = Join-Path $repoRoot '.private'
$outputRoot = Join-Path $repoRoot 'dist'
$keyPath = Join-Path $privateRoot 'pair-key.txt'
New-Item -ItemType Directory -Path $privateRoot, $outputRoot -Force | Out-Null

if ($Role -eq 'Sender' -and -not (Test-Path -LiteralPath $keyPath)) {
    throw 'Build the Receiver first in this checkout. Both builds must use the same private key.'
}
if (-not (Test-Path -LiteralPath $keyPath)) {
    $randomBytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($randomBytes) } finally { $rng.Dispose() }
    $newKey = ([BitConverter]::ToString($randomBytes)).Replace('-', '').ToLowerInvariant()
    [IO.File]::WriteAllText($keyPath, $newKey, [Text.Encoding]::ASCII)
}
$pairKey = [IO.File]::ReadAllText($keyPath).Trim()
if ($pairKey -cnotmatch '^[0-9a-f]{64}$') { throw 'Invalid private key file; expected 32 random bytes encoded as lowercase hex.' }

if ($Role -eq 'Sender') {
    if ($UseRunningReceiver) {
        if ($Endpoint) { throw 'Specify either -Endpoint or -UseRunningReceiver.' }
        $endpointPath = Join-Path $env:LOCALAPPDATA 'RemoteImageBridge\endpoint.txt'
        if (-not (Test-Path -LiteralPath $endpointPath)) { throw 'Start the Receiver and wait for its connection before building the Sender.' }
        $Endpoint = [IO.File]::ReadAllText($endpointPath).Trim()
    }
    # Keep generated C# literals constrained; reject paths, credentials and query strings.
    if ($Endpoint -cnotmatch '^https://[a-z0-9-]+\.trycloudflare\.com/?$') {
        throw 'Sender requires a valid HTTPS Quick Tunnel URL. Use -UseRunningReceiver or -Endpoint.'
    }
    $Endpoint = $Endpoint.TrimEnd('/')
} elseif ($Endpoint -or $UseRunningReceiver) {
    throw 'Endpoint options apply to the Sender only.'
}

$receiverLiteral = if ($Role -eq 'Receiver') { 'true' } else { 'false' }
$generated = 'internal static class Pair { internal const string Key = "' + $pairKey + '"; internal const bool IsReceiver = ' + $receiverLiteral + '; }' + "`r`n" +
    'internal static class EndpointConfig { internal const string Default = "' + $Endpoint + '"; }' + "`r`n"
$configPath = Join-Path $privateRoot ($Role + '-LocalConfig.cs')
[IO.File]::WriteAllText($configPath, $generated, [Text.Encoding]::ASCII)

$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework C# compiler not found. A 64-bit Windows desktop with .NET Framework 4.8 is required.' }
$binaryPath = Join-Path $outputRoot ('RemoteImageBridge-' + $Role + '.exe')
$arguments = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/codepage:65001',
    '/reference:System.dll', '/reference:System.Core.dll',
    '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
    ('/out:' + $binaryPath),
    (Join-Path $repoRoot 'src\ImageBridge.cs'),
    (Join-Path $repoRoot 'src\NetworkTransport.cs'), $configPath
)
& $compilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }

if ($SelfTest) {
    $testPath = Join-Path $outputRoot ($Role + '-self-test.txt')
    $testProcess = Start-Process -FilePath $binaryPath -ArgumentList @('--self-test', ('"' + $testPath + '"')) -WindowStyle Hidden -PassThru
    if (-not $testProcess.WaitForExit(30000)) { $testProcess.Kill(); throw 'Self-test timed out.' }
    if ($testProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $testPath)) { throw 'Self-test failed.' }
    Get-Content -LiteralPath $testPath
}
Write-Output ('Built: ' + $binaryPath)
Write-Output 'PRIVATE BUILD: transfer only to your paired computer. Never attach this EXE or .private/ to a public release.'
