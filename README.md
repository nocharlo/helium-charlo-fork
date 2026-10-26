# Helium (charlo fork)

**A Steam-inspired library Navigator for Playnite Desktop.**

A custom [Playnite](https://github.com/JosefNemec/Playnite) theme and companion extension based on [Helium](https://github.com/darklinkpower/Helium) and the [Helium KKCD fork](https://github.com/kushagr-m/Helium).

It brings a compact, searchable game list alongside Playnite's native Grid and Details views, with synchronized selection, configurable navigation, and a layout inspired by the modern Steam Library.

**Latest stable release: v0.2.5 — Architecture Foundation**

[**Download v0.2.5**](https://github.com/nocharlo/helium-charlo-fork/releases/tag/v0.2.5) · [Changelog](docs/CHANGELOG.md) · [Report an issue](https://github.com/nocharlo/helium-charlo-fork/issues)

**Upcoming local v0.2.7 candidate:** Recent Activity groups the Navigator by play history. Hybrid summarizes previous years; Detailed keeps monthly groups. Neither v0.2.6 nor v0.2.7 has been published.

---

## About

I've been using Playnite for a while, and one thing I've always missed from Steam is its library navigation.

I like having a compact list of games on the left while the rest of the window displays my library or the details of whichever game I've selected. Playnite already has both Grid and Details views, but switching between them always felt a little inconvenient to me.

I wanted to use both at the same time.

After looking around for themes and extensions, I couldn't find anything that worked quite the way I imagined. So I decided to experiment with AI-generated code, using the Helium KKCD fork I already had installed.

Somehow, it worked.

The result is **Helium (charlo fork)**, a modified Helium theme with a custom game Navigator integrated into Playnite's native library views.

I'm sharing it here in case anyone else has been looking for something similar, and hopefully to make further development a little easier.

## Features

### Steam-style game Navigator

- A compact, scrollable game list alongside the main library.
- Search within the Navigator without changing the main library's search results.
- Support for Playnite's active global filters.
- Expandable game groupings, including platforms, series, categories, and Recent Activity.
- Game icons and synchronized selection.
- Game counts, adjustable width, and a collapsible sidebar.
- Persistent Navigator preferences, including Hybrid or Detailed Recent Activity grouping.

The Navigator reads from Playnite's existing library. It doesn't maintain a separate game database or modify your game metadata.

### Native library integration

Rather than implementing a completely separate library interface, Helium works with Playnite's existing views.

- **Grid View:** Browse game covers while keeping the Navigator visible.
- **Grid Details Panel:** Display information for the selected game alongside the grid.
- **Full Details View:** Open the native details view without losing the Navigator.
- **List View:** Continue using Playnite's native list without a redundant Navigator.

Selection stays synchronized between the Navigator and Playnite.

### Configurable navigation

You can configure sidebar clicks and grid-cover clicks independently.

For example, my preferred setup is:

- **Sidebar click:** Open Grid Details Panel.
- **Grid cover click:** Open Full Details View.

When you're already in Full Details View, selecting another game from the Navigator updates the displayed game without forcing you back to Grid View.

You can also return to Grid View while preserving the relevant library state.

That sounds obvious, but getting it to behave consistently took a surprising number of iterations.

## Current Release

### v0.2.5 — Architecture Foundation

This update focuses on improving the Navigator's internal architecture, making future development easier and more reliable.

**Highlights:**
- Refactored and modularized the Navigator's code.
- Improved synchronization with Playnite's global filters.
- Improved search, sorting, and grouping logic.
- Added automated tests and safer build and rollback procedures.
- Preserved existing features, settings, and visual behavior.

See the [full changelog](docs/CHANGELOG.md) for technical details.

### Previous Releases

**v0.2.4 — Stability Hotfix**

- Fixed unwanted returns to Grid when selecting games in Full Details View.
- Improved navigation consistency and view transitions.
- Added safeguards for mouse interactions.
- Improved settings backup and recovery.
- Fixed keyboard context-menu targeting.

**v0.2.3 — Navigation Improvements**

- Introduced independent click-action settings.
- Improved Grid/Details navigation and selection handling.
- Refined layout behavior, collapse, and scroll restoration.

**v0.1.0 — Initial Release**

The first working proof of concept, introducing the searchable sidebar, synchronized selection, grouping, and integration with the Helium library layout.

See the [full changelog](docs/CHANGELOG.md) for additional development history.

## Installation

You'll need **Playnite 10 Desktop on Windows**.

Download both components from the latest release:

- **[Helium (charlo fork) v0.2.5 — Theme (.pthm)](https://github.com/nocharlo/helium-charlo-fork/releases/download/v0.2.5/Helium-charlo-fork-v0.2.5.pthm)**
- **[Helium Navigator v0.2.5 — Extension (.pext)](https://github.com/nocharlo/helium-charlo-fork/releases/download/v0.2.5/Helium-Navigator-v0.2.5.pext)**

### Setup

1. Back up your current Playnite theme, Navigator extension, and settings.
2. Install the `.pthm` theme package.
3. Install the `.pext` Navigator extension.
4. Restart Playnite.
5. Select **Helium (charlo fork) v0.2.5** in your Desktop theme settings.
6. Open the Navigator extension settings to customize its behavior.

**Always use matching theme and extension versions.**

If you're updating from v0.2.4, the existing configuration and component IDs are preserved. Keeping a backup is still recommended.

### Manual Installation

If necessary, the packages can be extracted into Playnite's Desktop theme and extension directories.

**Themes:** `%AppData%\Playnite\Themes\Desktop`

**Extensions:** `%AppData%\Playnite\Extensions`

Restart Playnite after installation.

If something goes wrong, reinstall your previous working version and restore your backed-up settings.

Previous versions are available on the [Releases page](https://github.com/nocharlo/helium-charlo-fork/releases).

## Extension Compatibility

This project was developed around my existing Helium setup, which includes:

- [Details to Grid View Converter](https://playnite.link/addons.html#ThemesDetailsViewToGridViewConverter_ef8a7226-eedc-478d-a506-92ee6c088aa3)
- [Extra Metadata Loader](https://playnite.link/addons.html#ExtraMetadataLoader_705fdbca-e1fc-4004-b839-1d040b8b4429)
- [ThemeModifier](https://github.com/Lacro59/playnite-thememodifier-plugin)
- [ThemeExtras](https://github.com/felixkmh/ThemeExtras-for-Playnite)
- [LibraryManagement](https://github.com/Lacro59/playnite-librarymanagement-plugin)
- [GameActivity](https://github.com/Lacro59/playnite-gameactivity-plugin)
- [HowLongToBeat](https://github.com/Lacro59/playnite-howlongtobeat-plugin)
- [Game Relations](https://github.com/darklinkpower/PlayniteExtensionsCollection/wiki/Game-Relations)
- [SuccessStory](https://github.com/Lacro59/playnite-successstory-plugin)
- [ScreenshotsVisualizer](https://github.com/Lacro59/playnite-screenshotsvisualizer-plugin)

Many of these integrations were already supported by the original Helium theme or the KKCD fork.

My modifications primarily concern the Navigator and its interaction with Playnite's native library.

**Details to Grid View Converter** is particularly important for the intended Grid Details Panel experience.

Not every extension combination has been tested, so compatibility may vary.

## Development

Helium (charlo fork) consists of two main components.

### Theme — XAML/WPF

A modified Helium KKCD theme that hosts the Navigator alongside Playnite's native library views.

The main integration points include:

- `theme/Views/Library.xaml`
- `theme/Views/LibraryGridView.xaml`
- `theme/Views/LibraryDetailsView.xaml`

The native Grid and Details templates remain important parts of the implementation.

### Navigator Extension — C\#

A companion Playnite extension responsible for the game list, selection synchronization, search, grouping, navigation, and settings.

The v0.2.5 architecture separates its responsibilities across:

| Component                         | Responsibility                                     |
| --------------------------------- | -------------------------------------------------- |
| `Navigator.cs`                    | WPF interaction, navigation, layout, and lifecycle |
| `NavigatorProjection.cs`          | Search, sorting, grouping, and game counts         |
| `NavigatorPresentation.cs`        | UI rows and icon caching                           |
| `LibraryStateAdapter.cs`          | Read-only Playnite library and view-state access   |
| `Logic.cs`                        | Shared logic helpers                               |
| `Settings.cs` / `SettingsFile.cs` | Configuration and persistence                      |

The project targets **.NET Framework 4.6.2**, **C# 7.3**, and **PlayniteSDK 6.11.0**.

### Building and Testing

The project includes a PowerShell test harness supporting:

- Release builds and paired package generation.
- Package and manifest validation.
- Isolated portable Playnite testing.
- Verified deployment, backups, and rollback.
- Collection of local test evidence.

From the repository root, a build can be started with:

```powershell
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$build = Join-Path $env:TEMP 'helium-build'

.\scripts\Helium-TestHarness.ps1 -Action Build -OutputRoot $build -DotnetExe $dotnet
```

Choose an unused output directory for each fresh build. The required SDK and dependencies must be available.

Automated test projects include:

```powershell
dotnet run --project tests/LogicTests.csproj
dotnet run --project tests/NavigatorProjectionTests.csproj
dotnet run --project tests/SettingsFileTests.csproj
```

For package validation:

```powershell
python tests/validate_package.py --build-dir $build
```

These checks do not replace testing in Playnite on Windows. WPF bindings, mouse interactions, and native view transitions require runtime validation.

The repository also contains historical v0.2.4 installers under `packages/`. For **v0.2.5**, use the verified files attached to its GitHub release rather than those older packages.

### Technical Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Navigator Architecture Foundation](docs/NAVIGATOR-ARCHITECTURE.md)
- [v0.2.5 Validation](docs/VALIDATION-v0.2.5.md)
- [Changelog](docs/CHANGELOG.md)
- [Technical Report](docs/TECHNICAL-REPORT.md)
- [Windows Testing](docs/WINDOWS-TESTING.md)

## A Note About Vibe Coding

**This project is entirely vibe-coded.**

I have no programming background, and I'm not going to pretend otherwise.

The custom implementation was generated with AI tools, mostly ChatGPT and Codex, based on my ideas, requirements, feedback, and testing.

My involvement has been figuring out what I wanted, explaining it, testing the results, identifying bugs, rejecting changes that didn't work, and guiding the next iteration.

I'm aware that AI-generated code can be inefficient, difficult to maintain, or contain problems I don't have the knowledge to identify.

There's also a broader discussion worth having about the environmental costs of AI and the additional review work it can create for open-source developers.

I take those concerns seriously, even if I'm still experimenting with the technology.

At the same time, I think there's something interesting about being able to build software for a very specific personal need, even without the skills normally required to implement it.

Without AI, this project probably wouldn't exist. It would just be another feature I wished someone else would make.

That doesn't mean the code is perfect. It means I finally got to try the idea.

### To Developers

If you're familiar with Playnite development and find this interesting, I'd genuinely appreciate your help.

Code reviews, bug fixes, refactoring, performance improvements, and alternative implementations are all welcome.

**I have no attachment to the AI-generated code.** If there's a cleaner or more reliable way to implement this functionality, I'd be happy to see it.

I'm particularly interested in better integration with Playnite's native library views, filtering, selection, and input handling.

I can help with testing, interface decisions, requirements, documentation, and feedback. My programming knowledge, unfortunately, remains nonexistent.

If someone eventually develops a better implementation from scratch, I'd be glad to see that happen too.

For now, I'm treating this as a working personal project that other people can inspect, improve, or learn from.

## What's Next?

With the Navigator functional and the first architecture refactor completed, I'd like to continue improving its foundations before making larger visual changes.

The next development priorities include:

- Separating selection and navigation responsibilities into smaller components.
- Making Grid and Details transitions more reliable.
- Improving how the Navigator communicates filtering, grouping, and game status.
- Refining the layout and controls without sacrificing native Playnite behavior.
- Gradually bringing the experience closer to the **modern Steam Library**.

I'd eventually like to explore things such as recently played games, collection shelves, and a more customizable library interface.

But I'd rather build those on a reliable foundation than keep adding features faster than I can test them.

These are development goals, not promised release features or dates.

## Known Issues and Limitations

As of **v0.2.5**:

- Uncommon mouse press-and-release interactions across different Navigator entries may still behave unexpectedly.
- Certain drag-and-release cases inherited from earlier versions have not been fully resolved or retested.
- The Navigator, cover grid, and details panel can feel cramped on narrow windows.
- Some layout behavior depends on Details to Grid View Converter.
- Broad compatibility across different Playnite setups and extension combinations remains unverified.
- Game launching was not part of the documented v0.2.5 architecture regression run.

The v0.2.5 tests confirmed settings persistence across a full restart, but they do not establish that every configuration or interaction is error-free.

Additional issues may exist. Feel free to report them.

## Contributing

Issues, testing feedback, and pull requests are welcome.

For bug reports, please include:

- Playnite version.
- Theme and Navigator version.
- Relevant installed extensions.
- Steps to reproduce the problem.
- Screenshots or logs, when useful.

**Please remove personal paths, account information, tokens, and other private details before sharing screenshots or logs.**

If you're contributing code, explain what changed and how it was tested.

I may need help reviewing technical contributions, so patience is appreciated.

## Credits

This project builds on the work of the Playnite community.

**[Josef Nemec](https://github.com/JosefNemec)** — Creator of Playnite.

**[Brandon Balderas (darklinkpower)](https://github.com/darklinkpower/Helium)** — Original Helium theme and several Playnite extensions.

**[Kush Mittal (KKCD)](https://github.com/kushagr-m/Helium)** — Helium KKCD fork, used as the foundation for this project.

**Playnite extension developers** — For the integrations and tools that make this setup possible.

**charlo** — Project concept, design direction, feature requirements, testing, and AI-assisted development coordination.

The custom implementation was produced with AI assistance. The original developers are not responsible for modifications introduced by this fork.

Special thanks to the original developers for making their work available to the community.

## License

Helium (charlo fork) is based on Helium by Brandon Balderas and the KKCD fork by Kush Mittal.

The inherited theme code is distributed under the **MIT License**.

The original copyright notices are preserved in the repository's [license](license) file.

Third-party components retain their applicable licenses and attribution requirements.

## Disclaimer

This is an unofficial, experimental community fork with no affiliation to Playnite, Valve, Steam, or the original Helium developers.

I've tested it on my own Playnite installation, but I can't guarantee that everything will work correctly on yours.

Please keep a backup of your working configuration before installing updates or experimental builds.

---

*Made by someone who spends an unreasonable amount of time organizing their game library instead of actually playing it.*
