[CmdletBinding()]
param([string]$DotnetExe = 'dotnet')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
foreach ($project in Get-ChildItem (Join-Path $repo 'tests') -Filter '*.csproj' | Sort-Object Name) {
    & $DotnetExe build $project.FullName --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Test build failed: $($project.BaseName)" }
    [xml]$xml = Get-Content $project.FullName
    $framework = [string]$xml.Project.PropertyGroup.TargetFramework
    $output = Join-Path $repo "tests/bin/$($project.BaseName)/Release/$framework/$($project.BaseName)"
    if ($framework -eq 'net462') { & "$output.exe" }
    else { & $DotnetExe "$output.dll" }
    if ($LASTEXITCODE -ne 0) { throw "Test execution failed: $($project.BaseName)" }
}
python (Join-Path $repo 'tests/ArtifactOpsecTests.py')
if ($LASTEXITCODE -ne 0) { throw 'Artifact scanner tests failed' }
python (Join-Path $repo 'tests/ReleaseProvenanceTests.py')
if ($LASTEXITCODE -ne 0) { throw 'Release provenance tests failed' }
