# Helium Hybrid 0.1.0 — Technical Report

## Scope and result

The delivered prototype includes a standalone theme derived from the supplied KKCD fork, a compiled C# extension, source code, `.pext` and `.pthm` packages, and installation instructions.

This report records the checks performed in the Linux build environment. **WPF and Playnite were not run there.** A successful DLL build does not, by itself, demonstrate that the interface renders correctly or that every integration works.

The supplied theme's `theme.yaml` identified it as **Helium (kkcd fork) 1.68.3**, with `ThemeApiVersion` 2.8.0, theme ID `b46f5ca0-be71-4398-b924-216c7c20efdd`, and credits to darklinkpower and KKCD. The archive contained 194 files, including 64 XAML files. Helium 1.71 was not used as the base.

## Architecture

The extension derives from `GenericPlugin`, registers `SourceName=HeliumHybrid` and `ElementList=[Navigator]`, and returns a `PluginUserControl` through `GetGameViewControl`. The theme places a `ContentControl` named `HeliumHybrid_Navigator` inside the `Library` control template, before the container holding the three original library views.

This integration was checked against Playnite's source code. In `Controls/Views/Library.cs` (including the **10.35** tag), Playnite calls `ControlTemplateTools.InitializePluginControls`. That initializer locates the named placeholder, requests the control from the extension, and assigns it to the placeholder. The game's data context uses `GameContext`, leaving the custom control free to manage its own layout.

### Game selection and details

- **Sidebar → Playnite:** `MainView.SelectGame(Guid)`.
- **Playnite → sidebar:** `OnGameSelected`, with additional synchronization through `GameContext` and the refresh cycle.
- Synchronization guards stop a UI update from triggering another selection in Playnite.
- The sidebar reads `MainView.FilteredGames`, so it does not attempt to navigate to games excluded by the global library filters.
- `SelectGame` alone does not guarantee that a closed details panel will open. The theme exposes `ShowGameSideBarCommand` in the host's `Tag` and `SelectedGame` in its `DataContext`. After selecting a game, the extension finds its host and runs that `ICommand` with the parameter supplied by the theme. This is the same command used by the native information button. It does not use reflection, private fields, or fabricated internal list entries.
- That last adapter depends on command names in the Playnite 10 theme and requires runtime testing. The SDK does not provide a dedicated method for opening this panel.

### Sidebar and performance

The sidebar uses a WPF `ListBox` with container recycling and virtualization enabled, including for grouped lists. Whether virtualization is effective with the grouping template still needs to be measured on Windows.

Game icons are decoded when their rows appear. Display width is limited to 32 pixels, and up to 512 paths are cached. The extension does not download images. Missing, remote, or unreadable icons leave the row without an icon.

Grouping uses the existing series, categories, or platforms. Games belonging to multiple groups appear once in each relevant group, while the counter shows the number of distinct games. These groups do not create any new metadata hierarchy. Expanded/collapsed group state is not saved and may reset when the list is rebuilt.

Local search is case-insensitive and accent-insensitive, with a 180 ms delay to avoid rebuilding the list on every keystroke. It does not change Playnite's native filter.

The SDK has no dedicated event for every possible filter change, so a background-priority `DispatcherTimer` checks the filtered set every **1.5 seconds**. If IDs and metadata have not changed, the sidebar rows are not rebuilt. Changes to games, series, categories, and platforms also invalidate the list. The timer and event subscriptions are cleaned up on `Unloaded`.

These checks and rebuilds run on the UI dispatcher. Very large libraries may still experience pauses. The standalone logic benchmark does not rule out that possibility.

The sidebar's draggable `Thumb` adjusts its width from **180 to 480 WPF logical pixels**, starting at **250 px**. When collapsed, the control still occupies **36 px**. Width, grouping, and collapsed state are saved exclusively to `navigator.xml` in the extension's data folder. Search text and scroll position are not persisted.

The native grid retains multi-selection, sorting, and its full context menu. The sidebar uses single selection, a small **Play** context menu, Enter, and double-click. It does not replicate the complete native menu, which is not exposed by the API used here.

## Files changed from the KKCD base

| File | Change |
|---|---|
| `theme.yaml` | Renamed the theme to Helium Hybrid (KKCD), set version 0.1.0, assigned a separate ID, explicitly set Desktop mode, and preserved credits. |
| `Views/Library.xaml` | Added an extension placeholder before the original library views, along with bindings for the details-opening adapter. |
| `themeExtras.yaml` | Updated `ThemeId` to the standalone theme variant. |

The other **191 theme files are byte-for-byte identical** to the supplied KKCD archive. In particular, `LibraryGridView.xaml`, `LibraryDetailsView.xaml`, `GridViewGameOverview.xaml`, `themeGridConverter.yaml`, `thememodifier.yaml`, and the media resources were left unchanged.

The previous ID in `themeExtras.yaml` pointed to the original Helium theme. It was replaced with the variant's ID so banner resources resolve against the installed variant. This still requires runtime verification with ThemeExtras.

**Theme ID:** `3a68d1fd-945b-42c2-8272-8a939339c362`  
**Extension ID:** `HeliumHybrid_e9e0f96a-b087-4f96-a157-710e6b7da49c`

## Compatibility and Details to Grid View Converter

The DLL references the official **PlayniteSDK 6.11.0** package and targets **.NET Framework 4.6.2**. It does not rely on APIs that are only present in newer SDK versions.

Initialization within the library container was checked against Playnite **10.35** source code; older versions were not investigated. The user's exact Playnite version was not supplied at the time of this report. Playnite 11 was not validated.

Details to Grid View Converter defines a replacement rule for the details panel's close button. Neither the files affected by that rule nor the converter's YAML were edited. The new sidebar sits in the outer library container. This reduces the likely conflict surface, but does not guarantee compatibility at runtime or protect against future changes to the converter.

Extra Metadata Loader, GameActivity, HowLongToBeat, and other integrations retain their existing placeholders. Settings saved by other extensions under a theme-specific ID are not migrated automatically. The standalone variant does not automatically inherit later KKCD theme updates.

## Validation performed in the build environment

1. Extracted the archive and confirmed it was the correct KKCD fork.
2. Inspected the library, grid, and details containers, converter settings, ThemeExtras, and ThemeModifier.
3. Checked the relevant APIs and implementation against official Playnite source code.
4. Compiled with **Roslyn 4.11**, official .NET Framework 4.6.2 references, and PlayniteSDK 6.11.0, with optimizations enabled: **zero errors and zero warnings**. The build used the compiler directly rather than Windows MSBuild/WPF tooling. The navigator's XAML is loaded as a runtime resource; it was not compiled into BAML.
5. Parsed the theme's **64 XAML files** and the new navigator XAML as XML, and checked the names of controls referenced by the code. This confirms XML syntax, not WPF type or resource resolution.
6. Ran **nine standalone logic checks** under .NET 8, covering accents, case, null values, multiple group memberships, missing groups, width limits, and a synthetic search across **50,000 games and 100,000 memberships**. Result: **PASS; 244 ms for the synthetic scenario** on that run. Timing depends on the machine and does not measure UI rendering.
7. Compared the modified theme with the base byte-for-byte, checked package contents, and verified checksums.

**Not tested in that environment:** plugin startup inside Playnite, runtime WPF parsing, rendering, focus, actual two-way selection, the details-opening command, UI performance, launch actions, or joint operation with the user's extensions. See [WINDOWS-TESTING.md](WINDOWS-TESTING.md) for the original Windows validation checklist.

### Subsequent user testing

After this report was generated, the user installed **v0.1.0** on Windows and confirmed that the sidebar, synchronized navigation, and existing grid/details interface worked in their Playnite setup. This is a successful real-world initial test, but it should not be read as completion of every checklist item or a full compatibility audit. The original build-time validation record in [VALIDATION.json](VALIDATION.json) has been preserved as generated.

## Primary references used

- Custom UI integration: https://api.playnite.link/docs/tutorials/extensions/customUiIntegration.html
- Theme extension integration: https://api.playnite.link/docs/tutorials/themes/extensionIntegration.html
- Library control initialization (10.35): https://github.com/JosefNemec/Playnite/blob/10.35/source/Playnite.DesktopApp/Controls/Views/Library.cs
- Plugin placeholder resolution: https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/ControlTemplateTools.cs
- Selection API: https://github.com/JosefNemec/Playnite/blob/master/source/Playnite.DesktopApp/Api/MainViewAPI.cs
- Selection and commands: `DesktopAppViewModel.cs` and `DesktopAppViewModel_Commands.cs` in https://github.com/JosefNemec/Playnite
- SDK version used: https://www.nuget.org/packages/PlayniteSDK/6.11.0
- KKCD fork listed by the supplied theme: https://github.com/kushagr-m/Helium

## Possible next steps after further Windows testing

Address any loading or layout problems observed on Windows before adding new features. Measure grouped-list virtualization with a real library. Where the window cannot comfortably fit all three panels, reduce the details-panel width or collapse the sidebar. For larger libraries, consider incremental updates and more targeted filter notifications without accessing private Playnite fields.

## Credits and licensing note

The base theme and its resources are credited to **darklinkpower** and **KKCD**. Their original files and source links were retained. This adaptation is independent and is not an official release by either author or by Playnite.

The supplied archive did not include a license file. This package does not assign a new license to the original theme's resources. New code and modifications were prepared for use and review within this project. Upstream licensing must be checked before redistribution.
