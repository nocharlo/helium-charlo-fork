using System;
using System.Collections.Generic;

namespace HeliumHybrid
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
    }
}
