# Helium Hybrid 0.1.0 — Windows Testing Checklist

This checklist was written for testing the release in a real Playnite installation. **None of the items below were marked as passed in the original Linux build environment.** The user subsequently confirmed that the basic sidebar, grid, and selection worked on their Windows setup; the full checklist has not been formally completed.

1. Install both packages. Confirm that the original KKCD theme and Helium Hybrid are listed separately.
2. Open Grid View and check that the game list, covers, and details panel are visible. Resize the window and check whether the main content still has enough usable space. Reduce the details-panel width if necessary.
3. Click three games in the sidebar. Check the selected cover, game title, logo, and background. Close the details panel, then select another game from the sidebar; the panel should reopen.
4. Select a game farther down in the grid. Check that the matching entry becomes highlighted and is brought into view in the sidebar. Scroll the sidebar and confirm that the grid's scroll position does not change.
5. Use Playnite's native search and filters. Allow up to 1.5 seconds, then confirm that the sidebar reflects the filtered game set.
6. Search for a game name with and without accents in the sidebar search. Confirm that local search does not modify the grid or the global filters.
7. Group games by series, category, and platform. Expand and collapse group headers. Check games assigned to multiple groups and games with no group assignment.
8. Drag the sidebar's edge to resize it, collapse it, and restart Playnite. Check whether width and grouping preferences are restored.
9. Test the native grid context menu and the sidebar's **Play** menu. Use Enter or double-click on a game you actually want to launch. Double-clicking empty space or a group header should not launch a game.
10. Check Extra Metadata Loader, ThemeExtras, ThemeModifier, Game Relations, GameActivity, and HowLongToBeat. Process only the Hybrid variant with Details to Grid View Converter if prompted. Switch back to the original KKCD theme to confirm that rollback works.

## Reporting a problem

Capture the entire Playnite window and include the Playnite version, window size, Windows display scaling, the action that caused the problem, and the relevant section of `playnite.log` containing **“Helium Hybrid”**.

For implementation notes and the original build-time checks, see [TECHNICAL-REPORT.md](TECHNICAL-REPORT.md) and [VALIDATION.json](VALIDATION.json).
