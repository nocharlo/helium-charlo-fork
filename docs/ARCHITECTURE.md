# Architecture of the v0.2.4 library Navigator

## Components

| Component | Source | Responsibility |
| --- | --- | --- |
| Library host | `theme/Views/Library.xaml` | Inserts the Navigator beside native Grid, Details and List views; binds selected game and the grid panel command; supplies hidden native view-switch command adapters and the floating expand button. |
| Native Grid template | `theme/Views/LibraryGridView.xaml` | Keeps `PART_ListGames` and `PART_ControlGameView` (the existing grid overview). |
| Native Details template | `theme/Views/LibraryDetailsView.xaml` | Provides the native details view and the named list the Navigator hides after its own full-details navigation. |
| Navigator UI | `source/HeliumHybrid/Navigator.xaml`, `Navigator.cs` | Search, grouping, icons, selection synchronization, click handling, collapse, width, responsive overview host and return navigation. |
| Settings | `source/HeliumHybrid/Settings.cs`, `Plugin.cs` | Official Playnite add-on settings view, XML persistence in the plugin user-data directory, and migration from the v0.2.2 checkbox fields. |
| Converter integration | `theme/themeGridConverter.yaml`, `theme/themeExtras.yaml` | Existing theme integration settings; neither file implements a separate details panel. |

## Native commands and navigation

The extension is a `GenericPlugin` with `PluginUserControl` registered under source name `HeliumCharlo`. `Library.xaml` hosts it through Playnite's custom element support. The extension reads the selected and filtered games through `IPlayniteAPI.MainView`, selects a game with `SelectGame(Guid)`, and receives selection notifications through `OnGameSelected`. A periodic refresh also observes view/layout and library changes; database collection events mark the list dirty. The game list uses virtualization in `Navigator.xaml`, while local icons are decoded at visible-row size and cached with a bound.

The full-details action executes `SwitchDetailsViewCommand` through the theme's hidden command adapter. Back to Grid uses `SwitchGridViewCommand` and conditionally restores the captured scroll offset if filter, grouping and ordering are unchanged. The Navigator hides the redundant native Details list for this transition, and the native List View hides the Navigator.

The grid-panel action uses the `ShowGameSideBarCommand` already bound to the grid information button. `Library.xaml` binds that command to the Navigator host's `Tag` and `MainViewModel.SelectedGame` to its `DataContext`. The extension executes the command with that selected entry from Grid. In Playnite Desktop source, it enables `GridViewSideBarVisible` and selects the entry. In Full Details View, selecting another Navigator game changes selection and keeps that view open, regardless of the stored sidebar action. The native overview and converter remain responsible for panel content.

Sidebar and cover click actions are separate selector values. A single click selects immediately; navigation waits for the Windows double-click interval plus a small margin. A second click cancels navigation. Both handlers require matching mouse-down and mouse-up rows and reject significant movement, modifiers and interactive children. View changes cancel pending click actions. The sidebar's small Play menu uses its selected row for keyboard invocation; Playnite retains native grid menus and shortcuts.

The Grid Details Panel layout selector controls the default host visibility independently. An explicit panel request temporarily reveals the overview host even in a compact or Always hidden layout. The Navigator observes native overview visibility during its existing 1.5-second layout refresh and restores the configured layout after the panel closes; it does not persist that temporary override.

## IDs, migration and reproduction

The theme and extension GUIDs remain stable across v0.2.2, v0.2.3 and v0.2.4. The plugin's `navigator.xml` stores width, collapse, grouping, panel layout and click actions. Old navigation checkboxes still migrate. An unreadable settings file is copied to a timestamped `.corrupt-*` path before default settings may be saved; if copying fails, the plugin refuses to overwrite it. A successful save writes a temporary file and replaces the old file with a `.bak` of its prior contents.

To reproduce the integration in another theme, start with a native `Library.xaml` view host and a native Grid overview. Add a custom control beside the native view host, bind the selected entry and `ShowGameSideBarCommand` to that host, register the control through Playnite's custom element API, and route clicks through the existing native commands. Keep the native grid list and overview templates intact. Read `Navigator.cs` before adapting element names: it locates named template parts in the visual tree, so these names are part of this theme's integration contract. Validate in a real Playnite Windows installation after building; parsing XAML and compiling C# cannot verify runtime binding resolution.
