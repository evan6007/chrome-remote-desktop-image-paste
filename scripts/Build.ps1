[CmdletBinding()]
param([switch] $SelfTest)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $repoRoot 'dist\unsigned'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework C# compiler not found. Use 64-bit Windows with .NET Framework 4.8.' }
$binaryPath = Join-Path $outputRoot 'ChromeRemoteDesktopImagePaste-Setup.exe'
$arguments = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/codepage:65001',
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Security.dll',
    '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
    ('/win32manifest:' + (Join-Path $repoRoot 'src\app.manifest')),
    ('/out:' + $binaryPath)
) + @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
& $compilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
if ($SelfTest) {
    foreach ($test in @('self-test', 'clipboard-test', 'setup-test')) {
        $testPath = Join-Path $repoRoot ('dist\' + $test + '.txt')
        $process = Start-Process -FilePath $binaryPath -ArgumentList @(('--' + $test), ('"' + $testPath + '"')) -WindowStyle Hidden -PassThru
        if (-not $process.WaitForExit(60000)) { $process.Kill(); throw ($test + ' timed out.') }
        if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $testPath)) { throw ($test + ' failed.') }
        Get-Content -LiteralPath $testPath
    }
}
$digest = (Get-FileHash -LiteralPath $binaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $outputRoot 'SHA256SUMS.txt'), ($digest + '  ChromeRemoteDesktopImagePaste-Setup.exe' + "`n"), [Text.Encoding]::ASCII)
Write-Output ('Universal installer built: ' + $binaryPath)
Write-Output 'Contains no pairing credentials. This local build is UNSIGNED; a checksum is not a trusted publisher signature.'
