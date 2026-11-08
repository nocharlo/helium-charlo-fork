using Playnite.SDK.Models;

namespace HeliumHybrid
{
    // Explicit SDK 6.11.0 mappings. A missing mapping leaves that native axis alone.
    internal static class NavigatorNativeOrganization
    {
        internal static GroupableField? Group(int mode)
        {
            switch ((NavigatorGroupingMode)mode) {
                case NavigatorGroupingMode.None: return GroupableField.None;
                case NavigatorGroupingMode.Series: return GroupableField.Series;
                case NavigatorGroupingMode.Category: return GroupableField.Category;
                case NavigatorGroupingMode.Platform: return GroupableField.Platform;
                case NavigatorGroupingMode.Genre: return GroupableField.Genre;
                case NavigatorGroupingMode.Tags: return GroupableField.Tag;
                case NavigatorGroupingMode.Developer: return GroupableField.Developer;
                case NavigatorGroupingMode.Publisher: return GroupableField.Publisher;
                case NavigatorGroupingMode.Features: return GroupableField.Feature;
                case NavigatorGroupingMode.Library: return GroupableField.Library;
                case NavigatorGroupingMode.Source: return GroupableField.Source;
                case NavigatorGroupingMode.Region: return GroupableField.Region;
                case NavigatorGroupingMode.AgeRating: return GroupableField.AgeRating;
                case NavigatorGroupingMode.CompletionStatus: return GroupableField.CompletionStatus;
                case NavigatorGroupingMode.InstallationStatus: return GroupableField.InstallationStatus;
                case NavigatorGroupingMode.ReleaseYear: return GroupableField.ReleaseYear;
                default: return null;
            }
        }

        internal static SortOrder? Sort(NavigatorSortMode mode)
        {
            switch (mode) {
                case NavigatorSortMode.Name: return SortOrder.Name;
                case NavigatorSortMode.LastPlayed: return SortOrder.LastActivity;
                case NavigatorSortMode.Playtime: return SortOrder.Playtime;
                case NavigatorSortMode.ReleaseDate: return SortOrder.ReleaseDate;
                case NavigatorSortMode.DateAdded: return SortOrder.Added;
                case NavigatorSortMode.DateModified: return SortOrder.Modified;
                case NavigatorSortMode.PlayCount: return SortOrder.PlayCount;
                case NavigatorSortMode.UserScore: return SortOrder.UserScore;
                case NavigatorSortMode.CommunityScore: return SortOrder.CommunityScore;
                case NavigatorSortMode.CriticScore: return SortOrder.CriticScore;
                case NavigatorSortMode.FavoriteStatus: return SortOrder.Favorite;
                case NavigatorSortMode.InstallationStatus: return SortOrder.IsInstalled;
                case NavigatorSortMode.CompletionStatus: return SortOrder.CompletionStatus;
                case NavigatorSortMode.InstallationSize: return SortOrder.InstallSize;
                // Playnite has no separate SortingName sort enum. Its Name sort uses
                // Playnite's own ordering semantics; keep this explicit mode local.
                default: return null;
            }
        }

        internal static SortOrderDirection Direction(bool descending) =>
            descending ? SortOrderDirection.Descending : SortOrderDirection.Ascending;

        internal static FilterPreset SortPreset(FilterPresetSettings current, SortOrder order,
            SortOrderDirection direction, GroupableField grouping)
        {
            var preset = NavigatorFilterComposer.TemporaryPreset(current);
            preset.SortingOrder = order;
            preset.SortingOrderDirection = direction;
            preset.GroupingOrder = grouping;
            return preset;
        }
    }
}
