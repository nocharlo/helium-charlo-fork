# Helium (charlo fork)

A Steam-style game Navigator integrated into [Playnite](https://github.com/JosefNemec/Playnite) Desktop's native library views. This project modifies Brandon Balderas's [Helium](https://github.com/darklinkpower/Helium) through the Helium KKCD fork by Kush Mittal. The current version is **v0.2.3**, tested by the project maintainer in Playnite Desktop on Windows. The theme and companion extension display as **Helium (charlo fork) v0.2.3** and **Helium Navigator v0.2.3**.

The goal is to keep a compact, searchable list of individual games beside the existing cover grid or full game details. Helium's artwork, logos, backgrounds and supported integrations remain in place. This is an unofficial community fork; inherited theme components are credited below.

## What v0.2.3 does

- Shows a scrollable, independently scrolling game Navigator with icons, search and expandable groupings.
- Synchronizes game selection between Navigator and Playnite's grid, preserving native filtering and sorting.
- Lets sidebar games and grid covers independently open either the native Full Details View or the Grid Details Panel. The grid panel uses Playnite's existing sidebar command, also used by the cover's `i` button.
- Offers Back to Grid with selected game and, where possible, prior grid scroll position.
- Collapses the Navigator completely, with a floating expand button, adjustable width and responsive panel layout.
- Keeps the native List View free of a duplicate Navigator. Settings persist through the extension's user-data file.

The [architecture notes](docs/ARCHITECTURE.md) map the XAML host, Playnite commands, navigation state and settings migration to source files. [Windows testing](docs/WINDOWS-TESTING.md) covers the accepted behavior and additional installation checks.

## Requirements and installation

Use Playnite 10 Desktop on Windows. The theme integrates with [Details to Grid View Converter](https://playnite.link/addons.html#ThemesDetailsViewToGridViewConverter_ef8a7226-eedc-478d-a506-92ee6c088aa3); retain it for the intended grid details presentation. [Extra Metadata Loader](https://playnite.link/addons.html#ExtraMetadataLoader_705fdbca-e1fc-4004-b839-1d040b8b4429), ThemeModifier, ThemeExtras and other integrations may supply additional theme content. Their features depend on the respective extensions and your configuration. The project targets PlayniteSDK 6.11.0 and .NET Framework 4.6.2.

1. Close Playnite and back up your current working theme, extension and Navigator `navigator.xml` settings.
2. Download the [theme](packages/Helium-charlo-fork-v0.2.3.pthm) and [Navigator extension](packages/Helium-Navigator-v0.2.3.pext) from this repository. Open each installer with Playnite, then restart.
3. Select **Helium (charlo fork) v0.2.3** under Desktop theme settings. In add-on settings, open **Helium Navigator v0.2.3** and choose the sidebar and grid-cover click actions separately.
4. Keep your other theme integrations enabled. Check that only one Navigator is active; the installer does not remove older installations.

For manual installation, extract the package contents into separate folders under `%AppData%\Playnite\Themes\Desktop` and `%AppData%\Playnite\Extensions`, respectively, then restart Playnite. The `theme/` and `install/` directories contain the corresponding unpacked contents. The v0.2.3 theme and extension retain stable IDs from v0.2.2 for settings compatibility; earlier v0.1.0 code remains accessible in Git history.

To roll back, reinstall your backed-up v0.2.2 packages and restore your backed-up `navigator.xml`. The new version saves navigation choices in new fields, so the settings backup matters for exact rollback. No game metadata is changed.

## Build and checks

Run `source/Build.ps1` in PowerShell on Windows with the .NET SDK and network access for the NuGet references. It builds the C# project in Release, copies the DLL and extension manifest into `install/`, and packages the theme and extension. The project uses .NET Framework 4.6.2 and PlayniteSDK 6.11.0. Run `python tests/validate_package.py` after packaging; `tests/LogicTests.csproj` covers platform-independent search and layout logic.

The checked-in v0.2.3 DLL was compiled in Release with Roslyn against .NET Framework reference assemblies and PlayniteSDK 6.11.0. Static XML/XAML and package checks and 13 standalone logic checks passed. The maintainer installed and validated v0.2.3 in Playnite on Windows; the automated checks do not replace that runtime validation. See [technical validation](docs/TECHNICAL-REPORT.md) and the [change log](docs/CHANGELOG.md).

## Limits and development status

The layout depends on Playnite Desktop's current view templates and the converter's grid overview. Different themes, extension combinations or future Playnite changes may require adjustments. The temporary panel override can take up to the existing 1.5-second layout refresh to return to its default after closing. Broad compatibility across other installations has not been established. v0.2.3 is the current tested baseline; earlier experimental local builds are not separate releases.

No screenshot of the approved v0.2.3 is included in this repository. Screenshots may be added after confirming their source and permission to publish.

Development used substantial AI assistance for code and documentation. charlo defined requirements, made interface decisions, tested in Playnite, identified failures and guided iterations. The source is published for review, improvement and independent reimplementation; contributions should explain changes and how they were tested.

## Credits and license

- **Brandon Balderas (darklinkpower)** — original Helium theme.
- **Kush Mittal** — Helium KKCD fork used as the theme base.
- **Josef Nemec and Playnite contributors** — Playnite and its SDK.
- **Third-party extension authors** — their own integrations and resources remain theirs.
- **charlo** — concept, requirements, interface choices and Windows testing.

The inherited MIT copyright notices for Balderas and Mittal are preserved in [license](license). Existing credits and third-party notices in theme resources remain in place. This project is not affiliated with Playnite or Valve.
