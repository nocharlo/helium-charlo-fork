# Helium (charlo fork)

**A Steam-inspired library sidebar for Playnite Desktop, built around Playnite's existing views.**

![Helium library overview](docs/images/library-overview.webp)

Helium (charlo fork) is a modified version of [Helium](https://github.com/darklinkpower/Helium), based on the [KKCD fork](https://github.com/kushagr-m/Helium). Its main addition is the **Helium Navigator**, a custom sidebar that lets you browse and organize your games while keeping Playnite's Grid and Details views accessible.

The idea is to bring some of Steam's library navigation into Playnite without replacing the library system underneath it.

**Latest release: v0.3.2 — Library Filters & Native Organization**

[Download v0.3.2](https://github.com/nocharlo/helium-charlo-fork/releases/tag/v0.3.2) · [Changelog](docs/CHANGELOG.md) · [Report an Issue](https://github.com/nocharlo/helium-charlo-fork/issues)

---

## Why I Started This

I've been using Playnite for a while, and one thing I kept missing from Steam was its library sidebar.

I wanted to have a compact list of games on the left while browsing covers or reading game details on the right. Being able to jump between games without constantly switching views is such a basic part of how I use Steam that I found myself missing it in Playnite.

I looked around for something similar, but nothing quite worked the way I wanted.

At the time, I was already using the KKCD fork of Helium, so I started experimenting with AI-assisted code to see if I could add my own Navigator to it.

The first working version was pretty rough, but it did what I wanted. I kept working on it, fixing things that bothered me, adding features, and gradually figuring out what kind of experience I wanted to build.

That's how this fork came about.

I'm sharing it because I figured I probably wasn't the only person who wanted something like this. It's still a personal project, but I'm working toward making it reliable enough for other people's libraries, too.

## Features

### Helium Navigator

The Navigator adds a compact, scrollable game list alongside Playnite's library views.

- Search for games directly from the sidebar without replacing the main library search.
- Browse expandable groups based on platforms, categories, series, and other library properties.
- Keep game icons visible, with separate styling for installed and uninstalled titles.
- See favorite indicators without opening a game's details.
- Resize or collapse the sidebar to give the Grid more room.
- Keep your preferred width, grouping, sorting, and other settings between sessions.

The Navigator uses your existing Playnite library. It doesn't create another game database or require you to reorganize your metadata.

### Working With Playnite's Views

I didn't want to replace the Grid or Full Details View just to have a sidebar.

The Navigator works alongside Playnite's existing library interface:

- **Grid View:** Browse covers with the Navigator beside them.
- **Grid Details Panel:** Select a game and view its information alongside the Grid.
- **Full Details View:** Open the full game page while keeping the Navigator available.
- **List View:** Continue using Playnite's native list without an unnecessary second sidebar.

You can configure what happens when you click a game in the Navigator or a cover in the Grid, independently.

For example, I use the Navigator to open the Grid Details Panel and the covers to open Full Details View.

Selection is synchronized between supported views, with handling for returning to the Grid, restoring scroll positions, and revealing games that move between groups.

A lot of development time has gone into making those transitions behave consistently. They turned out to be considerably more complicated than adding the sidebar itself.

### Sort & Group

The Navigator has its own sorting and grouping controls.

You can follow Playnite's current organization or configure the Navigator independently. Supported sorting, direction, and grouping changes can also be synchronized back to Playnite when you want both views to use the same organization.

Recent versions have added more sorting and grouping options, improved handling of missing metadata, and made the available modes customizable.

You can hide modes you don't use and rearrange their order in the Navigator settings.

When following Playnite, the Navigator respects the native sorting order rather than trying to reproduce it separately.

### Recent Activity

Recent Activity organizes games according to when they were last played.

There are two presentation modes:

- **Hybrid:** More detailed groups for recent activity, with older entries organized by year.
- **Detailed:** A more granular chronological history, including monthly groups.

Each mode keeps its own expanded and collapsed groups.

Recent Activity works with the existing library filters and Navigator search, so you can narrow the results without losing the chronological organization.

### Library Filters

Library Filters is a floating panel for working with Playnite's native filters without leaving the Navigator.

It currently exposes **27 filter fields**, organized into five groups:

- Library & Status
- Classification
- Game Information
- Activity & History
- Ratings

The panel adjusts between one, two, and three columns depending on its width. Sections can expand, and the contents become scrollable when they exceed the available height.

You can search for fields or metadata values, select multiple values, and load additional entries in batches when a field contains a long list.

Active filters appear as removable chips, making it easier to see what's affecting the library.

The **Clear filters** action removes conditions managed by Library Filters while preserving unrelated native filter conditions and Playnite's existing AND/OR mode.

You can also hide and reorder fields and groups under **Navigator Settings → Filters**. Hiding a field doesn't remove its active condition.

The Ready to Play shortcut and the Navigator's local search remain available alongside these controls.

## Screenshots

Click a screenshot to open it at full resolution.

| Hybrid Grid & Details | Sort & Group |
|:---:|:---:|
| [![Navigator beside the Grid and EarthBound details](docs/images/hybrid-layout.webp)](docs/images/hybrid-layout.webp) | [![Navigator Sort and Group controls](docs/images/sort-and-group.webp)](docs/images/sort-and-group.webp) |
| **Recent Activity + Details** | **Native Full Details View** |
| [![Recent Activity alongside Silksong details](docs/images/recent-activity.webp)](docs/images/recent-activity.webp) | [![Full Details View showing DOOM Eternal](docs/images/full-details.webp)](docs/images/full-details.webp) |

## Installation

**Requires Playnite 10 Desktop on Windows.**

Download both packages from the latest release:

- [Helium Theme (.pthm)](https://github.com/nocharlo/helium-charlo-fork/releases/download/v0.3.2/Helium-charlo-fork-v0.3.2.pthm)
- [Helium Navigator (.pext)](https://github.com/nocharlo/helium-charlo-fork/releases/download/v0.3.2/Helium-Navigator-v0.3.2.pext)

Open each package with Playnite and confirm the installation.

Restart Playnite, then select **Helium (charlo fork) v0.3.2** under **Settings → Appearance → Desktop**.

Make sure the theme and Navigator extension are on matching versions. The Navigator currently depends on the theme for its integrated sidebar layout, so installing the extension alone won't give you the same experience.

If you're updating from an older version, back up your existing theme and Navigator settings first.

The Grid Details Panel experience also relies on [Details to Grid View Converter](https://playnite.link/addons.html#ThemesDetailsViewToGridViewConverter_ef8a7226-eedc-478d-a506-92ee6c088aa3).

## Development

The project currently has two main components:

- **Helium Theme:** The modified XAML/WPF theme, including the layout that hosts the Navigator.
- **Helium Navigator:** A C# extension responsible for the game list, sorting, grouping, selection, navigation, and settings.

The current codebase targets .NET Framework 4.6.2 and PlayniteSDK 6.11.0.

The repository includes automated tests and tooling for building, packaging, and validating releases. I also use an isolated portable installation of Playnite to test changes without risking my main library.

Development documentation is available in the [docs directory](docs/README.md), along with the project's [development guidelines](AGENTS.md).

### A Note on AI-Assisted Development

I don't have a traditional programming background. I started this project using AI tools, mainly ChatGPT and Codex, to help me write and modify code that I wouldn't have been able to produce on my own.

I handle the design, requirements, feature decisions, testing, and quality control. I also spend a fair amount of time investigating bugs, reviewing behavior, and figuring out how different parts of Playnite interact.

The code itself has largely been written with AI assistance.

I think it's worth being upfront about that, especially for an open-source project. AI-generated code can introduce problems that aren't immediately obvious, and I'm aware that relying on these tools comes with technical limitations as well as broader environmental and social concerns.

I've been putting more effort into automated tests, isolated testing environments, regression checks, and documentation as the project has grown. I'm learning along the way, but I don't consider that a replacement for experienced developers reviewing the code.

If you know Playnite or C# and spot something questionable, feel free to open an issue or suggest a better implementation. I'm not particularly attached to how a piece of code was written if there's a more reliable way to do it.

## Roadmap

Most development so far has focused on the Navigator. Once it's stable enough, I want to spend more time improving the Helium theme itself.

The roadmap is subject to change. Some ideas are still being explored, and I'd rather revise the scope than force a feature into a release before it's ready.

### Navigator

**v0.3.3 — Navigator Controls (Release Candidate)**

This update expands what you can do directly from the Navigator.

The context menu gains more native Playnite actions, including Play/Install, Edit Game, categories, completion status, favorites, links, file operations, and supported extension contributions.

It also improves keyboard navigation, selection handling, transitions between Grid and Details, and the floating Library Filters panel.

The current candidate is undergoing final testing before release.

**v0.3.4 — Architecture & Performance (Planned)**

The Navigator has grown quite a bit since the original prototype, and some of its internal architecture needs attention before I keep adding features.

This update will focus on investigating performance bottlenecks, reducing unnecessary work during library updates, simplifying tightly coupled components, and improving the testing workflow.

Performance in larger libraries is a particular concern. Most of my testing has been done with a relatively small personal library, so broader testing is needed to understand how well the Navigator scales.

**v0.4.0 — Adaptive Navigator (Exploratory)**

I'd eventually like the Navigator to work more independently from Helium.

The idea is to separate its core behavior from theme-specific presentation, allowing it to work with other compatible Desktop themes where technically possible.

That doesn't necessarily mean every theme will support the same integrated sidebar layout. The Playnite theme and extension APIs impose some limitations, and I'll need to investigate those before deciding what a standalone release can realistically offer.

### Theme Redesign — Proposed v0.5.x Series

Once the Navigator is in better shape, I want to revisit the rest of Helium.

The theme already supports a fair number of Playnite extensions, which is one of the reasons I use it. But its layout has accumulated features over time, and some parts could really use a more thorough redesign.

The intention is to keep those integrations while making the interface easier to navigate and less cluttered.

**Full Details**

Rework the game details page while keeping the existing banner and artwork presentation.

I want genres and tags to be more visible, activity information to be better organized, and related-game sections to take up less space.

The goal is to make better use of the information already available, including data supplied by other extensions.

**Grid & Hybrid**

Improve the Grid and the optional side details panel without turning the covers into oversized information cards.

Cover artwork should remain the focus. The Navigator and details panel should fit around it without wasting space or making the library unnecessarily cramped.

**Genre & Tag Icon System**

I'm also exploring a set of icons for genres and tags.

Rather than requiring everyone to use the same names or classifications, the system would support aliases, custom assignments, reusable icon packs, and fallbacks for anything it doesn't recognize.

It would be a visual layer over Playnite's existing metadata, not a replacement for it.

### Longer-Term Ideas

**Helium Home**

I've experimented with the idea of a more complete library homepage, with features such as Continue Playing and customizable game shelves.

An earlier prototype is archived. I still want to revisit the idea, but it needs more design and technical work before becoming part of the release roadmap.

## Known Limitations

There are still a few rough edges:

- The details panel can briefly appear blank while Playnite rebuilds the library view and restores selection.
- Some unusual mouse interactions haven't been tested thoroughly across different configurations.
- Uninstalled game titles can appear brighter than intended on hover.
- The layout can feel cramped at smaller window sizes.
- Compatibility with every Playnite extension, theme configuration, or library setup hasn't been established.
- Performance in very large libraries still needs broader testing.

I develop and test primarily on my own setup, so issues that don't show up there may behave differently elsewhere.

If you run into a problem, please [open an issue](https://github.com/nocharlo/helium-charlo-fork/issues). Reproduction steps, screenshots, Playnite version, and relevant library details are especially helpful.

## Credits

This project builds on work by people who made the original theme and the surrounding Playnite ecosystem possible.

- [Josef Nemec](https://github.com/JosefNemec) — Creator of Playnite.
- [Brandon Balderas (darklinkpower)](https://github.com/darklinkpower/Helium) — Creator of the original Helium theme.
- [Kush Mittal (KKCD)](https://github.com/kushagr-m/Helium) — Helium KKCD fork.
- **Playnite extension developers** — For the extensions and integrations supported by the theme.
- **charlo** — Fork maintenance, Navigator concept, design, specifications, testing, and AI-assisted development.

The original developers aren't responsible for bugs or changes introduced by this fork.

## License

This project is based on Helium and its KKCD fork. The inherited theme code is distributed under the **MIT License**, with the original attribution preserved in the repository's [license](license) file.

Helium (charlo fork) is an unofficial community project and is not affiliated with Playnite, Valve, or Steam.

---

*Made by someone who apparently enjoys organizing and developing his game library almost as much as actually playing the games.*
