# Windows acceptance checklist — v0.2.4 stability hotfix

Back up v0.2.3 before testing. Use the maintainer's configuration: **Sidebar game click: Open Grid Details Panel**, **Grid cover click: Open Full Details View**, **Restore grid scroll position: enabled**. Note the view, selected game and Playnite log for failures.

## Maintainer's v0.2.4 results

The maintainer installed the paired v0.2.4 packages in Playnite on Windows and reported **12 passed, 2 failed, 1 pending** checks. The failures are pressing one Navigator row and releasing over another, and dragging within the list then releasing over another game; either can leave the list behaving as though a mouse interaction is still active. The cause is unconfirmed. Preference persistence after a full Playnite restart remains pending. These results were reported by the maintainer, not reproduced by an automated test.

## Checklist for future regression testing

- [ ] From Grid, click a sidebar game: the selected game's native Grid Details Panel opens. Repeat with sidebar action set to Full Details View.
- [ ] From Grid, click a cover: Full Details opens. Repeat with cover action set to Grid Details Panel; the grid `i` button and panel close still work.
- [ ] Inside Full Details, click several different Navigator games in succession: selection and details update, and Full Details stays open with either sidebar action.
- [ ] Enter Full Details through both a cover and the Navigator. Back to Grid preserves selection and restores scroll when filter, sort and grouping have not changed.
- [ ] Rapidly switch Grid, Full Details and native List View with a pending single click: no delayed return or stray panel opens. Native List View has no duplicate Navigator.
- [ ] Double-click a sidebar game and a grid cover: each launches only once without a delayed navigation action.
- [ ] Press on one sidebar row and release on another; drag farther than the Windows minimum drag distance; drag to scroll; release over an interactive child if present: none opens a view or panel.
- [ ] Right-click a sidebar row: Play applies to that row. Shift+F10 and the Menu key apply to the keyboard-selected row, regardless of pointer location. Grid context menus remain native.
- [ ] Search, filtering, alphabetic and grouped rows, icons and two-way selection still work. Check whether expanded groups reset on refresh and record it.
- [ ] Collapse, reopen and resize the Navigator. Restart Playnite: layout, width, grouping and both click actions persist.
- [ ] Grid panel layout modes Automatic, Always hidden and Always visible retain their defaults; an explicit request opens temporarily in Always hidden, and closing restores the configured layout after the refresh interval.
- [ ] Test settings recovery with a backed-up disposable copy: corrupt `navigator.xml`, start Playnite, confirm a `.corrupt-*` copy contains the original bytes and defaults load; save settings, restart and confirm persistence. Restore the real backup afterward.
- [ ] Artwork, logos, backgrounds, metadata and existing third-party integrations remain as in v0.2.3. No background or converter behavior should change.
