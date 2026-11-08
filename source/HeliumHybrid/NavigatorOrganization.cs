using System;
using System.Collections.Generic;
using System.Linq;

namespace HeliumHybrid
{
    // Persisted IDs 0-4 are the v0.3.0 contract. Effective IDs are a separate namespace.
    internal enum NavigatorGroupingMode
    {
        FollowingPlaynite = 0, Series = 1, Category = 2, Platform = 3, RecentActivity = 4,
        None = 5, Genre = 6, Tags = 7, Developer = 8, Publisher = 9, Features = 10,
        Library = 11, Source = 12, Region = 13, AgeRating = 14, CompletionStatus = 15,
        InstallationStatus = 16, Favorites = 17, ReleaseYear = 18, AddedYear = 19, Default = 20
    }
    internal enum NavigatorGroupPolicy
    {
        None = 0, Series = 1, Category = 2, Platform = 3, CompletionStatus = 4,
        NativeSeries = 5, NativeCategory = 6, NativePlatform = 7,
        RecentHybrid = 8, RecentDetailed = 9, Genre = 10, Tags = 11,
        Developer = 12, Publisher = 13, Features = 14, Library = 15, Source = 16,
        Region = 17, AgeRating = 18, InstallationStatus = 19, Favorites = 20,
        ReleaseYear = 21, AddedYear = 22
    }
    internal sealed class OrganizationMode
    {
        public int Id { get; }
        public string Label { get; }
        public string Category { get; }
        public bool IsGrouping { get; }
        public bool DefaultVisible { get; }
        public int Order { get; }
        internal NavigatorGroupPolicy GroupPolicy { get; }
        internal NavigatorSortMode SortPolicy => (NavigatorSortMode)Id;
        internal OrganizationMode(int id, string label, string category, bool grouping, bool visible, NavigatorGroupPolicy policy = NavigatorGroupPolicy.None)
        { Id = id; Label = label; Category = category; IsGrouping = grouping; DefaultVisible = visible; Order = id; GroupPolicy = policy; }
        public override string ToString() => Label;
    }
    internal static class NavigatorOrganization
    {
        private static OrganizationMode G(NavigatorGroupingMode id, string label, string category, NavigatorGroupPolicy policy, bool visible = false) =>
            new OrganizationMode((int)id, label, category, true, visible, policy);
        private static OrganizationMode S(NavigatorSortMode id, string label, string category, bool visible = false) =>
            new OrganizationMode((int)id, label, category, false, visible);
        internal static readonly OrganizationMode[] Groups = {
            G(NavigatorGroupingMode.Default, "Default", "Basic", NavigatorGroupPolicy.None, true),
            G(NavigatorGroupingMode.FollowingPlaynite, "Following Playnite", "Basic", NavigatorGroupPolicy.None, true),
            G(NavigatorGroupingMode.Series, "Series", "Metadata", NavigatorGroupPolicy.Series, true),
            G(NavigatorGroupingMode.Category, "Category", "Metadata", NavigatorGroupPolicy.Category, true),
            G(NavigatorGroupingMode.Platform, "Platform", "Metadata", NavigatorGroupPolicy.Platform, true),
            G(NavigatorGroupingMode.RecentActivity, "Recent Activity", "Activity", NavigatorGroupPolicy.RecentHybrid, true),
            G(NavigatorGroupingMode.None, "No Grouping", "Basic", NavigatorGroupPolicy.None, true),
            G(NavigatorGroupingMode.Genre, "Genre", "Metadata", NavigatorGroupPolicy.Genre, true),
            G(NavigatorGroupingMode.Tags, "Tags", "Metadata", NavigatorGroupPolicy.Tags),
            G(NavigatorGroupingMode.Developer, "Developer", "Metadata", NavigatorGroupPolicy.Developer),
            G(NavigatorGroupingMode.Publisher, "Publisher", "Metadata", NavigatorGroupPolicy.Publisher),
            G(NavigatorGroupingMode.Features, "Features", "Metadata", NavigatorGroupPolicy.Features),
            G(NavigatorGroupingMode.Library, "Library", "Metadata", NavigatorGroupPolicy.Library),
            G(NavigatorGroupingMode.Source, "Source", "Metadata", NavigatorGroupPolicy.Source),
            G(NavigatorGroupingMode.Region, "Region", "Metadata", NavigatorGroupPolicy.Region),
            G(NavigatorGroupingMode.AgeRating, "Age Rating", "Metadata", NavigatorGroupPolicy.AgeRating),
            G(NavigatorGroupingMode.CompletionStatus, "Completion Status", "Status", NavigatorGroupPolicy.CompletionStatus, true),
            G(NavigatorGroupingMode.InstallationStatus, "Installation Status", "Status", NavigatorGroupPolicy.InstallationStatus),
            G(NavigatorGroupingMode.Favorites, "Favorites", "Status", NavigatorGroupPolicy.Favorites),
            G(NavigatorGroupingMode.ReleaseYear, "Release Year", "Dates", NavigatorGroupPolicy.ReleaseYear),
            G(NavigatorGroupingMode.AddedYear, "Date Added (Year)", "Dates", NavigatorGroupPolicy.AddedYear)
        };
        internal static readonly OrganizationMode[] Sorts = {
            S(NavigatorSortMode.Default, "Default", "Basic", true),
            S(NavigatorSortMode.FollowingPlaynite, "Following Playnite", "Basic", true),
            S(NavigatorSortMode.Name, "Name", "Basic", true),
            S(NavigatorSortMode.LastPlayed, "Last Played", "Activity", true),
            S(NavigatorSortMode.Playtime, "Playtime", "Activity", true),
            S(NavigatorSortMode.ReleaseDate, "Release Date", "Dates", true),
            S(NavigatorSortMode.DateAdded, "Date Added", "Dates", true),
            S(NavigatorSortMode.DateModified, "Date Modified", "Dates"),
            S(NavigatorSortMode.PlayCount, "Play Count", "Activity", true),
            S(NavigatorSortMode.UserScore, "User Score", "Ratings", true),
            S(NavigatorSortMode.CommunityScore, "Community Score", "Ratings"),
            S(NavigatorSortMode.CriticScore, "Critic Score", "Ratings"),
            S(NavigatorSortMode.FavoriteStatus, "Favorite Status", "Status"),
            S(NavigatorSortMode.InstallationStatus, "Installation Status", "Status"),
            S(NavigatorSortMode.CompletionStatus, "Completion Status", "Status"),
            S(NavigatorSortMode.InstallationSize, "Installation Size", "Storage"),
            S(NavigatorSortMode.SortingName, "Sorting Name", "Basic")
        };
        internal static OrganizationMode Group(int id) => Groups.FirstOrDefault(m => m.Id == id) ?? Groups.First(m => m.Id == 0);
        internal static OrganizationMode Sort(int id) => Sorts.FirstOrDefault(m => m.Id == id) ?? Sorts.First(m => m.Id == 0);
        internal static List<int> Normalize(IEnumerable<int> ids, bool grouping)
        {
            var catalog = grouping ? Groups : Sorts;
            var result = ids == null ? catalog.Where(m => m.DefaultVisible).Select(m => m.Id).ToList()
                : ids.Where(id => catalog.Any(m => m.Id == id)).Distinct().ToList();
            if (result.Count == 0) result.Add(0);
            return result;
        }
        internal static IEnumerable<OrganizationMode> Visible(IEnumerable<int> ids, bool grouping, int active)
        {
            var catalog = grouping ? Groups : Sorts;
            var visible = Normalize(ids, grouping);
            if (!visible.Contains(active) && catalog.Any(m => m.Id == active)) visible.Add(active);
            return visible.Select(id => catalog.First(m => m.Id == id));
        }
        internal static List<int> Move(IEnumerable<int> source, int id, int delta)
        {
            var ids = source.ToList(); var index = ids.IndexOf(id); var target = index + delta;
            if (index >= 0 && target >= 0 && target < ids.Count) {
                ids[index] = ids[target]; ids[target] = id;
            }
            return ids;
        }
    }

    // WPF groups by this value, never by a display label. Real "None" is not missing metadata.
    public sealed class NavigatorGroupIdentity : IEquatable<NavigatorGroupIdentity>
    {
        public string Key { get; }
        public string Label { get; }
        public bool IsUnassigned { get; }
        public long Order { get; }
        internal string LegacyLabel { get; }
        internal NavigatorGroupIdentity(string key, string label, bool missing = false, long order = 0, string legacy = null)
        { Key = key; Label = label; IsUnassigned = missing; Order = order; LegacyLabel = legacy ?? label; }
        internal static NavigatorGroupIdentity Missing(string label = "None") => new NavigatorGroupIdentity("missing", label, true, 0, "No group");
        public bool Equals(NavigatorGroupIdentity other) => other != null && Key == other.Key;
        public override bool Equals(object obj) => Equals(obj as NavigatorGroupIdentity);
        public override int GetHashCode() => Key.GetHashCode();
        public override string ToString() => Label;
    }
}
