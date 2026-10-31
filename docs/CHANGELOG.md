# Changelog

A history of changes to Helium (charlo fork), from the original Navigator prototype to the current release.

## v0.3.0 — Navigator Experience

This update brings together the major Navigator improvements developed since v0.2.7. The focus is on making library navigation more flexible, with better organization controls, easier access to filters, and tighter integration with Playnite's native views.

### New features

- **Unified Home button:** Replaced the previous navigation controls with a single Home button that returns to the native Grid while preserving the selected game.
- **Sort & Group:** Added a dedicated popup for organizing the Navigator independently of the Grid.
  - Group by Playnite's native organization, Series, Category, or Platform.
  - Sort by Playnite's current order, Name, Last Played, Playtime, or Release Date.
  - Switch between ascending and descending order.
- **Advanced Filters:** Added a sidebar panel for filtering games using Playnite's existing library metadata, including favorites, libraries, platforms, genres, categories, tags, and completion status.
- **Ready to Play:** Added a shortcut for showing installed games.
- **Recent Activity:** Integrated the existing Hybrid and Detailed activity modes into the new Navigator controls.

### Improvements and fixes

- Improved synchronization between Navigator, Grid, Grid Details Panel, and Full Details View.
- Preserved the selected game when switching views or returning to the Grid.
- Fixed selected-game visibility after changing the Navigator's sorting or grouping.
- Improved scroll handling when revealing the selected game.
- Added Escape key support for closing the Sort & Group popup.
- Improved compatibility between Navigator search, Advanced Filters, and Playnite's native filters.
- Preserved sidebar width, organization preferences, and other Navigator settings across restarts.
- Refined the Navigator header and its controls for a more consistent layout.

### Testing and compatibility

Tested in both portable Playnite and a regular Playnite installation, including navigation, organization, filtering, selection synchronization, settings persistence, and extended use.

Automated tests cover the underlying sorting, grouping, refresh, filter composition, and settings logic. Some Advanced Filters interactions still need additional verification in different Playnite configurations.

This release focuses on the Navigator. A broader redesign of the Grid and Details views is not included.

---

## v0.2.8 — Navigator Header & Advanced Filters

*Unreleased development version — incorporated into v0.3.0.*

This development milestone introduced the first version of the Navigator controls that would later become part of Navigator Experience.

### Changes

- Added a compact Navigator header with shortcuts for Grid, Recent Activity, and installed games.
- Introduced an Advanced Filters panel connected to Playnite's existing global filter system.
- Added the ability to combine metadata filters with the Navigator's local game search.
- Designed the filtering controls to work without modifying saved user presets.
- Preserved existing grouping options and Navigator preferences.

These changes were refined and included in v0.3.0 rather than released separately.

---

## v0.2.7 — Recent Activity

Introduced a new way to browse the library based on when games were last played.

### New features

- **Recent Activity:** Added chronological grouping using Playnite's Last Activity data.
- **Hybrid mode:** Organizes recent activity into more detailed time periods while grouping older activity by year.
- **Detailed mode:** Provides a more granular month-by-month history.
- Added independent expanded and collapsed group states for each activity mode.

### Improvements and fixes

- Preserved compatibility with native Playnite filters and Navigator search.
- Maintained game selection and Reveal Selected Game behavior when using chronological groups.
- Improved chronological ordering across activity groups.
- Standardized month labels in English, regardless of whether Windows uses Portuguese or English regional settings.
- Improved group-state persistence when switching between activity modes.

---

## v0.2.6 — Navigator Essentials

Focused on making the Navigator more consistent with Playnite's library organization while improving responsiveness and preserving navigation state.

### New features

- Added favorite indicators and distinct styling for installed and uninstalled games.
- Expanded support for native Playnite grouping, including Rating and Completion Status.
- Added handling for custom rating groups and games without ratings.

### Improvements and fixes

- Updated the Navigator to follow Playnite's filtered game order when using native organization.
- Kept Navigator-specific grouping independent from the Grid.
- Improved refresh behavior to avoid unnecessary rebuilding when only visual game properties change.
- Updated existing rows when favorite or installation status changes.
- Improved preservation of the selected game, scroll position, and expanded groups during library updates.
- Improved compatibility with native sorting, presets, and changes between Grid and Details.
- Strengthened refresh logic to handle changes in filtered game order without losing the current navigation context.

### Technical changes

- Added dedicated refresh-decision logic and automated tests for common library update scenarios.
- Improved synchronization between the Navigator's presentation and Playnite's current library state.

---

## v0.2.5 — Architecture Foundation

A behind-the-scenes update focused on making the Navigator easier to maintain, test, and extend without changing its established behavior.

### Technical changes

- Introduced `NavigatorProjection` to handle game ordering, search matching, grouping, and row counts outside the UI.
- Introduced `NavigatorPresentation` to manage game rows and icon presentation.
- Added a bounded icon cache to reduce unnecessary image processing.
- Introduced `LibraryStateAdapter` to centralize access to Playnite's library and view state.
- Reduced dependencies between the Navigator's interface and its underlying logic.
- Added automated tests for projection and library organization behavior.

### Testing and stability

- Added an isolated portable Playnite environment for testing changes without affecting a regular installation.
- Added repeatable build, package validation, deployment, backup, and rollback procedures.
- Verified compatibility with existing Navigator settings, theme and extension identifiers, and native Playnite commands.
- Tested selection synchronization, search, grouping, Grid/Details navigation, and preference persistence.

This version established the technical foundation for the features introduced in later releases.

---

## v0.2.4 — Stability Hotfix

Focused on fixing navigation inconsistencies and improving the handling of mouse interactions and saved settings.

### Fixes

- Fixed an issue where selecting a game from the Navigator could unexpectedly return Full Details View to the Grid.
- Improved mouse interaction handling to distinguish intentional clicks from dragging or releasing over a different row.
- Added safeguards against unwanted navigation triggered by interactive elements inside game rows.
- Improved keyboard context-menu handling so actions target the selected game.
- Fixed an inherited tooltip translation issue.

### Reliability

- Improved recovery from unreadable Navigator settings.
- Added backup handling before replacing corrupted settings.
- Changed settings saves to use a temporary file and a backup of the previous configuration.
- Preserved the existing theme layout, artwork, integrations, and navigation preferences.

**Known limitation:** Two uncommon mouse drag-and-release interactions remained unresolved in this version.

---

## v0.2.3 — Navigator Stabilization

Established a more reliable baseline for the Navigator and expanded its integration with Playnite's native views.

### Features and improvements

- Stabilized the searchable, scrollable game list alongside Grid and Details.
- Improved synchronization between the selected Navigator entry and Playnite's selected game.
- Added independent click behavior settings for Navigator entries and Grid covers.
- Added support for opening either Full Details View or the Grid Details Panel through configurable actions.
- Improved returning to the Grid while preserving the selected game and restoring scroll position when appropriate.
- Improved sidebar resizing and collapse behavior.
- Added a floating control for reopening the Navigator when collapsed.
- Improved compatibility with window resizing and Playnite's native List View.
- Migrated earlier navigation settings to the updated configuration format.

The existing KKCD artwork, layouts, and theme integrations were retained.

---

## v0.1.0 — Initial Release

The first working version of Helium Hybrid, combining the Helium KKCD theme with a custom Navigator extension.

### Initial features

- Added a Steam-inspired game sidebar alongside the existing cover Grid.
- Introduced a searchable and scrollable game list with icons.
- Added basic game grouping and expandable sections.
- Implemented two-way selection synchronization between the Navigator and Playnite.
- Added sidebar resizing and collapse controls.
- Provided separate theme (`.pthm`) and extension (`.pext`) installers.

This release established the original concept: keeping a navigable game list visible while browsing Playnite's library views.

---

## Early Development Builds

Several local iterations were created between the initial release and v0.2.3, exploring navigation behavior, layout, resizing, selection synchronization, and settings persistence.

The working v0.2.2 build served as the foundation for v0.2.3. These intermediate builds were not published as separate stable releases.

---

For installation instructions, current features, known limitations, and project information, see the [README](../README.md).
