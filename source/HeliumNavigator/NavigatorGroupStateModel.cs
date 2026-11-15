using System;
using System.Collections.Generic;

namespace HeliumNavigator
{
    // Kept outside WPF containers so virtualization and view replacement cannot erase it.
    internal sealed class NavigatorGroupState
    {
        private readonly Dictionary<string, bool> expanded = new Dictionary<string, bool>(StringComparer.Ordinal);
        internal int Mode { get; set; }
        private static string Key(int mode, string name) => mode + ":" + (name ?? string.Empty);
        internal bool IsExpanded(int mode, string name) =>
            !expanded.TryGetValue(Key(mode, name), out var value) || value;
        internal void SetExpanded(int mode, string name, bool value) => expanded[Key(mode, name)] = value;
        internal bool IsExpanded(int mode, NavigatorGroupIdentity group)
        {
            if (group == null) return true;
            if (expanded.TryGetValue(Key(mode, "id:" + group.Key), out var value)) return value;
            // Legacy state was in-memory and label-based. Preserve unrelated groups on transition.
            return IsExpanded(mode, group.LegacyLabel);
        }
        internal void SetExpanded(int mode, NavigatorGroupIdentity group, bool value)
        { if (group != null) expanded[Key(mode, "id:" + group.Key)] = value; }

        internal T NextVisibleOccurrence<T>(int mode, IEnumerable<T> rows,
            Func<T, NavigatorGroupIdentity> groupOf, T current, NavigatorGroupIdentity header, bool down) where T : class
        {
            var pastAnchor = current == null && header == null;
            T previous = null;
            foreach (var row in rows) {
                var group = groupOf(row);
                if (!pastAnchor && header != null && Equals(header, group)) {
                    if (!down) return previous;
                    pastAnchor = true;
                }
                if (ReferenceEquals(row, current)) {
                    if (!down) return previous;
                    pastAnchor = true;
                    continue;
                }
                if (!IsExpanded(mode, group)) continue;
                if (pastAnchor && down) return row;
                previous = row;
            }
            return current == null && header == null && !down ? previous : null;
        }

        internal T FindRevealOccurrence<T>(int mode, IEnumerable<T> occurrences,
            Func<T, NavigatorGroupIdentity> groupOf, NavigatorGroupIdentity previousGroup) where T : class
        {
            T first = null;
            T firstExpanded = null;
            foreach (var occurrence in occurrences) {
                var group = groupOf(occurrence);
                if (previousGroup != null && Equals(previousGroup, group)) return occurrence;
                if (first == null) first = occurrence;
                if (firstExpanded == null && IsExpanded(mode, group)) firstExpanded = occurrence;
            }
            return firstExpanded ?? first;
        }
    }
}
