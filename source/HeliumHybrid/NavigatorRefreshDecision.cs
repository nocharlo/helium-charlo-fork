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

    internal static class NavigatorRefreshDecision
    {
        internal static Guid[] SortedIds(IEnumerable<Guid> ids)
        {
            return (ids ?? Enumerable.Empty<Guid>()).OrderBy(id => id).ToArray();
        }

        internal static bool MembershipChanged(Guid[] current, Guid[] previous)
        {
            return !(current ?? new Guid[0]).SequenceEqual(previous ?? new Guid[0]);
        }

        // This deliberately preserves the v0.2.5 trigger. Native sort/group state is observed
        // separately and must not start rebuilding the list until the parity change is tested.
        internal static bool ShouldRebuild(bool dirty, bool membershipChanged)
        {
            return dirty || membershipChanged;
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
