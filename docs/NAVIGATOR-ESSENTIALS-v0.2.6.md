# v0.2.6 Navigator Essentials: preset synchronization baseline

The first local commit recorded the v0.2.5 behavior and added opt-in diagnostics. The ordering candidate described below now preserves native order and reacts to native view-state changes. Native commands, click handling, Navigator grouping preferences, and Playnite filters remain unchanged. The six preset observations were reported by the maintainer and still require runtime verification on this branch.

## Baseline source and SDK map (first commit)

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

## Ordering parity candidate

The Playnite Desktop `MainViewAPI.FilteredGames` implementation enumerates the current collection view and removes duplicate game objects in encounter order. The collection view applies the native sort field, direction, and grouping before this API read ([MainViewAPI source](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite.DesktopApp/Api/MainViewAPI.cs), [DesktopCollectionView source](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite.DesktopApp/DesktopCollectionView.cs)). This supports using the API sequence rather than reimplementing Playnite comparators. The installed portable Playnite build still requires UI verification.

- `NavigatorProjection.Build` now preserves the incoming game sequence. With a Navigator-specific group selected, it continues to sort group headings alphabetically and preserves the native game sequence within each group. Search keys, row counts, icons, and independent group membership rules remain unchanged.
- `Navigator.Refresh` now compares both sorted IDs (membership) and incoming IDs (native order). It also watches native sort field, direction, grouping, and active preset identity. Any of these changes can trigger a rebuild even when membership is unchanged. The 1.5-second poll and full-rebuild behavior remain; this is not an incremental refresh rewrite.
- The diagnostic log now includes `orderChanged` and `viewStateChanged` as rebuild reasons. It still excludes game and preset identifiers.
- This change does not add native group headings to the Navigator. Its existing None/Series/Category/Platform grouping preference remains independent. Completion Status is still under investigation.

## Validation of the ordering candidate

- Pure refresh decision and diagnostic tests: 21 passed.
- Projection tests: 16 passed, including native order, reversed direction, ties, and retained Navigator group behavior.
- Existing logic tests: 13 passed. Existing settings persistence/recovery test: passed.
- Extension build and Test Harness package build: passed with zero warnings and zero errors. The static package validator passed 65 XAML/XML files, two paired packages, DLL integrity, and stable IDs.
- The development packages still carry the unchanged v0.2.5 manifests and are not a v0.2.6 release. This local candidate has theme SHA-256 `95F99C4D83CD4B2624728A7A50A99BB271748FB09592ABD539AAF8C371D187B6` and extension SHA-256 `973205483895F158AA227BF3ACD136D7F1F6304AC8BDA3B6D0CF7EB621766FBE`. The build manifest records a dirty source tree because the validation preceded this commit.
- Portable Playnite preset checks: **PENDING** until the primary Playnite process is closed and the sandbox can be safely deployed. No visual parity claim is made.

## Completion Status investigation

The native SDK separates `FilterPresetSettings.CompletionStatuses` (membership filtering) from `GroupableField.CompletionStatus` (presentation grouping). Navigator reads the native `FilteredGames` set, so a filter that changes that set should affect its membership. Navigator has no Completion Status group headings. A preset could be using filtering, grouping, both, or neither; its saved configuration and runtime result have not been inspected. In the portable test, compare Grid and Navigator membership first, then compare group boundaries. Do not implement a duplicate status filter based on appearance alone.

## Remaining refresh and UI work

1. Confirm Recently Played, Most Played, and Rating order and ties in the portable Playnite; verify a sort or preset switch that keeps membership unchanged. Check Favorites, Installed, Navigator-specific groups, selection, and Grid/Details transitions for regressions.
2. Determine whether Completion Status differs in membership, presentation, or preset configuration. Native Completion Status headings are not implemented in this candidate.
3. The 1.5-second poll still reads the full filtered list and sorts an ID copy for membership comparison. A detected change recreates all projected rows and the WPF `ListCollectionView`; it does not move only affected rows. That can reset group expansion or scroll and repeat icon-path resolution. Measure this before any incremental refresh work.
4. The SDK path used here has no filter-change event in this integration. Any event-driven replacement for polling needs a documented public SDK signal and Windows regression tests.
