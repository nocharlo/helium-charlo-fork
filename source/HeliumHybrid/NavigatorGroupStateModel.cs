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
    }
}
