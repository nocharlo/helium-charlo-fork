using System;
using System.Collections.Generic;
using System.Globalization;
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
        internal IEnumerable<NavigatorGroupIdentity> Groups { get; set; }
        internal DateTime? LastActivity { get; set; }
        internal ulong Playtime { get; set; }
        internal DateTime? ReleaseDate { get; set; }
        internal DateTime? Added { get; set; }
        internal DateTime? Modified { get; set; }
        internal ulong PlayCount { get; set; }
        internal ulong? InstallSize { get; set; }
        internal int? UserScore { get; set; }
        internal int? CommunityScore { get; set; }
        internal int? CriticScore { get; set; }
        internal string CompletionStatus { get; set; }
    }

    internal sealed class NavigatorProjectedRow
    {
        internal Guid Id { get; set; }
        internal string Name { get; set; }
        internal string Group { get; set; }
        internal NavigatorGroupIdentity GroupIdentity { get; set; }
        internal string Key { get; set; }
        internal long ActivityTicks { get; set; }
        internal long GroupOrder { get; set; }
        internal string IconPath { get; set; }
        internal bool IsInstalled { get; set; }
        internal bool Favorite { get; set; }
        internal ulong? SortValue { get; set; }
        internal string SortText { get; set; }
    }

    internal enum NavigatorSortMode { FollowingPlaynite = 0, Name = 1, LastPlayed = 2, Playtime = 3, ReleaseDate = 4,
        DateAdded = 5, DateModified = 6, PlayCount = 7, UserScore = 8, CommunityScore = 9, CriticScore = 10,
        FavoriteStatus = 11, InstallationStatus = 12, CompletionStatus = 13, InstallationSize = 14, SortingName = 15 }

    internal static class NavigatorSortPolicy
    {
        internal static NavigatorSortMode ValidMode(int value) =>
            NavigatorOrganization.Sort(value).SortPolicy;

        internal static string Text(NavigatorSource game, NavigatorSortMode mode)
        {
            switch (mode) {
                case NavigatorSortMode.Name:
                case NavigatorSortMode.SortingName:
                    return string.IsNullOrWhiteSpace(game.SortingName) ? game.Name : game.SortingName;
                case NavigatorSortMode.CompletionStatus:
                    return game.CompletionStatus;
                default:
                    return null;
            }
        }
        private static ulong? DateValue(DateTime? value) => value.HasValue && value.Value.Year > 1 ? (ulong?)value.Value.Ticks : null;
        private static ulong? Score(int? value) => value.HasValue && value.Value >= 0 ? (ulong?)value.Value : null;

        internal static ulong? Value(NavigatorSource game, NavigatorSortMode mode, DateTime now)
        {
            switch (mode) {
                case NavigatorSortMode.LastPlayed:
                    var activity = NavigatorProjection.NormalizeActivity(game.LastActivity, now);
                    return activity.HasValue ? (ulong?)activity.Value.Ticks : null;
                case NavigatorSortMode.Playtime: return game.Playtime;
                case NavigatorSortMode.DateAdded: return DateValue(game.Added);
                case NavigatorSortMode.DateModified: return DateValue(game.Modified);
                case NavigatorSortMode.PlayCount: return game.PlayCount;
                case NavigatorSortMode.InstallationSize: return game.InstallSize;
                case NavigatorSortMode.UserScore: return Score(game.UserScore);
                case NavigatorSortMode.CommunityScore: return Score(game.CommunityScore);
                case NavigatorSortMode.CriticScore: return Score(game.CriticScore);
                case NavigatorSortMode.FavoriteStatus: return game.Favorite ? 1UL : 0UL;
                case NavigatorSortMode.InstallationStatus: return game.IsInstalled ? 1UL : 0UL;
                case NavigatorSortMode.ReleaseDate:
                    return game.ReleaseDate.HasValue && game.ReleaseDate.Value.Year > 1
                        ? (ulong?)game.ReleaseDate.Value.Ticks : null;
                default: return null;
            }
        }

        internal static IEnumerable<NavigatorSource> Order(IEnumerable<NavigatorSource> games,
            NavigatorSortMode mode, bool descending, DateTime now)
        {
            var source = games ?? Enumerable.Empty<NavigatorSource>();
            if (mode == NavigatorSortMode.FollowingPlaynite) return source;
            if (mode == NavigatorSortMode.Name || mode == NavigatorSortMode.SortingName || mode == NavigatorSortMode.CompletionStatus) {
                var keyed = source.Select(game => new { Game = game, Text = Text(game, mode) });
                return (descending
                    ? keyed.OrderBy(item => string.IsNullOrWhiteSpace(item.Text)).ThenByDescending(item => item.Text, StringComparer.CurrentCultureIgnoreCase)
                    : keyed.OrderBy(item => string.IsNullOrWhiteSpace(item.Text)).ThenBy(item => item.Text, StringComparer.CurrentCultureIgnoreCase))
                    .ThenBy(item => item.Game.Name, StringComparer.Ordinal)
                    .ThenBy(item => item.Game.Id).Select(item => item.Game);
            }
            var values = source.Select(game => new { Game = game, Key = Value(game, mode, now) });
            return (descending
                ? values.OrderBy(item => !item.Key.HasValue).ThenByDescending(item => item.Key.GetValueOrDefault())
                : values.OrderBy(item => !item.Key.HasValue).ThenBy(item => item.Key.GetValueOrDefault()))
                .ThenBy(item => string.IsNullOrWhiteSpace(item.Game.Name))
                .ThenBy(item => item.Game.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Game.Name ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.Game.Id).Select(item => item.Game);
        }
    }

    internal static class NavigatorProjection
    {
        internal static List<NavigatorProjectedRow> Build(IEnumerable<NavigatorSource> games, int grouping, Func<string, string> resolveIcon,
            DateTime? now = null, NavigatorSortMode sortMode = NavigatorSortMode.FollowingPlaynite, bool descending = false)
        {
            if (grouping == 8 || grouping == 9)
                return BuildRecentActivity(games, resolveIcon, now ?? DateTime.Now, grouping == 9, sortMode, descending);
            var rows = new List<NavigatorProjectedRow>();
            // FilteredGames already follows Playnite's current collection view order.
            var clock = now ?? DateTime.Now;
            foreach (var game in NavigatorSortPolicy.Order(games, sortMode, descending, clock)) {
                string iconPath = null;
                if (!string.IsNullOrWhiteSpace(game.Icon)) iconPath = resolveIcon(game.Icon);
                foreach (var group in Groups(game, grouping))
                    rows.Add(new NavigatorProjectedRow {
                        Id = game.Id, Name = game.Name ?? "Untitled", Key = Logic.SearchKey(game.Name),
                        Group = group.Label, GroupIdentity = group, GroupOrder = group.Order, IconPath = iconPath,
                        IsInstalled = game.IsInstalled, Favorite = game.Favorite,
                        SortValue = NavigatorSortPolicy.Value(game, sortMode, clock),
                        SortText = NavigatorSortPolicy.Text(game, sortMode)
                    });
            }
            if (grouping != 0) rows = rows.OrderBy(row => row.GroupIdentity.IsUnassigned)
                .ThenByDescending(row => row.GroupOrder)
                .ThenBy(row => row.Group, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.GroupIdentity.Key, StringComparer.Ordinal).ToList();
            return rows;
        }

        internal static IEnumerable<NavigatorGroupIdentity> Groups(NavigatorSource game, int grouping)
        {
            if (grouping == 0) return new[] { new NavigatorGroupIdentity("flat", "") };
            if (grouping == (int)NavigatorGroupPolicy.InstallationStatus)
                return new[] { new NavigatorGroupIdentity(game.IsInstalled ? "installed" : "not-installed", game.IsInstalled ? "Installed" : "Not Installed") };
            if (grouping == (int)NavigatorGroupPolicy.Favorites)
                return new[] { new NavigatorGroupIdentity(game.Favorite ? "favorite" : "not-favorite", game.Favorite ? "Favorites" : "Not Favorites") };
            if (grouping == (int)NavigatorGroupPolicy.ReleaseYear || grouping == (int)NavigatorGroupPolicy.AddedYear) {
                var date = grouping == (int)NavigatorGroupPolicy.ReleaseYear ? game.ReleaseDate : game.Added;
                var local = date.HasValue && date.Value.Kind == DateTimeKind.Utc ? date.Value.ToLocalTime() : date;
                return new[] { local.HasValue && local.Value.Year > 1
                    ? new NavigatorGroupIdentity("year:" + local.Value.Year, local.Value.Year.ToString(CultureInfo.InvariantCulture), false, local.Value.Year)
                    : NavigatorGroupIdentity.Missing("Unknown year") };
            }
            var groups = (game.Groups ?? Logic.Groups(game.GroupNames)
                .Select(name => new NavigatorGroupIdentity("name:" + name, name))).Distinct().ToArray();
            return groups.Length == 0 ? new[] { NavigatorGroupIdentity.Missing() } : groups;
        }

        private static List<NavigatorProjectedRow> BuildRecentActivity(IEnumerable<NavigatorSource> games,
            Func<string, string> resolveIcon, DateTime now, bool detailed, NavigatorSortMode sortMode, bool descending)
        {
            var rows = new List<NavigatorProjectedRow>();
            var sevenDayBoundary = now.AddDays(-7);
            foreach (var game in NavigatorSortPolicy.Order(games, sortMode, descending, now)) {
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
                    label = activity.Value.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("en-US"));
                    order = activity.Value.Year * 12L + activity.Value.Month;
                }
                rows.Add(new NavigatorProjectedRow {
                    Id = game.Id, Name = game.Name ?? "Untitled", Key = Logic.SearchKey(game.Name),
                    Group = label, GroupIdentity = new NavigatorGroupIdentity("activity:" + order, label), GroupOrder = order, ActivityTicks = activity?.Ticks ?? 0,
                    IconPath = string.IsNullOrWhiteSpace(game.Icon) ? null : resolveIcon(game.Icon),
                    IsInstalled = game.IsInstalled, Favorite = game.Favorite,
                    SortValue = NavigatorSortPolicy.Value(game, sortMode, now),
                    SortText = NavigatorSortPolicy.Text(game, sortMode)
                });
            }
            // Stable grouping keeps the chosen row order within each chronological bucket.
            var chronologicalGroups = rows.OrderByDescending(row => row.GroupOrder);
            return (sortMode == NavigatorSortMode.FollowingPlaynite
                ? chronologicalGroups.ThenByDescending(row => row.ActivityTicks)
                : chronologicalGroups).ToList();
        }
        internal static DateTime? NormalizeActivity(DateTime? recorded, DateTime now)
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
