using System;
using System.Collections.Generic;
using System.Linq;

namespace HeliumHybrid
{
    // Plain data used between the Playnite API and the Navigator's WPF view.
    internal sealed class NavigatorSource
    {
        internal Guid Id { get; set; }
        internal string Name { get; set; }
        internal string SortingName { get; set; }
        internal string Icon { get; set; }
        internal bool IsInstalled { get; set; }
        internal bool Favorite { get; set; }
        internal IEnumerable<string> GroupNames { get; set; }
    }

    internal sealed class NavigatorProjectedRow
    {
        internal Guid Id { get; set; }
        internal string Name { get; set; }
        internal string Group { get; set; }
        internal string Key { get; set; }
        internal string IconPath { get; set; }
        internal bool IsInstalled { get; set; }
        internal bool Favorite { get; set; }
    }

    internal static class NavigatorProjection
    {
        internal static List<NavigatorProjectedRow> Build(IEnumerable<NavigatorSource> games, int grouping, Func<string, string> resolveIcon)
        {
            var rows = new List<NavigatorProjectedRow>();
            // FilteredGames already follows Playnite's current collection view order.
            foreach (var game in games ?? Enumerable.Empty<NavigatorSource>()) {
                string iconPath = null;
                if (!string.IsNullOrWhiteSpace(game.Icon)) iconPath = resolveIcon(game.Icon);
                foreach (var group in grouping == 0 ? new[] { "" } : Logic.Groups(game.GroupNames))
                    rows.Add(new NavigatorProjectedRow {
                        Id = game.Id, Name = game.Name ?? "Untitled", Key = Logic.SearchKey(game.Name),
                        Group = group, IconPath = iconPath,
                        IsInstalled = game.IsInstalled, Favorite = game.Favorite
                    });
            }
            if (grouping != 0) rows = rows.OrderBy(row => row.Group, StringComparer.CurrentCultureIgnoreCase).ToList();
            return rows;
        }

        internal static string SearchKey(string text) => Logic.SearchKey(text);
        internal static bool Matches(string rowKey, string searchKey) => rowKey.Contains(searchKey);
        internal static int DistinctGameCount(IEnumerable<Guid> ids) => ids.Distinct().Count();
    }
}
