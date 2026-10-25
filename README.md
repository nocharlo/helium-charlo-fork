# Helium (charlo fork)

A Steam-style game Navigator integrated into [Playnite](https://github.com/JosefNemec/Playnite) Desktop's native library views. This project modifies Brandon Balderas's [Helium](https://github.com/darklinkpower/Helium) through the Helium KKCD fork by Kush Mittal. **v0.2.4 remains the approved published stability release.** v0.2.5 Architecture Foundation is a locally validated, unpublished candidate. The checked-in installers remain the v0.2.4 packages; the v0.2.5 packages were built separately from the local branch.

**Latest version: v0.2.4 — Stability Hotfix**

## About

I've been using Playnite for a while, and one thing I've always missed from Steam is its library navigation.

I like having a compact list of games on the left while the rest of the window displays my library or the details of whichever game I've selected. Playnite already has both Grid and Details views, but switching between them always felt a little inconvenient to me.

I wanted to use both at the same time.

After looking around for themes and extensions, I couldn't find anything that worked quite the way I imagined. So I decided to experiment with AI-generated code, using the Helium KKCD fork I already had installed.

Somehow, it worked.

The result is **Helium (charlo fork)**, a modified Helium theme with a custom game Navigator integrated into Playnite's native library views.

I'm sharing it here in case anyone else has been looking for something similar, and hopefully to make further development a little easier.

## Features

For isolated Windows deployment, backup, rollback and visual regression evidence, use the [test harness](docs/TEST-HARNESS.md). The [v0.2.5 validation record](docs/VALIDATION-v0.2.5.md) lists the exact local build and test scope. The harness prepares a separate portable Playnite copy and keeps the stable v0.2.4 installation untouched. The [infrastructure audit](docs/INFRASTRUCTURE-AUDIT.md) records the external development-extension path found in the first sandbox and the working Computer Use launch procedure.

The checked-in v0.2.4 packages remain the approved release. `source/Build.ps1` is the legacy v0.2.4 packaging command and refuses to overwrite those paths while source manifests identify a v0.2.5 candidate. For v0.2.5 development, use `scripts/Helium-TestHarness.ps1 -Action Build` with a new output directory, then run `python tests/validate_package.py --build-dir <output directory>`. The project targets .NET Framework 4.6.2 and PlayniteSDK 6.11.0. `dotnet run --project tests/LogicTests.csproj` and `dotnet run --project tests/SettingsFileTests.csproj` exercise portable logic and real settings-file recovery.

The included v0.2.4 DLL was compiled in Release with Roslyn against .NET Framework reference assemblies and PlayniteSDK 6.11.0. Earlier build reports recorded static XML/XAML and package checks, 13 logic checks and a settings recovery test. The maintainer installed v0.2.4 in Playnite on Windows and reported 12 passed checks, two unresolved mouse-interaction failures, and one pending preference-persistence check. A later isolated Playnite run confirmed that the Navigator grouping preference survived a full exit and restart; the earlier maintainer checklist remains a historical record. See [technical validation](docs/TECHNICAL-REPORT.md) and the [change log](docs/CHANGELOG.md).

The Navigator works alongside Playnite's existing library rather than maintaining a separate game database.

The native List View also remains available without an additional Navigator.

### How navigation works

You can configure sidebar clicks and grid-cover clicks independently.

For example, my preferred setup is:

- **Sidebar click:** Open Grid Details Panel.
- **Grid cover click:** Open Full Details View.

When you're already in Full Details View, selecting another game from the Navigator updates the displayed game without forcing you back to Grid View.

That sounds obvious, but getting it to behave consistently took a surprising number of iterations.

## Current Status

### v0.2.4 — Stability Hotfix

The latest update focuses on making the existing Navigator more reliable rather than adding new features.

Changes include:

- Fixed unwanted returns to Grid when selecting games in Full Details View.
- Improved navigation consistency and view transitions.
- Added safeguards for certain mouse interactions.
- Improved settings backup and recovery.
- Fixed keyboard context-menu targeting.
- Preserved existing Helium integrations and customization.

I installed and tested v0.2.4 on my own Playnite setup. The main navigation features are working as intended.

There are still some uncommon mouse interaction issues, documented below, and broader compatibility testing remains limited.

### v0.2.3 — Navigation Improvements

Introduced independent click-action settings for sidebar games and grid covers, improved navigation between library views, and refined layout and scroll restoration.

### v0.1.0 — Initial Release

The first working proof of concept.

It introduced the searchable game sidebar, synchronized selection, basic grouping, and integration with Helium's grid and details layout.

It had plenty of rough edges, but it proved the idea could work.

## Installation

You'll need **Playnite 10 Desktop** on Windows.

Download both components:

- **[Helium (charlo fork) v0.2.4 — Theme](packages/Helium-charlo-fork-v0.2.4.pthm)**
- **[Helium Navigator v0.2.4 — Extension](packages/Helium-Navigator-v0.2.4.pext)**

1. Back up your current theme, Navigator extension, and settings.
2. Install the `.pthm` theme.
3. Install the `.pext` Navigator extension.
4. Restart Playnite.
5. Select **Helium (charlo fork) v0.2.4** in your Desktop theme settings.
6. Open the Navigator extension settings to configure your preferred navigation behavior.

Make sure the theme and extension are from the same version.

### Manual Installation

Playnite stores Desktop themes and extensions in these directories:

**Themes:** `%AppData%\Playnite\Themes\Desktop`

**Extensions:** `%AppData%\Playnite\Extensions`

Extract the respective packages into their appropriate folders and restart Playnite.

If you encounter problems, you can reinstall your previous working version and restore your backed-up Navigator settings.

The Navigator does not modify your game metadata.

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

My changes focus primarily on the Navigator and its interaction with Playnite's library. I haven't attempted to replace features these extensions already provide.

**Details to Grid View Converter** is particularly important for the intended Grid Details Panel experience.

I haven't tested every possible extension combination, so your experience may vary depending on your setup.

## Development

The project consists of two main components.

**Theme (XAML/WPF)**

A modified Helium KKCD theme that integrates the Navigator alongside Playnite's existing library views.

**Navigator Extension (C#)**

A companion Playnite extension handling the game list, selection synchronization, search, grouping, navigation, and settings.

The project targets **.NET Framework 4.6.2** and **PlayniteSDK 6.11.0**.

Source code, build scripts, and technical documentation are available in the repository:

- [Architecture](docs/ARCHITECTURE.md)
- [Changelog](docs/CHANGELOG.md)
- [Technical Report](docs/TECHNICAL-REPORT.md)
- [Windows Testing](docs/WINDOWS-TESTING.md)

### A Note About Vibe Coding

**This project is entirely vibe-coded.**

I have no programming background, and I'm not going to pretend otherwise.

The custom implementation was generated with AI tools, mostly ChatGPT, based on my ideas, requirements, feedback, and testing.

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

I'm particularly interested in better integration with Playnite's native library views, filtering, and selection system.

I can help with testing, interface decisions, requirements, documentation, and feedback. My programming knowledge, unfortunately, remains nonexistent.

If someone eventually develops a better implementation from scratch, I'd be glad to see that happen too.

For now, I'm treating this as a working personal project and a proof of concept that other people can inspect, improve, or learn from.

## What's Next?

Now that the core navigation works reasonably well, I'd like to focus on smaller quality-of-life improvements before attempting more ambitious changes.

A few things I'm interested in:

- Visually distinguishing installed and uninstalled games, like Steam does.
- Better synchronization with Playnite's global filters and saved filter presets.
- Improving search and grouping interactions.
- Refining the Navigator's appearance and usability.
- Eventually bringing the theme closer to the **modern Steam Library** interface.

Longer-term ideas include improved collection navigation and a Steam-style library Home, but those are not immediate development priorities.

I'd rather improve the features I already have than keep adding new ones faster than I can test them.

## Contributing

Feel free to open an issue or submit a pull request.

For bug reports, please include:

- Playnite version.
- Theme and Navigator version.
- Relevant installed extensions.
- Steps to reproduce the issue.
- Screenshots or logs, if available.

If you're submitting code, please explain what changed and how it was tested.

I may need help reviewing technical contributions, so patience is appreciated.

## Known Issues

As of **v0.2.4**:

- Pressing the mouse on one Navigator entry and releasing over another may sometimes leave the list in an unintended interaction state.
- Certain drag-and-release interactions can cause similar behavior.
- Narrow windows may feel cramped when displaying the Navigator, grid, and details panel together.
- Some layout behavior depends on Details to Grid View Converter.
- Compatibility with other installations and extension combinations has not been extensively tested.

A full restart-persistence acceptance check for v0.2.4 is also still pending.

Additional issues may exist. Feel free to report them.

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
