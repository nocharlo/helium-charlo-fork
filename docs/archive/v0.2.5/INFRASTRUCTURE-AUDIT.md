# Infrastructure audit before Navigator refactoring

Date: 2026-10-24. Branch: `v0.2.5-architecture-foundation`. Baseline: `e94fc44b127ae802e93541e98a3932aeb2daa7ce` (approved v0.2.4). This audit precedes any functional source change.

| Claim | Evidence | Status | Remaining check |
| --- | --- | --- | --- |
| Existing harness compiled and packaged paired v0.2.4 artifacts | Prior `outputs/helium-test-evidence/build.json`, build/test results in `TEST-RESULTS.md` | Locally tested in prior run | Repeat after each source change |
| Backup, sandbox deployment, restore and settings hash verification passed | Prior `TEST-RESULTS.md`; backup and rollback folders under isolated sandbox `test-runs` | Locally tested in prior run | Repeat after each candidate |
| Visual baseline passed startup, selection, searches, filters, view switches and preference restart | Ten prior screenshots and `TEST-RESULTS.md` | Windows Playnite executed on 10.63 | Repeat relevant cases after each candidate |
| Clearing the path isolates the runtime plugin list | `Initialize` now empties `DevelExtenions`; `Verify` rejects nonempty entries; fixture initialization passed; new Computer Use startup and fresh log segment showed only `Helium Navigator v0.2.4` | Locally tested and Windows Playnite executed | Recheck every fresh sandbox |
| Shell `Start` failed in this executor | Prior `playnite.log` has `Computer.GetGpuVendors` `UnauthorizedAccessException`; a read-only `Get-CimInstance Win32_VideoController` in this shell also failed | Locally observed environment limit | Test `Start` in a normal interactive PowerShell session if needed |
| Computer Use can launch the same portable executable | Exact sandbox `Playnite.DesktopApp.exe` launched through `sky.launch_app`; screenshot and log show Helium and sandbox library | Windows Playnite executed | Repeat after each candidate |

The sandbox's current config has only two absolute paths: its own `DatabasePath` and `AutoBackupDir`. The external development-extension entry is empty. The main Playnite config and extension folder were only read. The sandbox still contains a copied personal library, so it remains outside Git.

The shell `Start` action now waits for both a sandbox database-open entry and an application-start entry in newly written `playnite.log` bytes, or fails. This guards against reporting a process ID as successful startup. It still does not prove the UI rendered; Computer Use must capture and inspect the target window.

## Working launch sequence

1. Fully exit the main Playnite with **Playnite > Exit**. Window X may only hide it in the tray. Confirm no Playnite process or global `PlayniteInstaceMutex` remains.
2. Run `Verify` against the exact sandbox root. It rejects external development extensions, wrong IDs, duplicate Navigator installations and a database path outside the sandbox.
3. Launch `$sandbox\Playnite.DesktopApp.exe` through Computer Use `sky.launch_app`; select the returned window whose process path exactly matches the sandbox executable. No shell or Windows Run UI automation is used.
4. Capture the window state and screenshot. Inspect `playnite.log` for the sandbox library path and only intended plugin loading. The startup screenshot and fresh log segment for this audit are in the local task evidence folder.
5. Perform regression actions and capture each observed outcome. Use `Save-TestEvidence.ps1` to save screenshots, hashes and logs.
6. Exit with **Playnite > Exit** or Alt+Q and confirm the process/mutex ended before deployment, rollback or opening the main instance.

Do not infer successful GUI startup from shell output alone. This exact limitation is local to the current Codex shell; no claim is made about a normal Windows PowerShell session.
