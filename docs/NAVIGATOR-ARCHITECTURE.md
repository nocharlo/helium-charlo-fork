# Navigator architecture foundation

This branch starts from the approved v0.2.4 commit `e94fc44`. The v0.2.5 work is a behavior-preserving refactor for Playnite 10 Desktop. Theme and Navigator IDs remain unchanged. A settings-only or no-code change cannot reduce the responsibilities concentrated in `Navigator.cs`; the existing `Logic.cs` already supplies pure normalization and grouping helpers.

## Responsibility audit before extraction

| Responsibility | v0.2.4 location | Coupling and risk |
| --- | --- | --- |
| Game ordering, group expansion, search keys and counts | `Navigator.cs` `Refresh`/`ApplySearch` | Pure behavior mixed with Playnite model access and WPF `ListCollectionView`; low-risk extraction |
| Icon path resolution and image decoding/cache | `Refresh`, `GameRow`, `IconCache` | Playnite database path plus file I/O/WPF images; extract after projection |
| Filtered game and selected-game reads | `Refresh`, `SyncSelection`, click timers and scroll methods | Direct `IPlayniteAPI` reads scattered through the class; moderate risk |
| Selection and click routing | `SelectionChanged`, sidebar/grid handlers, `Navigate` and detail methods | Timers, double-click suppression and native view transitions; high risk |
| Grid scroll, layout, width and collapse | `CaptureGridScroll`, `RestoreGridScroll`, `ApplySize`, `RecomputeLayout` | WPF visual tree and theme commands; high risk |
| Extension lifecycle and error handling | `OnLoaded`, `OnUnloaded`, database event handlers, `Safe` | Subscription symmetry and dispatcher state; high risk |

## First extraction: NavigatorProjection

`NavigatorProjection.cs` receives plain C# game snapshots and an injected icon-path resolver. It performs the existing case-insensitive sort by `SortingName`/`Name`, expands memberships with the existing `Logic.Groups` rules, computes search keys and distinct game counts. It contains no WPF or Playnite SDK references. `Navigator.cs` remains the adapter from Playnite `Game` objects and retains `ListCollectionView`, UI selection, icons, timers and navigation. No input gesture or view-routing code was changed.

The candidate was built as paired v0.2.5 packages in a separate output directory. Its Playnite runtime checks covered Series, Category and Platform groupings, local and global BioShock searches, sidebar/grid/details synchronization, Grid↔Details, and Platform preference after a full process restart. The screenshots and exact package hashes are held in the local task evidence folder, not in Git. A transient Computer Use capture error was recovered by reselecting the exact sandbox window; the app log contained no Navigator or other error during the candidate run.

## Second extraction: presentation and icons

`NavigatorPresentation.cs` now owns `GameRow` and the 512-entry `IconCache`. Their class bodies were compared with the prior commit and are identical; only their file location and required `using` directives changed. The Playnite build compiled offline with zero warnings/errors, the projection checks passed, and candidate package/XAML validation passed. Portable deployment and visual testing are deferred to the next related-extractions milestone.
## Safest continuation

1. Completed: presentation adapter move, with lazy icon decoding and cache unchanged.
2. Add a read-only `LibraryStateAdapter` that centralizes `FilteredGames`, selected IDs, sort/group state and Playnite metadata access. Keep UI-thread access and existing 1.5-second filter polling; do not add SDK subscriptions that are not available.
3. Isolate selection synchronization from click navigation. Preserve the current timer delays, drag thresholds, double-click launch and Grid-only click actions. Require a focused Windows milestone test before committing navigation behavior.
4. Separate width, collapse and view-layout operations, preserving the theme's named controls and converter-owned templates.
5. Review event/timer lifecycle last, pairing every subscription with its current unsubscription and retaining dispatcher marshaling.

Each step should be a small commit after relevant C# checks and build validation. Related steps can share one portable Playnite/Computer Use milestone, as long as failures are localized and no unverified behavior is reported as passed. The known v0.2.4 drag-release issues remain outside this architecture work.
