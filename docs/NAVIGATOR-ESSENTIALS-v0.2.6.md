# v0.2.6 Navigator Essentials: preset synchronization baseline

This first step records the current v0.2.5 behavior and adds opt-in diagnostics. It does **not** change Navigator ordering, native commands, click handling, grouping preferences, or Playnite filters. The six preset observations below were reported by the maintainer; they have not yet been reproduced on this branch.

## Source and SDK map

| Concern | Current path | Consequence |
| --- | --- | --- |
| Native filtered set | `LibraryStateAdapter.FilteredGames` reads `api.MainView.FilteredGames`; `Navigator.Refresh` polls every 1.5 seconds | Navigator inherits native membership. It does not reimplement the preset filter. |
| Rebuild trigger | `Navigator.Refresh` sorts filtered IDs by GUID and compares them with the previous snapshot; database and Navigator grouping events set `dirty` | A sort or direction change with identical membership does not rebuild rows. The pure decision is now in `NavigatorRefreshDecision`, with the same behavior as v0.2.5. |
| Row ordering | `NavigatorProjection.Build` sorts by `SortingName`, falling back to `Name`, using current-culture case-insensitive comparison | Native Playnite ordering is discarded even when filtered games arrive in native view order. |
| Navigator grouping | `LibraryStateAdapter.GroupNames` supports only Series, Category, and Platform; `NavigatorProjection.Build` orders group names alphabetically; `Navigator` applies WPF grouping when the Navigator preference is nonzero | Native `MainView.Grouping` is not used for Navigator rows. Completion Status grouping has no Navigator representation today. |
| Native view state | `LibraryStateAdapter` exposes `SortOrder`, `SortOrderDirection`, and `Grouping` | Before this step, they were read only to decide whether captured Grid scroll could be restored. Optional diagnostics now observe them without changing refresh decisions. |
| Local search | `Navigator.ApplySearch` filters existing rows using `NavigatorProjection.Matches` | It narrows the native filtered set; it does not apply or clear a Playnite preset. |

The project's C# target is .NET Framework 4.6.2 with PlayniteSDK 6.11.0. The public SDK defines `IMainViewAPI.FilteredGames`, `SortOrder`, `SortOrderDirection`, `Grouping`, `GetActiveFilterPreset()`, and `GetCurrentFilterSettings()`. Presets can store their own sort order, direction, grouping, and filter settings. Playnite's Desktop implementation exposes the native collection view through `FilteredGames`; that view can include grouping entries, so its exact order and tie behavior must be checked in the installed Playnite version before using it as the source of Navigator order. See the [main view API](https://api.playnite.link/docs/api/Playnite.SDK.IMainViewAPI.html), [filter preset model](https://api.playnite.link/docs/api/Playnite.SDK.Models.FilterPreset.html), and [Playnite Desktop implementation](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite.DesktopApp/Api/MainViewAPI.cs).

## Six preset acceptance cases

“Expected” means parity with the *active native Grid configuration*, including any user-selected sort field, direction, grouping, and filters. The preset names alone do not prove their saved settings.

| Preset | Maintainer observation | Expected Navigator behavior | Manual evidence needed |
| --- | --- | --- | --- |
| Favorites | Filtering works | Same filtered membership as Grid; follow the preset's native ordering when ordering parity is implemented | Compare counts and relative order after switching from another preset, then after clearing local search. |
| Installed | Filtering works | Same installed membership as Grid; preserve native sort and direction | Check installed/uninstalled boundary and a preset switch with unchanged membership. |
| Most Played | Order differs | Match the preset's actual native sort field, direction, and tie behavior; do not assume whether it uses play count or playtime | Record the native sort enum and direction with diagnostics; compare several relative positions in Grid and Navigator. |
| Recently Played | Membership filters, order differs | Match Last Played (`LastActivity`) descending when that is the saved native setting; handle games without a last-played value as Playnite does | Confirm the saved field/direction and compare recent, older, and never-played entries without publishing their names. |
| Rating | Grid order works, Navigator order differs | Match the actual score field (user, critic, or community), direction, null placement, and ties | Read the native sort enum and direction; compare rated and unrated positions. |
| Completion Status | Not working correctly; exact failure unknown | Match native filtered membership; if grouping by completion status is intended, show the native grouping rather than the Navigator's unrelated Series/Category/Platform grouping | Determine whether the preset applies `CompletionStatuses` filtering, `Grouping=CompletionStatus`, both, or neither. Compare native/Navigator counts, status group boundaries, and missing-status behavior. |

The SDK exposes `FilterPresetSettings.CompletionStatuses` separately from native `GroupableField.CompletionStatus` ([filter settings](https://api.playnite.link/docs/api/Playnite.SDK.Models.FilterPresetSettings.html), [filter field](https://api.playnite.link/docs/api/Playnite.SDK.Models.IdItemFilterItemProperties.html)). Static source inspection cannot tell which setting is saved in the user's preset. That distinction is the first manual diagnostic for Completion Status.

## Optional local diagnostics

Set `HELIUM_NAVIGATOR_DIAGNOSTICS=1` before starting **a test Playnite process** to enable diagnostics. Any other value, or an unset variable, leaves them off. Logs report native sort/direction/group state changes, whether the completion-status filter is active, preset transitions as a boolean, rebuild reasons, and refresh/rebuild timing summaries. They do not log game names, game IDs, preset IDs, paths, or filter values. The 1.5-second polling interval is unchanged. Diagnostics run while the Navigator is expanded, aggregate skipped refreshes over 60 seconds, and log rebuild timing only on a rebuild.

Keep diagnostic logs, screenshots, and test-library data outside the repository. Inspect and sanitize them before sharing. A shell launch or a successful build does not count as a Playnite UI test.

## Validation of this diagnostic step

- New pure refresh/diagnostic tests: 17 passed.
- Existing projection and logic tests: 12 and 13 passed, respectively.
- Existing settings persistence/recovery test: passed using a disposable writable test directory.
- Extension build: .NET Framework 4.6.2 target compiled against the declared Playnite SDK with zero warnings and zero errors. The standalone test projects ran with a .NET 10 SDK target override available in the local toolchain.
- Playnite UI, six preset scenarios, portable deployment, and diagnostic log behavior in a running Playnite process: **NOT RUN** in this step. No ordering-parity claim is made.

## Next parity step

1. Run the six cases in isolated portable Playnite with diagnostics enabled. Record the exact native sort/direction/group state and whether membership changes, without retaining personal titles or IDs in public evidence.
2. For Completion Status, distinguish filtering failure from missing native grouping and from a preset configuration issue. Do not add a duplicate filter unless the native filtered set itself is insufficient.
3. Add an explicit ordering input to the pure projection, backed by tests for native order, direction, ties, missing values, and Navigator-specific grouping. Keep existing alphabetical behavior as the fallback until the native contract is confirmed.
4. Change the refresh trigger to react to the relevant native view-state transitions. Preserve Grid scroll guards, selection, click commands, and the current visual layout.
5. Build and deploy paired packages to the portable sandbox; run automated tests and the six focused UI regressions. Stop on unresolved difference before touching the primary Playnite installation.
