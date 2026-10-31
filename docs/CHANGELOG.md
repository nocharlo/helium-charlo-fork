# Changelog

## v0.3.0 — Navigator Experience

- Integrated a unified Home control with the native Grid navigation command and preserved the selected game when returning from Details.
- Added independent Navigator grouping and sorting controls, including native order, name, last played, playtime, release date, and ascending or descending direction.
- Added Recent Activity with Hybrid and Detailed chronological grouping and independent expansion state.
- Added Advanced Filters that compose with Playnite's current global filter state without saving or editing user presets, plus a Ready to Play shortcut.
- Preserved local search and synchronized global filter, game selection, Grid, and Full Details behavior.
- Improved explicit-selection visibility and corrected scroll-to-selected behavior after deliberate reordering.
- Added Escape dismissal for the Sort & Group popup and retained Navigator width and other settings across restarts.
- I tested the candidate in both portable Playnite and my main installation: navigation, sorting and grouping, Recent Activity, filter/search combinations, selection visibility, popup dismissal, width persistence, and extended use. This covers the interactions I tried, not every possible Playnite setup.

## v0.2.8 — Navigator Header & Advanced Filters (development candidate)

- Added compact Navigator shortcuts for Grid, Recent Activity, and installed games.
- Added an Advanced Filters panel that edits Playnite's current global filters without saving or mutating user presets.
- Kept local name search, existing grouping and settings compatibility.
- This candidate was incorporated into v0.3.0 and was not published as a stable release.

## v0.2.7 — Recent Activity (release candidate)

- Added Recent Activity grouping using Playnite LastActivity and the local calendar.
- Hybrid groups previous years annually; Detailed groups them by month. Both preserve independent expansion state.
- Preserved native filters, Navigator search, selection, Reveal Selected Game, and chronological ordering.
- Fixed month labels to remain in English under Portuguese and English Windows cultures.
- I tested the v0.2.7 candidate in Playnite; the PR also records the automated and package checks.

## v0.2.6 — Navigator Essentials

- Follows native filtered game order and supported native grouping, including custom Rating categories; explicit Navigator grouping remains independent.
- Shows favorite and installation indicators and updates existing rows for visual-only game changes.
- Avoids unnecessary rebuilds, preserves selection and scroll on structural refresh, and retains group expansion across view replacement and virtualization.
- Portable Playnite confirmed Rating and Completion Status grouping, custom unrated category, group state after scrolling and preset changes, and earlier preset, search, selection, view, layout and preference checks on the same functional candidate.
- Runtime diagnostic evidence for no-rebuild updates remains pending; pure refresh-decision tests pass. The v0.2.6 release was published.

## v0.2.5 — Architecture Foundation, locally validated candidate

- Extracted plain C# game ordering, group expansion, search matching and distinct counts into `NavigatorProjection`, with 12 directed checks.
- Moved the WPF row and bounded local icon cache into `NavigatorPresentation`; centralized read-only Playnite library and view-state access in `LibraryStateAdapter`. Removed the adapter's dependency on a helper in `Navigator.cs`.
- Added an isolated portable Playnite test harness with offline build, paired packaging, identity checks, verified backup, deployment, rollback and evidence capture. The sandbox clears inherited development-extension paths and startup scripts.
- Kept theme and extension IDs, settings format, click handling, native commands, layout behavior and approved v0.2.4 packages unchanged.
- Validated the candidate in portable Playnite: start, selection/Grid/details synchronization, local and global search, Platform/Series/Category grouping, Grid/Details views, and grouping preference after a full restart. The final selection cleanup was retested separately. See [v0.2.5 validation](VALIDATION-v0.2.5.md) for build hashes and test scope.

This is a local candidate. No push or release publication was performed. The two uncommon mouse-release issues documented for v0.2.4 remain outside this refactor.

## v0.2.4 — stability hotfix, accepted for normal use

- Kept Navigator selection inside Full Details View without an automatic return to Grid, independent of sidebar click preference.
- Added guards for sidebar drag releases, mismatched rows and interactive children before scheduling a single-click action; double-click launch remains separate. Two uncommon mouse-release interactions remain unresolved.
- Preserved and logged unreadable Navigator settings before default fallback; normal saves use a temporary file and previous-file backup.
- Corrected keyboard context-menu row targeting and an inherited tooltip translation.
- Retained the approved v0.2.3 backgrounds, ThemeModifier resources, converter integrations, stable IDs and existing navigation settings.

## v0.2.3 — previous tested baseline

I installed and tested this version in Playnite Desktop on Windows, starting from the working v0.2.2 package.

- Stabilized the Steam-style game Navigator beside native Grid and Details views, including search, icons, expandable groups and synchronized selection.
- Added independent sidebar and grid-cover click selectors for native Full Details View or the existing Grid Details Panel.
- Kept Back to Grid, selection and conditional grid scroll restoration.
- Kept complete sidebar collapse, a floating expand control, resizing, responsive layout and native List View compatibility.
- Migrated saved v0.2.2 navigation checkboxes to the two new selectors; retained the persistent theme and extension IDs.
- Continued using the KKCD artwork, overview and extension integration points.

## v0.1.0 — initial published baseline

The original working Helium Hybrid (KKCD) prototype introduced a searchable, scrollable game sidebar with icons and grouping alongside the KKCD cover grid; two-way selection synchronization; basic collapse and resize; and separate theme and extension installers. I tested it on Windows. Its original code and metadata remain in Git history.

## Experimental local builds

Development packages between these baselines explored layout, navigation, collapse and settings behavior. They were local iterations, not separate releases documented here. The functional v0.2.2 package was the direct source of v0.2.3. No release dates are asserted for these entries.
