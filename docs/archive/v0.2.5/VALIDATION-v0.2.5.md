# v0.2.5 Architecture Foundation validation

## Candidate identity

- Branch: `v0.2.5-architecture-foundation`; stable baseline: `e94fc44`.
- Final functional commit: `5aa2c2b5d2b2e3a5cdf842cff3ad9e4d40664cc4`. The final `build.json` records `sourceDirty=false`.
- Theme ID: `e5d3a4ba-1cc7-42b1-a090-8d4d3a922b23`; Navigator extension ID: `HeliumCharlo_b59e3c9f-9c52-4582-a332-0716b56cf023`. Both manifests say `0.2.5`.
- Final theme package SHA-256: `B9753A1E4385832DA240B7AA88B94514EC584D4AA371595926125F4CA28BCF36`.
- Final Navigator package SHA-256: `1D15227EBF74372963A6E83B007799B7B055791D97603B38572B9513AD00F8DF`.
- Final installed DLL SHA-256: `034C5D6A750632EE5E60ECA269169649D59D0C4A2D3898E8C75A16911B918E7A`, matching the final build's DLL in the isolated portable Playnite. The Test Harness verified sandbox paths, IDs and exactly one Navigator. Backups remain in the sandbox test-runs folder.

The build output is `work/helium-v025-final-build-5aa2c2b` in the local task workspace. Copies of the two final packages, `build.json`, log and screenshots are kept in the adjacent `outputs/helium-v025-final-evidence` folder. They are local artifacts, not published packages. The tracked `packages/` files remain the approved v0.2.4 release.

## Automated checks

| Check | Result |
| --- | --- |
| Offline Release build against PlayniteSDK 6.11.0 and .NET Framework 4.6.2 | Passed, 0 warnings and 0 errors |
| `NavigatorProjectionTests` | 12 passed |
| `LogicTests` | 13 passed, including a 50,000-game scenario |
| `SettingsFileTests` | Missing, persisted, corrupt-backup, recovery and restart-reload cases passed |
| `validate_package.py` on final packages | 65 XAML/XML files, paired packages, .NET DLL and stable IDs passed |
| Final Playnite log | Plugin loaded, sandbox database opened, app started; 0 warning/error lines in the final session |

## Portable Playnite observations

The broad regression run used clean build `9db5a0e`. The installed DLL SHA-256 was `4B6967DFE39207C2A288A162336649902D7C495FB7646F3602E849C977ECEFB0`, matching that build. Screenshots `01`–`07` belong to this build:

- Navigator loaded in the portable Playnite. Selecting a test game in full Details updated its details without launching it.
- Switching to Grid and selecting a test game in Navigator highlighted its cover and updated the Grid details panel. Grid/Details transitions remained usable.
- Local search showed matching Navigator entries while the Grid retained the full library. Global Playnite search reduced both Grid and Navigator to matching games.
- Platform, Series and Category / collection groups rendered with their members.
- Category grouping saved as `Grouping=2` in the sandbox's `navigator.xml` and reappeared after a full exit and restart.

Commit `5aa2c2b` changed only the internal construction of the selected-ID set and removed an unused compatibility helper. After a clean rebuild and redeployment, screenshot `08-final-selection-5aa2c2b.jpg` confirmed that selecting a test game in Navigator again highlighted its Grid cover and updated the details panel. Its installed DLL hash matched the final build. The other GUI scenarios were not redundantly rerun after this isolated cleanup.

## Limits

No game was launched. The two uncommon mouse-release interactions already known in v0.2.4 were not exercised. Third-party integrations and the main Playnite installation were not modified or retested. The shell launch path remains limited by the environment's WMI/CIM access; launching the exact portable executable through Computer Use and closing with Playnite's exit command worked. Screenshots provide visual evidence for the named scenarios, not a general compatibility guarantee.
