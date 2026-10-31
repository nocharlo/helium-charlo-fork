# Helium v0.3.0 — Testing & validation

This is a record of what I tested for Navigator Experience, what the automated checks cover, and what still needs checking.

## Tested in Playnite

I tested the v0.3.0 candidate in portable Playnite and in my main installation. The checks I completed covered:

- Navigator search, sorting and grouping, including switching back to Playnite's organization.
- Recent Activity and its grouping modes.
- The Home button and selection synchronization across Grid and Details.
- Advanced Filters used alongside search and native library filters.
- Keeping the selected game visible after explicitly changing sort order.
- Closing Sort & Group with Escape.
- Sidebar width persistence across restarts.
- An extended stability and responsiveness check (QA-030).

These are manual checks of the configurations I used, not a claim that every Playnite setup has been tested.

## Automated tests

| Check | Result |
| --- | --- |
| `LogicTests` | PASS — 18 checks |
| `NavigatorProjectionTests` | PASS — 47 checks |
| `NavigatorRefreshDecisionTests` | PASS — 44 checks |
| `NavigatorFilterComposerTests` | PASS — 34 checks |
| `SettingsFileTests` | PASS — persistence, recovery, migration, and reload |
| Release build | PASS — 0 errors, 0 warnings |

The package checks also confirmed the matching theme/extension version, stable IDs, and expected package structure for the candidate.

## Still to verify

The following Advanced Filters integration checks were not completed in a running Playnite instance during final release testing:

- **AF-01:** Behavior of the filter state after restarting Playnite.
- **AF-02:** Saved filter presets remain unchanged after using and clearing Advanced Filters.
- **AF-03:** The sidebar reflects a preset selected through Playnite itself.
- **AF-04:** Clear behavior and AND/OR combinations in the interface. The underlying composition logic passed its automated checks.
- **AF-05:** Filter-panel performance with a large, metadata-rich library.

These are outstanding checks, not confirmed bugs. In particular, the automated tests cover temporary filter composition and copying preset settings, but they do not replace AF-02 in the actual interface.

## Release packages

The theme and Navigator extension must have matching version numbers. Any package metadata changes require packaging again and verifying the final downloads. SHA-256 checksums are recorded with the release artifacts once those packages are finalized.
