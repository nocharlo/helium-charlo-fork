# Isolated Playnite test harness

This infrastructure prepares the next release without changing Navigator behavior. The source manifests and IDs remain paired and are versioned only for an approved release candidate. The harness is Windows PowerShell 7 code and uses a portable Playnite copy in a separate directory. Run it from the repository root.

## Safety boundary

- The production Playnite directory is read only during `Initialize`. Never pass it as `SandboxRoot`.
- A copied configuration once referenced an external development extension outside the sandbox. Initialize now clears DevelExtenions and Verify rejects nonempty entries. A fresh Computer Use launch showed only Helium Navigator loaded in the log.
- `Initialize` requires Playnite to be fully exited, a new destination, disjoint paths, and the known v0.2.4 IDs. It copies the library, the active Helium theme, one Navigator extension, and Navigator's `navigator.xml`. It does not copy third party extensions. It clears external development-extension paths (`DevelExtenions` in Playnite config) and copied startup/shutdown scripts. It verifies copied file hashes.
- `Deploy` and `Restore` require the sandbox marker and an exited Playnite. They only replace the exact theme and extension ID directories inside that sandbox. They keep a verified backup and a quarantine directory. No broad wildcard removal is used.
- Playnite has one application mutex for all portable instances. Exit the main instance using Playnite > Exit before starting the sandbox, then exit the sandbox before reopening the main instance. Closing the window with X can leave Playnite running in the system tray.
- Keep `work/helium-playnite-test3` and `work/helium-build-v024-check3` outside the repository. Do not put personal library copies in Git.

## Paths used in this run

The following example uses paths relative to the current Windows user profile. Adjust them to match your local environment.

```powershell
$production = Join-Path $env:USERPROFILE 'Documents\Playnite'
$sandbox = Join-Path $env:USERPROFILE 'HeliumDev\playnite-test'
$build = Join-Path $env:USERPROFILE 'HeliumDev\build'
$tool = '.\scripts\Helium-TestHarness.ps1'
```

Keep the sandbox and build directories outside the repository. Never use your production Playnite directory as `SandboxRoot`.

To create another sandbox, choose a **new** folder and run:

```powershell
& $tool -Action Initialize -ProductionRoot $production -SandboxRoot $newSandbox
& $tool -Action Verify -SandboxRoot $newSandbox
```

For a fresh build, choose a **new** output folder and use a .NET SDK. The C# project targets .NET Framework 4.6.2 and compiles against the reference assemblies in `source/HeliumHybrid/lib`.

```powershell
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source

& $tool -Action Build -OutputRoot $newBuild -DotnetExe $dotnet
```

The script stops when dependency restore, compilation, ZIP validation, manifest pairing, or packaged DLL verification fails. It writes `build.json` (including source commit, dirty-worktree flag, and package hashes) and paired `.pext`/`.pthm` packages only to the build output. For offline work, pass `-NugetPackagesRoot <existing cache>` and `-NugetConfig <config with packageSources cleared>`; all required packages must already be cached. An installation requires both package paths:

```powershell
& $tool -Action Deploy -SandboxRoot $sandbox -ThemePackage "$newBuild\packages\Helium-charlo-fork-v0.2.4.pthm" -ExtensionPackage "$newBuild\packages\Helium-Navigator-v0.2.4.pext"
& $tool -Action Verify -SandboxRoot $sandbox
```

`Deploy` prints the exact backup path. Save that path. If validation fails after installation, exit Playnite and restore:

```powershell
& $tool -Action Restore -SandboxRoot $sandbox -BackupRoot '<exact backup path printed by Deploy>'
& $tool -Action Verify -SandboxRoot $sandbox
```

The previous files remain in a timestamped test run and displaced files remain in a rollback quarantine. Preserve both directories and `playnite.log` when investigating a failure.

## Start and visual testing

`& $tool -Action Start -SandboxRoot $sandbox` starts the sandbox executable with its own user data and no library update. It requires an interactive Windows desktop and an exited main Playnite. In this Codex shell, a direct launch exited because Playnite's GPU vendor query returned `UnauthorizedAccessException`. Launching the sandbox executable through Computer Use in the interactive desktop succeeded. The shell launch path is therefore **unverified for successful startup** in this environment. Check `playnite.log` and the visible UI before reporting success.

For the working Computer Use route, launch the exact executable path `$sandbox\Playnite.DesktopApp.exe` with `sky.launch_app`, select the returned window for that process path from `sky.list_apps`, and capture its state with `sky.get_window_state`. The portable config already points `DatabasePath` to the sandbox library. Confirm `playnite.log` records that library path and only the intended plugin. Save a screenshot of the visible Helium UI. Exit through Playnite > Exit (or Alt+Q); X can leave the process in the tray. Confirm the process or global mutex has ended before deployment or opening the main Playnite.

Use Computer Use against the window whose executable path exactly matches `$sandbox\Playnite.DesktopApp.exe`. Save screenshots after each observed result. Never infer UI success from a process ID alone. The tests below were executed on the v0.2.4 sandbox; see `outputs/helium-test-evidence/TEST-RESULTS.md` in the task workspace for evidence.

After observing a check, save its screenshot from Computer Use and record the result, screenshot hash, and current Playnite logs with scripts/Save-TestEvidence.ps1. For example:

```powershell
& .\scripts\Save-TestEvidence.ps1 -SandboxRoot $sandbox -EvidenceRoot '<local evidence directory>' -TestName 'navigator-selection' -Result PASS -Observation 'Navigator and grid select the same game; detail panel updated; no launch.' -ScreenshotPath '<Computer Use screenshot path>'
```

| Check | v0.2.4 sandbox result |
| --- | --- |
| Helium starts active; Navigator visible | PASS, observed |
| Collapse and expand Navigator | PASS, observed |
| Single click selects without launching | PASS, observed with a test game |
| Navigator and grid selection agree | PASS, observed |
| Detail panel updates | PASS, observed |
| Navigator local search | PASS, observed; matching test results |
| Global search/filter reflected in Navigator | PASS, observed; matching results in grid and Navigator |
| Grid and Details switch | PASS, observed both directions |
| Grouping preference survives full process exit and restart | PASS, observed with Series |
| No second Navigator installation | PASS, one exact extension ID directory and one matching manifest |

The original [the v0.2.4 Windows checklist](../v0.2.4/WINDOWS-TESTING.md) contains additional behavioral cases not covered by this architecture baseline. Do not treat those as passed. The local sandbox copies personal library metadata; keep it outside version control and do not publish screenshots or logs without review.
