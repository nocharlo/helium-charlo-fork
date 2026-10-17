# Windows acceptance checklist — v0.2.3

Test with the working v0.2.2 settings backed up. Record failures with layout mode, click action and Playnite log.

- [ ] Theme selector shows exactly **Helium (charlo fork) v0.2.3**.
- [ ] Add-on management shows **Helium Navigator v0.2.3**, without a conflicting Navigator installation.
- [ ] Settings migration: enabled old checkbox → Full Details View; disabled → Grid Details Panel.
- [ ] Sidebar click → Full Details View; native list hidden and Back to Grid available.
- [ ] Sidebar click → Grid Details Panel for the selected game.
- [ ] Grid cover click → Full Details View.
- [ ] Grid cover click → Grid Details Panel for the selected game.
- [ ] Existing grid `i` button and native panel close control work.
- [ ] Automatic layout responds to width changes and explicit panel requests.
- [ ] Always visible layout retains its default with both click actions.
- [ ] Always hidden layout remains hidden by default.
- [ ] Always hidden: explicit panel request opens temporarily; close restores hidden layout.
- [ ] Back to Grid preserves selection and, where possible, grid scroll position.
- [ ] From full Details View, clicking another sidebar game in Grid Details Panel mode switches to Grid and opens that game.
- [ ] Double-click sidebar game and grid cover launches normally without unintended navigation.
- [ ] Right-click context menus, keyboard navigation and native shortcuts work.
- [ ] Selection synchronizes between Navigator and grid in both directions.
- [ ] Collapse sidebar fully and reopen with the floating button; resizing and search work.
- [ ] Restart Playnite: independent selectors, layout, width and collapsed state persist.
- [ ] Native List View has no duplicate Navigator.
- [ ] Backgrounds, logos, artwork, statistics and third-party integrations work.
- [ ] No visual regressions in native Grid and Details views or converter panel placement.
- [ ] All project-authored UI shown by the update is in English.
