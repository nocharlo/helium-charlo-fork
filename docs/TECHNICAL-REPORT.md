# Technical report — v0.2.4 stability hotfix

## Baseline and audit

The clean local checkout is exactly `d95b0246a924d5ded53346d499a8137f9b6b2e41`, the approved v0.2.3 commit. At checkout, remote `main` pointed to that commit. The separate experimental background v0.2.4 was not used. No remote writes were made. The requested Playnite Development Specialist skill was unavailable; official Playnite SDK documentation and Playnite Desktop source were checked for `IMainViewAPI.SelectGame`, plugin custom controls, user-data paths, and native view/sidebar commands.

**A — fixed confirmed defects:** `Navigator.cs` deliberately switched Full Details back to Grid in `OpenGridPanel`. The delayed sidebar action now runs only in Grid; a Details selection still uses the existing `SelectGame` call. The old Details-to-Grid recursion was private and reachable only through that guarded action, so it was removed. Sidebar mouse-up previously had no corresponding row or drag validation. It now requires a matching mouse-down row, movement within Windows thresholds, no modifiers and no interactive child. `Plugin.cs` previously swallowed settings deserialization errors; a later save could overwrite unreadable bytes. `SettingsFile.cs` logs the error, preserves a unique `.corrupt-*` copy, and blocks saves if backup fails. Successful saves use a flushed temporary file and `File.Replace` with a `.bak` of the prior file.

**B — small correction:** keyboard context menu invocation previously used `Mouse.DirectlyOver`, which can point at another game. It now targets the selected row for keyboard invocation; right-click still targets the pointer row. The inherited Portuguese resize tooltip was translated to English.

**C — deferred:** group expansion resets when a grouped collection is rebuilt after a game or metadata refresh. Persisting group state involves the generated `GroupItem` containers and virtualization. The 1.5-second filtered-game comparison, icon cache, and visual-tree searches had no measured regression here. The temporary `panelRequested` override and the three panel visibility modes remain unchanged; Always hidden allows an explicit panel request, then restores its default after native close and the next refresh. This wording describes the actual behavior rather than a strict permanent hide.

## Changed files

- `source/HeliumHybrid/Navigator.cs`: context guard, drag validation, stale timer cancellation, keyboard menu target.
- `source/HeliumHybrid/Plugin.cs` and new `SettingsFile.cs`: recovery, logging and failure-resistant saves.
- `source/HeliumHybrid/Navigator.xaml`: English resize tooltip.
- `theme/theme.yaml`, extension manifests, `source/Build.ps1`: display, assembly and package version 0.2.4. Persistent GUIDs unchanged.
- `tests/SettingsFileTests.cs`, `tests/validate_package.py`: recovery behavior and package checks; the existing logic test remains.
- `README.md`, `docs/ARCHITECTURE.md`, `docs/WINDOWS-TESTING.md`, `docs/TECHNICAL-REPORT.md`, `docs/CHANGELOG.md`: navigation contract, audit and installation.
- `install/HeliumCharlo.dll`, `packages/*.pext`, `packages/*.pthm`: rebuilt from this checkout.

The theme view templates, Grid Details background, ThemeModifier values, converter settings and other extension integration resources are unchanged from the approved commit.

## Validation and limitations

After this report was written, the maintainer tested the paired v0.2.4 packages in Playnite on Windows and accepted them for normal use: 12 checks passed, 2 mouse-release interactions failed, and preference persistence after a full restart remains untested. The mouse failures are not claimed fixed. The source and package verification in this report predates that acceptance test.

The extension compiled as an optimized .NET Framework 4.6.2 DLL against PlayniteSDK 6.11.0. Standalone settings tests exercised missing, valid and corrupt XML, original-byte backup, save and reload. Existing logic tests cover search, groups and layout. XAML/XML parsing, manifests and package integrity were checked. Static checks verify the native command bridge and persistent IDs. Linux cannot run Playnite Desktop or WPF interactions, so mouse, keyboard, view transitions, converter and installed extension combinations require the maintainer's Windows checklist. The normal save uses `File.Replace`, supported by the target Windows filesystem; storage errors are logged and leave the original file intact whenever replacement fails.

## Install and rollback

Back up the installed v0.2.3 theme, Navigator extension and its plugin user-data `navigator.xml`. Close Playnite; install both v0.2.4 packages and restart. The stable IDs update the same add-ons and user-data path. Check that no second Navigator is enabled. To roll back, close Playnite, reinstall the backed-up v0.2.3 packages and restore the settings backup if exact preferences are required. The extension does not modify game metadata.
