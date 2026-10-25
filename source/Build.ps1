$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $PSScriptRoot 'HeliumHybrid/HeliumHybrid.csproj'
$manifest = Get-Content (Join-Path $PSScriptRoot 'HeliumHybrid/extension.yaml') -Raw
if ($manifest -notmatch '(?m)^Version:\s*0\.2\.4\s*$') { throw 'Legacy Build.ps1 writes v0.2.4 release paths. Use scripts/Helium-TestHarness.ps1 -Action Build for a v0.2.5 candidate.' }
& dotnet build $project --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item (Join-Path $PSScriptRoot 'HeliumHybrid/bin/Release/net462/HeliumCharlo.dll') (Join-Path $root 'install/HeliumCharlo.dll') -Force
Copy-Item (Join-Path $PSScriptRoot 'HeliumHybrid/extension.yaml') (Join-Path $root 'install/extension.yaml') -Force
$packages = Join-Path $root 'packages'
New-Item -ItemType Directory -Force $packages | Out-Null
foreach ($pair in @(@('install', 'Helium-Navigator-v0.2.4.pext'), @('theme', 'Helium-charlo-fork-v0.2.4.pthm'))) {
    $zip = Join-Path $packages ($pair[1] + '.zip')
    Compress-Archive -Path (Join-Path $root ($pair[0] + '/*')) -DestinationPath $zip -Force
    Move-Item $zip (Join-Path $packages $pair[1]) -Force
}
Write-Host "DLL and packages updated in $root"
