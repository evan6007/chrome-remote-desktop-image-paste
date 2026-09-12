[CmdletBinding()]
param()

# Inspect exactly the files staged/tracked by Git, including newly staged files.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$files = @(& git -C $repoRoot -c core.quotepath=false ls-files)
if ($LASTEXITCODE -ne 0 -or $files.Count -eq 0) { throw 'No tracked files to check.' }
foreach ($file in $files) {
    if ($file -match '(^|/)(\.private|dist|artifacts|bin|obj)/|\.(exe|dll|pdb|log|bak)$|(^|/)(Pair|EndpointConfig[^/]*)\.cs$|LocalConfig\.cs$') {
        throw ('Private/generated file in public tree: ' + $file)
    }
    if ($file -match '\.png$') {
        $bytes = [IO.File]::ReadAllBytes((Join-Path $repoRoot $file))
        if ($bytes.Length -lt 8 -or [BitConverter]::ToString($bytes, 0, 8) -ne '89-50-4E-47-0D-0A-1A-0A') {
            throw ('Invalid PNG documentation asset: ' + $file)
        }
        continue
    }
    $content = [IO.File]::ReadAllText((Join-Path $repoRoot $file))
    if ($content -match '(?i)(?:const|readonly)\s+string\s+Key\s*=\s*"[0-9a-f]{32,}"') { throw ('Embedded pairing key in ' + $file) }
    if ($content -match 'https://[a-z0-9]+(?:-[a-z0-9]+){3,}\.trycloudflare\.com') { throw ('Live Quick Tunnel address in ' + $file) }
    if ($content -match '(?i)[A-Z]:[\\/]Users[\\/][^\s"''<>]+') { throw ('Personal Windows path in ' + $file) }
}
Write-Output ('PASS public-tree scan: ' + $files.Count + ' tracked source/documentation files')
