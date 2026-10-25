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

The candidate was built as paired v0.2.5 packages in a separate output directory. Its Playnite runtime checks covered Series, Category and Platform groupings, local and global searches, sidebar/grid/details synchronization, Grid↔Details, and Platform preference after a full process restart. The screenshots and exact package hashes are held in the local task evidence folder, not in Git. A transient Computer Use capture error was recovered by reselecting the exact sandbox window; the app log contained no Navigator or other error during the candidate run.

## Second extraction: presentation and icons

`NavigatorPresentation.cs` now owns `GameRow` and the 512-entry `IconCache`. Their class bodies were compared with the prior commit and are identical; only their file location and required `using` directives changed. The Playnite build compiled offline with zero warnings/errors, the projection checks passed, and candidate package/XAML validation passed. Portable deployment and visual testing were completed for this extraction together with the library adapter.

## Third extraction: Playnite library reads

`LibraryStateAdapter.cs` now owns read-only access to filtered games, selected games, active desktop view, sort/group state, icon paths, and Playnite metadata used for projection. `Navigator.cs` still issues selection and launch commands and owns WPF state. The existing null handling, filtering poll, source order, and UI-thread call sites are unchanged. The offline Playnite build, 12 projection checks, and package/XAML validation passed. The related portable milestone covered selection, Grid and Details, local and global search, Platform, Series and Category grouping, and preference persistence across a full restart.

## Final architecture audit

The dependency direction is now `Navigator.cs` → `LibraryStateAdapter.cs` → plain `NavigatorSource`, and `Navigator.cs` → `NavigatorProjection.cs` → `Logic.cs`. `NavigatorPresentation.cs` supplies the WPF-facing row and icon cache. The adapter no longer calls a helper defined in `Navigator.cs`; no source-level cycle remains among these extracted classes. SDK reads of filtered/selected games, active view, sort/group state, icon paths, and group metadata are centralized. SDK commands, database subscriptions, timers, WPF view manipulation and settings saves remain in `Navigator.cs`.

No new duplicate game projection or event subscription was found in this focused review. `OnLoaded` and `OnUnloaded` still pair the database and plugin subscriptions, and four timers retain their original intervals and stop points. The periodic 1.5-second poll remains necessary because the relevant filter change has no SDK notification in this integration. This area deserves focused lifecycle tests before extraction. `Navigator.cs` remains about 500 lines because input gestures, native navigation, layout and lifecycle are intentionally left for a later milestone.

The clean local v0.2.5 candidate from commit `5aa2c2b` was deployed to the isolated portable Playnite with matching installed/package DLL hashes, stable component IDs and one Navigator. Automated build, projection, logic, settings and package checks passed. Visual regression on `9db5a0e` covered the broad scenarios; after a five-line selection-collection cleanup, `5aa2c2b` was retested for selection and details synchronization. The last portable log showed no warnings or errors. See `docs/VALIDATION-v0.2.5.md` for exact provenance and limitations.

## Next architecture milestone

1. `SelectionController`: move selected-ID reconciliation and sidebar/grid selection state first. Keep the SDK read in `LibraryStateAdapter`; verify one-click selection and Details updates.
2. `NavigationController`: then separate click timers, drag guards, double-click launch, native view commands and Grid/Details transitions. This is the highest behavior risk and needs focused Windows tests.
3. `LayoutController`: separate width, collapse, native list visibility and overview host adjustments, preserving named theme controls and converter bindings.
4. `LifecycleController`: last, isolate subscription/timer ownership with explicit load/unload symmetry and dispatcher tests.

Plan these as separate, small changes with focused C# and portable UI checks. Do not start them in v0.2.5. The known v0.2.4 drag-release issues remain outside this architecture work.
