# Helium (charlo fork)

A Steam-inspired library navigation sidebar for [Playnite](https://github.com/JosefNemec/Playnite) Desktop Mode, based on [Helium](https://github.com/darklinkpower/Helium) by darklinkpower and the [KKCD fork](https://github.com/kushagr-m/Helium).

## About

I've been using Playnite for a while, and one thing I've always missed from Steam is its library navigation.

I like having a compact list of games on the left while the rest of the window displays my library or the details of whichever game I've selected. Playnite already has both Grid and Details views, but switching between them always felt a little inconvenient to me.

I wanted to use both at the same time.

After looking around for themes and extensions, I couldn't find anything that worked quite the way I imagined. So I decided to experiment with AI-generated code, using the Helium KKCD fork I already had installed.

Somehow, it worked.

The result is a custom navigation sidebar that sits alongside Helium's existing grid and details layout. I'm sharing it here in case anyone else has been looking for something similar, and hopefully to make further development a little easier.

## Features

The initial release includes:

- A scrollable game sidebar with titles and icons
- Alphabetical navigation and game search
- Synchronized selection between the sidebar and Playnite's main library
- A collapsible sidebar
- Independent scrolling between the game list and grid
- Integration with Helium's existing Grid View and game details
- A separate Playnite extension handling the navigation

Selecting a game in the sidebar updates the selection in Playnite. Selecting a game from the grid also highlights it in the sidebar.

The existing Helium interface, including backgrounds, logos, artwork, and supported extensions, remains available.

## Current Status

### v0.1.0 — Initial Release

The first working version.

I've installed and tested it on my own Playnite setup, and the main functionality works as intended.

There are still some issues, particularly on smaller displays. The sidebar takes up a fair amount of space, and keeping the grid and details panel visible together can make the interface feel cramped.

Still, I'm pretty happy with how the initial experiment turned out.

### v0.2.0 — In Development

The next version focuses on improving the interface for laptops and smaller windows.

Planned changes include:

- Responsive layouts based on available window space
- A more compact, resizable sidebar
- Persistent sidebar settings
- Improved collapsing behavior across library views
- Optional Steam-style navigation to full game details
- Adaptive cover grid sizing
- Better scroll position preservation
- Performance and compatibility improvements

Early development builds have introduced some visual regressions, so v0.1.0 remains the working reference for now.

I also have some ideas for expandable game groups and additional navigation options, but those can wait until the current features are more reliable.

## Installation

You'll need **Playnite 10 Desktop** on Windows.

1. Download the latest working version from [Releases](https://github.com/nocharlo/helium-charlo-fork/releases).
2. Install the included `.pext` navigator extension.
3. Install the `.pthm` theme.
4. Restart Playnite if prompted.
5. Go to **Settings → Appearance → General** and select the installed theme.
6. Switch to Grid View.

The v0.1.0 packages still use the name **Helium Hybrid (KKCD)** internally. The new project name will be reflected in future releases.

Make sure the theme and navigator extension belong to the same compatible release.

I recommend keeping your previous working theme installed, especially when testing experimental updates.

### Manual Installation

Playnite stores Desktop themes and extensions in these directories:

**Themes:** `%AppData%\Playnite\Themes\Desktop`

**Extensions:** `%AppData%\Playnite\Extensions`

If you're installing manually, copy the extracted folders into their respective locations and restart Playnite.

## Extension Compatibility

This project was developed around my existing Helium setup, which includes:

- [Details to Grid View Converter](https://playnite.link/addons.html#ThemesDetailsViewToGridViewConverter_ef8a7226-eedc-478d-a506-92ee6c088aa3)
- [Extra Metadata Loader](https://playnite.link/addons.html#ExtraMetadataLoader_705fdbca-e1fc-4004-b839-1d040b8b4429)
- [ThemeModifier](https://playnite.link/addons.html#playnite-thememodifier-plugin)
- [ThemeExtras](https://playnite.link/addons.html#felixkmh_Extras_Plugin)
- LibraryManagement
- GameActivity
- HowLongToBeat
- Game Relations
- SuccessStory
- ScreenshotsVisualizer

Many of these integrations were already supported by the original Helium theme or the KKCD fork. My changes focus primarily on the navigation sidebar and its interaction with Playnite's library.

I haven't tested every possible combination of extensions, so your experience may vary depending on your setup.

## Development

The project consists of two main components.

**Theme (XAML/WPF)**

A modified Helium theme that integrates the custom sidebar into the existing Playnite Desktop layout.

**Navigator Extension (C#)**

A Playnite extension that handles the game list, navigation, and selection synchronization.

The source code, build files, and technical documentation are available in this repository.

### A Note About Vibe Coding

**This project is entirely vibe-coded.**

I have no programming background, and I'm not going to pretend otherwise.

The custom code was generated with AI tools, mostly ChatGPT, based on my ideas, requirements, feedback, and testing. My involvement has been figuring out what I wanted, explaining it, testing the results, and trying to fix whatever broke along the way.

I'm aware that AI-generated code can be inefficient, difficult to maintain, or contain problems I don't have the knowledge to identify. There's also a broader discussion worth having about the environmental costs of AI and the additional review work it can create for open-source developers.

I take those concerns seriously, even if I'm still experimenting with the technology.

I decided to publish the project because the feature itself might be useful to other people, and I'd like to see whether someone with actual development experience could take it further.

### To Developers

If you're familiar with Playnite development and find this interesting, I'd genuinely appreciate your help.

Code reviews, bug fixes, refactoring, performance improvements, and alternative implementations are all welcome.

I have no attachment to the AI-generated code. If there's a cleaner or more reliable way to implement this functionality, I'd be happy to see it.

I'm particularly interested in how this could be better integrated with Playnite's native library views and selection system.

I can help with testing, interface decisions, documentation, and feedback. My programming knowledge, unfortunately, remains nonexistent.

If someone eventually develops a proper implementation of this feature from scratch, I'd be glad to see that happen too.

For now, I'm treating this repository as a proof of concept that other people can inspect, improve, or learn from.

## Contributing

Feel free to open an issue or submit a pull request.

For bug reports, please include:

- Playnite version
- Theme and navigator version
- Relevant installed extensions
- Steps to reproduce the problem
- Screenshots or logs, if available

If you're submitting code, please explain what changed and how it was tested.

I may need help reviewing technical contributions, so patience is appreciated.

## Known Issues

As of v0.1.0:

- The sidebar can occupy too much space on smaller displays.
- Its collapsing behavior across different library views needs improvement.
- The grid and details panel can become cramped.
- Some layout behavior depends on Details to Grid View Converter.
- The navigator uses periodic library updates that could be optimized.
- Compatibility with other setups hasn't been thoroughly tested.

Additional issues may exist. Feel free to report them.

## Credits

This project builds on the work of the Playnite community.

**[Josef Nemec](https://github.com/JosefNemec)** — Creator of Playnite.

**[Brandon Balderas (darklinkpower)](https://github.com/darklinkpower/Helium)** — Original Helium theme and several Playnite extensions.

**[Kush Mittal (KKCD)](https://github.com/kushagr-m/Helium)** — Helium KKCD fork, used as the foundation for this project.

**Playnite extension developers** — For the various extensions and integrations that make this setup possible.

**charlo** — Project concept, design decisions, feature requirements, testing, and ongoing experiments.

The custom implementation was generated with AI assistance.

Special thanks to the original developers for making their work available to the community.

## License

Helium (charlo fork) is based on Helium by Brandon Balderas and the KKCD fork by Kush Mittal.

The inherited theme code is distributed under the **MIT License**.

The original copyright notices are preserved in the [LICENSE](LICENSE) file:

- Copyright (c) 2020– Brandon Balderas
- Copyright (c) 2025– Kush Mittal

Any third-party components retain their applicable licenses. The licensing of newly generated extension code should also be checked independently.

## Disclaimer

This is an unofficial, experimental project with no affiliation to Playnite, Valve, Steam, or the original Helium developers.

I've tested the initial version on my own setup, but I can't guarantee that everything will work correctly on yours.

Please keep a backup of your working configuration before trying development builds.

---

Made by someone who spends an unreasonable amount of time organizing his game library instead of actually playing it.
