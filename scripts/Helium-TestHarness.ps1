[CmdletBinding()]
param(
 [Parameter(Mandatory)][ValidateSet('Initialize','Build','Deploy','Start','Restore','Verify')][string]$Action,
 [string]$ProductionRoot, [string]$SandboxRoot, [string]$OutputRoot,
 [string]$ThemePackage, [string]$ExtensionPackage, [string]$BackupRoot, [string]$DotnetExe
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repo=Split-Path $PSScriptRoot -Parent
$themeId='e5d3a4ba-1cc7-42b1-a090-8d4d3a922b23'
$extensionId='HeliumCharlo_b59e3c9f-9c52-4582-a332-0716b56cf023'
$dataId='b59e3c9f-9c52-4582-a332-0716b56cf023'
function Full([string]$p){[IO.Path]::GetFullPath($p).TrimEnd([char[]]@('\','/'))}
function NoReparse([string]$path){
 $p=Full $path
 while($p){
  if(Test-Path -LiteralPath $p){
   if(((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Reparse point in protected path: $p"}
  }
  $parent=Split-Path $p -Parent;if(-not $parent -or $parent -eq $p){break};$p=$parent
 }
}
function Guard([string]$a,[string]$b){
 $a=Full $a;$b=Full $b
 if($a.Equals($b,[StringComparison]::OrdinalIgnoreCase) -or $a.StartsWith($b + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or $b.StartsWith($a + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Overlapping paths: $a ; $b"}
 foreach($path in @($a,$b)){NoReparse $path}
}
function Field([string]$file,[string]$key){
 if(-not(Test-Path -LiteralPath $file -PathType Leaf)){throw "Missing manifest: $file"}
 $m=[regex]::Match([IO.File]::ReadAllText($file),"(?m)^$([regex]::Escape($key)):\s*(.+?)\s*$")
 if(-not $m.Success){throw "Missing $key in $file"};$m.Groups[1].Value.Trim()
}
function Pair([string]$theme,[string]$extension){
 if((Field $theme 'Id') -ne $themeId -or (Field $extension 'Id') -ne $extensionId){throw 'Unexpected Helium IDs'}
 $v=Field $theme 'Version';if($v -ne (Field $extension 'Version')){throw 'Theme/extension versions differ'}
 if((Field $extension 'Module') -ne 'HeliumCharlo.dll'){throw 'Unexpected extension DLL'}
 $v
}
function Closed{
 $p=@(Get-Process -Name Playnite.DesktopApp,Playnite.FullscreenApp -ErrorAction SilentlyContinue)
 if($p.Count){throw "Close Playnite before this operation; running PID(s): $($p.Id -join ',')"}
 try{$mutex=[Threading.Mutex]::OpenExisting('PlayniteInstaceMutex')}
 catch{
  if($_.Exception.InnerException -is [Threading.WaitHandleCannotBeOpenedException]){return}
  throw "Cannot prove Playnite is closed: $($_.Exception.Message)"
 }
 if($mutex){$mutex.Dispose();throw 'Playnite is running; close the main instance before testing.'}
}
function Sandbox{
 if(-not $SandboxRoot){throw 'SandboxRoot required'}
 $root=Full $SandboxRoot;$marker=Join-Path $root '.helium-test-sandbox.json'
 if(-not(Test-Path -LiteralPath $marker -PathType Leaf)){throw "Not a test sandbox: $root"}
 $id=Get-Content -LiteralPath $marker -Raw|ConvertFrom-Json
 if($id.kind -ne 'HeliumPlayniteTestSandboxV1' -or -not $root.Equals((Full $id.sandboxRoot),[StringComparison]::OrdinalIgnoreCase)){throw 'Sandbox marker mismatch'}
 Guard $root $id.productionRoot
 if(-not(Test-Path -LiteralPath (Join-Path $root 'Playnite.DesktopApp.exe') -PathType Leaf)){throw 'Missing sandbox executable'}
 $root
}
function CopyChecked([string]$from,[string]$to){
 if(-not(Test-Path -LiteralPath $from -PathType Container) -or (Test-Path -LiteralPath $to)){throw "Unsafe copy: $from -> $to"}
 Copy-Item -LiteralPath $from -Destination $to -Recurse -Force
 $prefix=(Full $from).Length+1
 foreach($f in (Get-ChildItem -LiteralPath $from -File -Recurse -Force)){
  $copy=Join-Path $to $f.FullName.Substring($prefix)
  if(-not(Test-Path -LiteralPath $copy -PathType Leaf) -or (Get-FileHash -LiteralPath $f.FullName).Hash -ne (Get-FileHash -LiteralPath $copy).Hash){throw "Copy verification failed: $($f.FullName)"}
 }
}
function Unique([string]$root){
 $t=@(Get-ChildItem -LiteralPath (Join-Path $root 'Themes\Desktop') -Filter theme.yaml -File -Recurse|Where-Object{(Field $_.FullName 'Id') -eq $themeId})
 $e=@(Get-ChildItem -LiteralPath (Join-Path $root 'Extensions') -Filter extension.yaml -File -Recurse|Where-Object{(Field $_.FullName 'Id') -eq $extensionId})
 if($t.Count -ne 1 -or $e.Count -ne 1){throw "Expected exactly one theme/extension: $($t.Count)/$($e.Count)"}
 Pair $t[0].FullName $e[0].FullName
}
function ExpandChecked([string]$file,[string]$to){
 if(-not(Test-Path -LiteralPath $file -PathType Leaf) -or (Test-Path -LiteralPath $to)){throw "Missing package or existing stage: $file"}
 $z=[IO.Compression.ZipFile]::OpenRead($file)
 try{
  if(-not $z.Entries.Count){throw 'Empty package'}
  $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  foreach($entry in $z.Entries){
   $name=$entry.FullName.Replace('\','/')
   if($name.StartsWith('/') -or $name.Contains(':') -or @($name.Split('/')) -contains '..' -or -not $names.Add($name)){throw "Unsafe ZIP entry: $name"}
   $stream=$entry.Open();try{$stream.CopyTo([IO.Stream]::Null)}finally{$stream.Dispose()}
  }
 }finally{$z.Dispose()}
 [IO.Compression.ZipFile]::ExtractToDirectory($file,$to)
}
function Json([string]$path,$value){[IO.File]::WriteAllText($path,(ConvertTo-Json -InputObject $value -Depth 100),[Text.UTF8Encoding]::new($false))}
switch($Action){
 'Initialize'{
  if(-not $ProductionRoot -or -not $SandboxRoot){throw 'ProductionRoot and SandboxRoot required'}
  $main=Full $ProductionRoot;$test=Full $SandboxRoot
  Guard $main $test;Guard $repo $test;Closed
  if(Test-Path -LiteralPath $test){throw "Sandbox destination already exists: $test"}
  foreach($name in @('Playnite.DesktopApp.exe','config.json','library','Themes\Desktop','Extensions','ExtensionsData')){
   if(-not(Test-Path -LiteralPath (Join-Path $main $name))){throw "Missing production component: $name"}
  }
  $mainTheme=Join-Path $main "Themes\Desktop\$themeId";$mainExt=Join-Path $main "Extensions\$extensionId"
  $version=Pair (Join-Path $mainTheme 'theme.yaml') (Join-Path $mainExt 'extension.yaml')
  New-Item -ItemType Directory -Path $test|Out-Null
  foreach($f in (Get-ChildItem -LiteralPath $main -File -Force)){
   if($f.Name -in @('config.json','backup.json') -or $f.Name -match '\.log$|\.tmp$|\.log\.\d+$'){continue}
   Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $test $f.Name)
  }
  foreach($name in @('Resources','Templates','Localization','locales','JITProfiles')){
   $from=Join-Path $main $name;if(Test-Path -LiteralPath $from -PathType Container){CopyChecked $from (Join-Path $test $name)}
  }
  New-Item -ItemType Directory -Path (Join-Path $test 'Themes\Desktop'),(Join-Path $test 'Extensions'),(Join-Path $test 'ExtensionsData'),(Join-Path $test 'Backup') -Force|Out-Null
  $default=Join-Path $main 'Themes\Desktop\Default'
  if(Test-Path -LiteralPath $default -PathType Container){CopyChecked $default (Join-Path $test 'Themes\Desktop\Default')}
  CopyChecked $mainTheme (Join-Path $test "Themes\Desktop\$themeId")
  CopyChecked $mainExt (Join-Path $test "Extensions\$extensionId")
  CopyChecked (Join-Path $main 'library') (Join-Path $test 'library')
  $settings=Join-Path $main "ExtensionsData\$dataId\navigator.xml"
  if(Test-Path -LiteralPath $settings -PathType Leaf){
   $dest=Join-Path $test "ExtensionsData\$dataId";New-Item -ItemType Directory -Path $dest|Out-Null
   Copy-Item -LiteralPath $settings -Destination (Join-Path $dest 'navigator.xml')
   if((Get-FileHash $settings).Hash -ne (Get-FileHash (Join-Path $dest 'navigator.xml')).Hash){throw 'Settings copy verification failed'}
  }
  $cfg=Get-Content -LiteralPath (Join-Path $main 'config.json') -Raw|ConvertFrom-Json
  $cfg.DatabasePath=Join-Path $test 'library';$cfg.Theme=$themeId
  foreach($key in @('CheckForProgramUpdates','CheckForAddonUpdates','CheckForLibraryUpdates','CheckForEmulatedLibraryUpdates','AutoBackupEnabled','StartOnBoot')){
   if($cfg.PSObject.Properties[$key]){$cfg.$key=$false}
  }
  foreach($key in @('AppStartupScript','AppShutdownScript','GameStartedScript','GameStoppedScript','PreScript','PostScript')){if($cfg.PSObject.Properties[$key]){$cfg.$key=''}}
  if($cfg.PSObject.Properties['DevelExtenions']){$cfg.DevelExtenions=@()}
  if($cfg.PSObject.Properties['AutoBackupDir']){$cfg.AutoBackupDir=Join-Path $test 'Backup'}
  Json (Join-Path $test 'config.json') $cfg
  Json (Join-Path $test '.helium-test-sandbox.json') ([ordered]@{kind='HeliumPlayniteTestSandboxV1';sandboxRoot=$test;productionRoot=$main;sourceVersion=$version;createdUtc=[DateTime]::UtcNow.ToString('o')})
  $null=Unique $test
  "Initialized isolated Playnite: $test ; Helium $version"
 }
 'Verify'{
  $test=Sandbox;$version=Unique $test
  $cfg=Get-Content -LiteralPath (Join-Path $test 'config.json') -Raw|ConvertFrom-Json
  if(-not(Full $cfg.DatabasePath).Equals((Full (Join-Path $test 'library')),[StringComparison]::OrdinalIgnoreCase) -or $cfg.Theme -ne $themeId){throw 'Sandbox config points outside test library or theme'}
  if($cfg.PSObject.Properties['DevelExtenions'] -and @($cfg.DevelExtenions).Count -ne 0){throw 'Sandbox contains external development extensions'}
  "PASS: paths, IDs, one Navigator, config and version $version"
 }
 'Build'{
  if(-not $OutputRoot){throw 'OutputRoot required'}
  $out=Full $OutputRoot;Guard $out $repo
  if($ProductionRoot){Guard $out $ProductionRoot}
  if(Test-Path -LiteralPath $out){throw "Output must be a new directory: $out"}
  $version=Pair (Join-Path $repo 'theme\theme.yaml') (Join-Path $repo 'source\HeliumHybrid\extension.yaml')
  $dotnet=if($DotnetExe){Get-Item -LiteralPath $DotnetExe -ErrorAction SilentlyContinue}else{$command=Get-Command dotnet -ErrorAction SilentlyContinue;if($command){Get-Item -LiteralPath $command.Source}}
  $env:DOTNET_CLI_HOME=Join-Path $out 'dotnet-home';New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME -Force|Out-Null
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1';$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
  if(-not $dotnet -or -not (& $dotnet.FullName --list-sdks)){throw '.NET SDK unavailable; no packages created'}
  New-Item -ItemType Directory -Path (Join-Path $out 'bin'),(Join-Path $out 'obj'),(Join-Path $out 'packages') -Force|Out-Null
  $env:NUGET_PACKAGES=Join-Path $out 'nuget';$env:NUGET_HTTP_CACHE_PATH=Join-Path $out 'nuget-http';$env:NUGET_SCRATCH=Join-Path $out 'nuget-scratch'
  & $dotnet.FullName build (Join-Path $repo 'source\HeliumHybrid\HeliumHybrid.csproj') --configuration Release "-p:BaseOutputPath=$out\bin\" "-p:BaseIntermediateOutputPath=$out\obj\"
  if($LASTEXITCODE -ne 0){throw "Compilation failed: $LASTEXITCODE"}
  $dlls=@(Get-ChildItem -LiteralPath (Join-Path $out 'bin') -Filter HeliumCharlo.dll -File -Recurse)
  if($dlls.Count -ne 1){throw "Expected one DLL, got $($dlls.Count)"}
  $install=Join-Path $out 'install';New-Item -ItemType Directory -Path $install|Out-Null
  Copy-Item $dlls[0].FullName (Join-Path $install 'HeliumCharlo.dll')
  Copy-Item (Join-Path $repo 'source\HeliumHybrid\extension.yaml') (Join-Path $install 'extension.yaml')
  $pext=Join-Path $out "packages\Helium-Navigator-v$version.pext"
  $pthm=Join-Path $out "packages\Helium-charlo-fork-v$version.pthm"
  [IO.Compression.ZipFile]::CreateFromDirectory($install,$pext)
  [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $repo 'theme'),$pthm)
  ExpandChecked $pext (Join-Path $out 'verify-extension');ExpandChecked $pthm (Join-Path $out 'verify-theme')
  $null=Pair (Join-Path $out 'verify-theme\theme.yaml') (Join-Path $out 'verify-extension\extension.yaml')
  if((Get-FileHash (Join-Path $out 'verify-extension\HeliumCharlo.dll')).Hash -ne (Get-FileHash $dlls[0].FullName).Hash){throw 'Packaged DLL mismatch'}
  Json (Join-Path $out 'build.json') ([ordered]@{version=$version;sourceCommit=(& git -C $repo rev-parse HEAD).Trim();themePackage=$pthm;extensionPackage=$pext;themeSha256=(Get-FileHash $pthm).Hash;extensionSha256=(Get-FileHash $pext).Hash;builtUtc=[DateTime]::UtcNow.ToString('o')})
  "PASS: compiled and packaged $version in $out"
 }
 'Deploy'{
  $test=Sandbox;if(-not $ThemePackage -or -not $ExtensionPackage){throw 'Both packages required'}
  Closed;$null=Unique $test
  $id=[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8)
  $run=Join-Path $test "test-runs\$id";New-Item -ItemType Directory -Path $run -Force|Out-Null
  $ts=Join-Path $run 'staged-theme';$es=Join-Path $run 'staged-extension'
  ExpandChecked $ThemePackage $ts;ExpandChecked $ExtensionPackage $es
  $version=Pair (Join-Path $ts 'theme.yaml') (Join-Path $es 'extension.yaml')
  if(-not(Test-Path -LiteralPath (Join-Path $es 'HeliumCharlo.dll') -PathType Leaf)){throw 'Package DLL missing'}
  $tt=Join-Path $test "Themes\Desktop\$themeId";$et=Join-Path $test "Extensions\$extensionId"
  $backup=Join-Path $run 'backup';New-Item -ItemType Directory -Path $backup|Out-Null
  CopyChecked $tt (Join-Path $backup 'theme');CopyChecked $et (Join-Path $backup 'extension')
  $settings=Join-Path $test "ExtensionsData\$dataId\navigator.xml"
  if(Test-Path -LiteralPath $settings -PathType Leaf){
   Copy-Item $settings (Join-Path $backup 'navigator.xml')
   if((Get-FileHash $settings).Hash -ne (Get-FileHash (Join-Path $backup 'navigator.xml')).Hash){throw 'Settings backup mismatch'}
  }
  Json (Join-Path $backup 'identity.json') ([ordered]@{kind='HeliumTestBackupV1';sandboxRoot=$test;previousVersion=(Unique $test);newVersion=$version;themePackageSha256=(Get-FileHash $ThemePackage).Hash;extensionPackageSha256=(Get-FileHash $ExtensionPackage).Hash})
  try{
   Move-Item -LiteralPath $tt -Destination (Join-Path $run 'previous-theme')
   Move-Item -LiteralPath $et -Destination (Join-Path $run 'previous-extension')
   Move-Item -LiteralPath $ts -Destination $tt
   Move-Item -LiteralPath $es -Destination $et
   $null=Unique $test
  }catch{
   if(Test-Path -LiteralPath (Join-Path $run 'previous-theme')){
    if(Test-Path -LiteralPath $tt){Move-Item -LiteralPath $tt -Destination (Join-Path $run 'failed-theme')}
    Move-Item -LiteralPath (Join-Path $run 'previous-theme') -Destination $tt
   }
   if(Test-Path -LiteralPath (Join-Path $run 'previous-extension')){
    if(Test-Path -LiteralPath $et){Move-Item -LiteralPath $et -Destination (Join-Path $run 'failed-extension')}
    Move-Item -LiteralPath (Join-Path $run 'previous-extension') -Destination $et
   }
   throw
  }
  "Deployed $version to sandbox only. Backup: $backup"
 }
 'Restore'{
  $test=Sandbox;if(-not $BackupRoot){throw 'BackupRoot required'};Closed
  $backup=Full $BackupRoot
  if(-not $backup.StartsWith((Join-Path $test 'test-runs') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Backup outside sandbox'}
  NoReparse $backup
  $id=Get-Content -LiteralPath (Join-Path $backup 'identity.json') -Raw|ConvertFrom-Json
  if($id.kind -ne 'HeliumTestBackupV1' -or -not(Full $id.sandboxRoot).Equals($test,[StringComparison]::OrdinalIgnoreCase)){throw 'Backup identity mismatch'}
  $null=Pair (Join-Path $backup 'theme\theme.yaml') (Join-Path $backup 'extension\extension.yaml')
  $tt=Join-Path $test "Themes\Desktop\$themeId";$et=Join-Path $test "Extensions\$extensionId"
  $settings=Join-Path $test "ExtensionsData\$dataId\navigator.xml"
  $settingsBackup=Join-Path $backup 'navigator.xml'
  $quarantine=Join-Path $test ('test-runs\rollback-'+[guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Path $quarantine|Out-Null
  if(Test-Path -LiteralPath $settings -PathType Leaf){Copy-Item -LiteralPath $settings -Destination (Join-Path $quarantine 'navigator.xml')}
  Move-Item -LiteralPath $tt -Destination (Join-Path $quarantine 'theme')
  Move-Item -LiteralPath $et -Destination (Join-Path $quarantine 'extension')
  try{
   CopyChecked (Join-Path $backup 'theme') $tt;CopyChecked (Join-Path $backup 'extension') $et
   $null=Unique $test
   if(Test-Path -LiteralPath $settingsBackup -PathType Leaf){
    Copy-Item -LiteralPath $settingsBackup -Destination $settings -Force
    if((Get-FileHash $settingsBackup).Hash -ne (Get-FileHash $settings).Hash){throw 'Restored settings hash mismatch'}
   }
  }catch{
   if(Test-Path -LiteralPath $tt){Move-Item -LiteralPath $tt -Destination (Join-Path $quarantine 'incomplete-theme')}
   if(Test-Path -LiteralPath $et){Move-Item -LiteralPath $et -Destination (Join-Path $quarantine 'incomplete-extension')}
   Move-Item -LiteralPath (Join-Path $quarantine 'theme') -Destination $tt
   Move-Item -LiteralPath (Join-Path $quarantine 'extension') -Destination $et
   $quarantinedSettings=Join-Path $quarantine 'navigator.xml'
   if(Test-Path -LiteralPath $quarantinedSettings -PathType Leaf){Copy-Item -LiteralPath $quarantinedSettings -Destination $settings -Force}
   throw
  }
  "Restored sandbox. Displaced files retained in $quarantine"
 }
 'Start'{
  $test=Sandbox;$null=Unique $test;Closed
  $exe=Join-Path $test 'Playnite.DesktopApp.exe'
  $log=Join-Path $test 'playnite.log'
  $priorLength=if(Test-Path -LiteralPath $log -PathType Leaf){(Get-Item -LiteralPath $log).Length}else{0}
  $p=Start-Process -FilePath $exe -WorkingDirectory $test -ArgumentList @('--userdatadir',('"'+$test+'"'),'--nolibupdate','--startdesktop') -PassThru -WindowStyle Normal
  $deadline=[DateTime]::UtcNow.AddSeconds(60)
  do{
   Start-Sleep -Seconds 1
   if($p.HasExited){throw "Sandbox Playnite exited with code $($p.ExitCode); inspect sandbox playnite.log"}
   if(Test-Path -LiteralPath $log -PathType Leaf){
    $bytes=[IO.File]::ReadAllBytes($log)
    $recent=if($bytes.Length -ge $priorLength){[Text.Encoding]::UTF8.GetString($bytes,[int]$priorLength,$bytes.Length-[int]$priorLength)}else{[Text.Encoding]::UTF8.GetString($bytes)}
    if($recent.Contains("GameDatabase:Opening db $(Join-Path $test 'library')") -and $recent -match 'PlayniteApplication:Application [^\r\n]+ started'){
     "Started sandbox Playnite PID $($p.Id); library opened and application startup logged. Confirm the visible UI."
     return
    }
   }
  }while([DateTime]::UtcNow -lt $deadline)
  throw 'Sandbox startup was not confirmed in playnite.log within 60 seconds; inspect the log and visible UI.'
 }
}