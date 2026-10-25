[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$SandboxRoot,
 [Parameter(Mandatory)][string]$EvidenceRoot,
 [Parameter(Mandatory)][string]$TestName,
 [Parameter(Mandatory)][ValidateSet('PASS','FAIL','BLOCKED')][string]$Result,
 [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Observation,
 [string[]]$ScreenshotPath=@()
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$test=[IO.Path]::GetFullPath($SandboxRoot)
$evidence=[IO.Path]::GetFullPath($EvidenceRoot)
$verify=& (Join-Path $PSScriptRoot 'Helium-TestHarness.ps1') -Action Verify -SandboxRoot $test
if($evidence.Equals($test,[StringComparison]::OrdinalIgnoreCase) -or $evidence.StartsWith($test+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must be outside the sandbox'}
$marker=Get-Content -LiteralPath (Join-Path $test '.helium-test-sandbox.json') -Raw|ConvertFrom-Json
$production=[IO.Path]::GetFullPath($marker.productionRoot)
if($evidence.Equals($production,[StringComparison]::OrdinalIgnoreCase) -or $evidence.StartsWith($production+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must be outside production Playnite'}
New-Item -ItemType Directory -Path $evidence -Force|Out-Null
$stamp=[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$slug=($TestName -replace '[^A-Za-z0-9_-]','-').Trim('-')
if(-not $slug){throw 'TestName must contain a letter or number'}
$run=Join-Path $evidence ($stamp+'-'+$slug)
if(Test-Path -LiteralPath $run){throw "Evidence directory exists: $run"}
New-Item -ItemType Directory -Path $run|Out-Null
$images=@()
foreach($path in $ScreenshotPath){
 $file=Get-Item -LiteralPath $path -ErrorAction Stop
 if($file.Extension -notin @('.png','.jpg','.jpeg')){throw "Screenshot file type not supported: $path"}
 $target=Join-Path $run $file.Name
 if(Test-Path -LiteralPath $target){throw "Duplicate screenshot filename: $($file.Name)"}
 Copy-Item -LiteralPath $file.FullName -Destination $target
 $images+=@{file=$file.Name;sha256=(Get-FileHash -LiteralPath $target).Hash}
}
$logs=@()
foreach($name in @('playnite.log','debug.log','cef.log')){
 $source=Join-Path $test $name
 if(Test-Path -LiteralPath $source -PathType Leaf){
  $target=Join-Path $run $name
  Copy-Item -LiteralPath $source -Destination $target
  $logs+=@{file=$name;sha256=(Get-FileHash -LiteralPath $target).Hash}
 }
}
$record=[ordered]@{test=$TestName;result=$Result;observation=$Observation;capturedUtc=[DateTime]::UtcNow.ToString('o');sandbox=$test;verification=$verify;screenshots=$images;logs=$logs}
[IO.File]::WriteAllText((Join-Path $run 'result.json'),(ConvertTo-Json -InputObject $record -Depth 20),[Text.UTF8Encoding]::new($false))
"Saved test evidence: $run"
