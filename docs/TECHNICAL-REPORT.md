# Technical report — v0.2.3

## Base and mechanism

The supplied `Helium-charlo-fork-v0.2.2-test(1).zip` is the exact source base. Its Navigator source matched the earlier v0.2.2 source by SHA-256. The KKCD Grid and Details templates and converter configuration are unchanged. The approved build was prepared locally before this repository publication.

Playnite Desktop's `ShowGameSideBarCommand` accepts a `GamesCollectionViewEntry`, sets `GridViewSideBarVisible = true`, and selects its game. The theme's grid `i` button uses this command. `Views/Library.xaml` already binds the Navigator host's `Tag` to this command and its `DataContext` to `MainViewModel.SelectedGame`. The extension executes that same command with the selected entry. It adds no second overview. The existing view switch adapters are retained.

## Changed files

- `source/HeliumHybrid/Navigator.cs`: routes sidebar and cover clicks independently. Sidebar selection stays immediate while navigation waits for the double-click interval and cancels on a second click. A request from full Details View switches to Grid before opening the panel. `panelRequested` temporarily reveals the existing overview host. Its native visibility is checked during the 1.5-second layout refresh; a two-second grace allows opening to finish.
- `source/HeliumHybrid/Settings.cs`, `Plugin.cs`: new selector fields. Legacy XML booleans deserialize and migrate (true → Full Details View, false → Grid Details Panel), then are omitted from new saves. Cancel restores selector values. Persistent IDs and settings location are unchanged.
- `theme/theme.yaml`, `source/HeliumHybrid/extension.yaml`, `install/extension.yaml`, `source/Build.ps1`: version metadata and package names. `theme/Views/Library.xaml` already contains the command bridge and is unchanged.
- `tests/validate_package.py` and documentation: migration and package checks, installation and acceptance guidance.

## Validation and limits

Roslyn compiled an optimized .NET Framework 4.6.2 DLL against PlayniteSDK 6.11.0. XML/XAML parsed; manifests, embedded resource and package contents were checked. Standalone logic tests exercise search, grouping, widths and a large synthetic library. Static checks inspect migration and navigation wiring. They cannot execute WPF or Playnite commands.

The selected-entry binding, temporary panel close behavior, converter interaction, context menus and double-click launches require Windows testing. Restoration after closing a temporarily opened panel may take up to the 1.5-second layout refresh interval. A failed native panel request may leave the host visible until the opening grace expires. The Windows checklist is the acceptance gate.

## Maintainer acceptance

The maintainer subsequently installed and validated the delivered v0.2.3 build in Playnite Desktop on Windows. This is a user-reported runtime acceptance, separate from the Linux compilation and static checks above.
