# Changelog

This file documents the **Helium Hybrid 0.1.0** release. The original 0.1.0 package did not contain a changelog; this entry was prepared afterward using its README, technical report, and delivered files.

## [0.1.0] — Initial working prototype

### Added

- A scrollable game navigation sidebar alongside the existing Helium KKCD Grid View.
- Game titles and icons, local search, and alphabetical browsing.
- Grouping by series, category, or platform, using existing Playnite metadata.
- Two-way selection synchronization between the sidebar and Playnite's main library.
- An adapter to request the native game details panel when a game is selected from the sidebar.
- A collapsible, resizable sidebar (250 px initial width; 180–480 px range; 36 px when collapsed).
- Persistence for sidebar width, grouping, and collapsed state.
- Basic game launching through double-click, Enter, or the sidebar's Play menu.
- A separately packaged Playnite extension (`.pext`) and Desktop theme (`.pthm`).

### Kept from the KKCD base

- The original grid, details views, media resources, and existing extension placeholders.
- Compatibility-oriented configuration for Details to Grid View Converter, ThemeExtras, and ThemeModifier.
- Separate theme and extension identifiers so the original KKCD installation is not overwritten.

### Validation

- The extension compiled against PlayniteSDK 6.11.0 and .NET Framework 4.6.2 with zero reported compiler errors or warnings.
- XML parsing and nine standalone logic checks passed in the build environment.
- The user subsequently installed 0.1.0 on Windows and confirmed that the sidebar, selection synchronization, and existing grid/details interface worked.

### Known limitations

- The sidebar reserves 36 px even when collapsed, and can crowd the grid on smaller displays.
- Some settings associated with the original theme ID may need to be configured again.
- The complete Windows test checklist and compatibility matrix remain unverified.
- Sidebar contents are checked periodically for filter changes, which could affect very large libraries.

For technical details, see [TECHNICAL-REPORT.md](TECHNICAL-REPORT.md). For Windows checks, see [WINDOWS-TESTING.md](WINDOWS-TESTING.md).
