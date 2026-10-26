using System;
using System.Collections.Generic;
using System.Linq;

namespace HeliumHybrid
{
    // Describes the native view for diagnostics; it does not control Navigator ordering.
    internal sealed class NavigatorViewSnapshot
    {
        internal string SortOrder { get; set; }
        internal string SortDirection { get; set; }
        internal string Grouping { get; set; }
        internal Guid PresetId { get; set; }
        internal bool CompletionStatusFilterActive { get; set; }
        internal int NavigatorGrouping { get; set; }
    }

    internal enum NavigatorRowChange { None, Visual, Structural }

    internal static class NavigatorRefreshDecision
    {
        internal static NavigatorRowChange ClassifyRows(IEnumerable<NavigatorProjectedRow> before,
            IEnumerable<NavigatorProjectedRow> after)
        {
            var oldRows = (before ?? Enumerable.Empty<NavigatorProjectedRow>()).ToArray();
            var newRows = (after ?? Enumerable.Empty<NavigatorProjectedRow>()).ToArray();
            if (oldRows.Length != newRows.Length) return NavigatorRowChange.Structural;
            var visual = false;
            for (var i = 0; i < oldRows.Length; i++) {
                var oldRow = oldRows[i]; var newRow = newRows[i];
                if (oldRow.Id != newRow.Id || oldRow.Name != newRow.Name ||
                    oldRow.Key != newRow.Key || oldRow.Group != newRow.Group ||
                    oldRow.IconPath != newRow.IconPath ||
                    oldRow.ActivityTicks != newRow.ActivityTicks || oldRow.GroupOrder != newRow.GroupOrder) return NavigatorRowChange.Structural;
                visual |= oldRow.IsInstalled != newRow.IsInstalled || oldRow.Favorite != newRow.Favorite;
            }
            return visual ? NavigatorRowChange.Visual : NavigatorRowChange.None;
        }
        // Explicit Navigator grouping wins. The SDK exposes group fields but not native group containers.
        internal static int EffectiveGrouping(int navigatorGrouping, string nativeGrouping, int recentActivityGroupingStyle = 0)
        {
            if (navigatorGrouping == 4) return recentActivityGroupingStyle == 1 ? 9 : 8;
            if (navigatorGrouping != 0) return navigatorGrouping;
            switch (nativeGrouping) {
                case "CompletionStatus": return 4;
                case "Series": return 5;
                case "Category": return 6;
                case "Platform": return 7;
                default: return 0;
            }
        }
        internal static Guid[] SortedIds(IEnumerable<Guid> ids)
        {
            return (ids ?? Enumerable.Empty<Guid>()).OrderBy(id => id).ToArray();
        }

        internal static bool MembershipChanged(Guid[] current, Guid[] previous)
        {
            return !(current ?? new Guid[0]).SequenceEqual(previous ?? new Guid[0]);
        }

        internal static bool OrderChanged(Guid[] current, Guid[] previous)
        {
            return !(current ?? new Guid[0]).SequenceEqual(previous ?? new Guid[0]);
        }

        internal static bool ShouldRebuild(bool dirty, bool membershipChanged, bool orderChanged,
            bool viewStateChanged)
        {
            return dirty || membershipChanged || orderChanged || viewStateChanged;
        }

        internal static bool ViewStateChanged(NavigatorViewSnapshot current, NavigatorViewSnapshot previous)
        {
            if (current == null || previous == null) return !ReferenceEquals(current, previous);
            return !string.Equals(current.SortOrder, previous.SortOrder, StringComparison.Ordinal) ||
                !string.Equals(current.SortDirection, previous.SortDirection, StringComparison.Ordinal) ||
                !string.Equals(current.Grouping, previous.Grouping, StringComparison.Ordinal) ||
                current.PresetId != previous.PresetId ||
                current.CompletionStatusFilterActive != previous.CompletionStatusFilterActive ||
                current.NavigatorGrouping != previous.NavigatorGrouping;
        }
    }
}
