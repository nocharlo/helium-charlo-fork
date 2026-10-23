# Changelog

## v0.2.4 — stability hotfix, accepted for normal use

- Kept Navigator selection inside Full Details View without an automatic return to Grid, independent of sidebar click preference.
- Added guards for sidebar drag releases, mismatched rows and interactive children before scheduling a single-click action; double-click launch remains separate. Two uncommon mouse-release interactions remain unresolved.
- Preserved and logged unreadable Navigator settings before default fallback; normal saves use a temporary file and previous-file backup.
- Corrected keyboard context-menu row targeting and an inherited tooltip translation.
- Retained the approved v0.2.3 backgrounds, ThemeModifier resources, converter integrations, stable IDs and existing navigation settings.

## v0.2.3 — previous tested baseline

The maintainer installed and validated this version in Playnite Desktop on Windows. It is based directly on the supplied working v0.2.2 package.

- Stabilized the Steam-style game Navigator beside native Grid and Details views, including search, icons, expandable groups and synchronized selection.
- Added independent sidebar and grid-cover click selectors for native Full Details View or the existing Grid Details Panel.
- Kept Back to Grid, selection and conditional grid scroll restoration.
- Kept complete sidebar collapse, a floating expand control, resizing, responsive layout and native List View compatibility.
- Migrated saved v0.2.2 navigation checkboxes to the two new selectors; retained the persistent theme and extension IDs.
- Continued using the KKCD artwork, overview and extension integration points.

## v0.1.0 — initial published baseline

The original working Helium Hybrid (KKCD) prototype introduced a searchable, scrollable game sidebar with icons and grouping alongside the KKCD cover grid; two-way selection synchronization; basic collapse and resize; and separate theme and extension installers. The maintainer tested it on Windows. Its code and exact original metadata remain in Git history.

## Experimental local builds

Development packages between these baselines explored layout, navigation, collapse and settings behavior. They were local iterations, not separate releases documented here. The functional v0.2.2 package was the direct source of v0.2.3. No release dates are asserted for these entries.
