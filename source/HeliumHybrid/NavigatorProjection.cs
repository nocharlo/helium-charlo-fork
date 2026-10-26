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
        internal DateTime? LastActivity { get; set; }
    }

    internal sealed class NavigatorProjectedRow
    {
        internal Guid Id { get; set; }
        internal string Name { get; set; }
        internal string Group { get; set; }
        internal string Key { get; set; }
        internal long ActivityTicks { get; set; }
        internal long GroupOrder { get; set; }
        internal string IconPath { get; set; }
        internal bool IsInstalled { get; set; }
        internal bool Favorite { get; set; }
    }

    internal static class NavigatorProjection
    {
        internal static List<NavigatorProjectedRow> Build(IEnumerable<NavigatorSource> games, int grouping, Func<string, string> resolveIcon, DateTime? now = null)
        {
            if (grouping == 8 || grouping == 9) return BuildRecentActivity(games, resolveIcon, now ?? DateTime.Now, grouping == 9);
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

        private static List<NavigatorProjectedRow> BuildRecentActivity(IEnumerable<NavigatorSource> games,
            Func<string, string> resolveIcon, DateTime now, bool detailed)
        {
            var rows = new List<NavigatorProjectedRow>();
            var sevenDayBoundary = now.AddDays(-7);
            foreach (var game in games ?? Enumerable.Empty<NavigatorSource>()) {
                var activity = NormalizeActivity(game.LastActivity, now);
                string label;
                long order;
                if (!activity.HasValue) { label = "No Recorded Activity"; order = 0; }
                else if (activity.Value >= sevenDayBoundary) { label = "Last 7 Days"; order = long.MaxValue; }
                else if (activity.Value.Year == now.Year && activity.Value.Month == now.Month) {
                    label = "Earlier This Month"; order = long.MaxValue - 1;
                } else if (!detailed && activity.Value.Year < now.Year) {
                    label = activity.Value.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    order = activity.Value.Year * 12L;
                } else {
                    label = activity.Value.ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
                    order = activity.Value.Year * 12L + activity.Value.Month;
                }
                rows.Add(new NavigatorProjectedRow {
                    Id = game.Id, Name = game.Name ?? "Untitled", Key = Logic.SearchKey(game.Name),
                    Group = label, GroupOrder = order, ActivityTicks = activity?.Ticks ?? 0,
                    IconPath = string.IsNullOrWhiteSpace(game.Icon) ? null : resolveIcon(game.Icon),
                    IsInstalled = game.IsInstalled, Favorite = game.Favorite
                });
            }
            return rows.OrderByDescending(row => row.GroupOrder)
                .ThenByDescending(row => row.ActivityTicks).ToList();
        }
        private static DateTime? NormalizeActivity(DateTime? recorded, DateTime now)
        {
            if (!recorded.HasValue) return null;
            try {
                var value = recorded.Value.Kind == DateTimeKind.Utc ? recorded.Value.ToLocalTime() : recorded.Value;
                return value.Year < 1900 || value > now ? (DateTime?)null : value;
            } catch (ArgumentOutOfRangeException) { return null; }
        }
        internal static string SearchKey(string text) => Logic.SearchKey(text);
        internal static bool Matches(string rowKey, string searchKey) => rowKey.Contains(searchKey);
        internal static int DistinctGameCount(IEnumerable<Guid> ids) => ids.Distinct().Count();
    }
}
