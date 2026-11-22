using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace HeliumNavigator
{
    public static class Logic
    {
        public static string SearchKey(string value)
        {
            var result = new StringBuilder();
            foreach (var c in (value ?? "").Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) result.Append(char.ToUpperInvariant(c));
            return result.ToString().Normalize(NormalizationForm.FormC);
        }
        public static IEnumerable<string> Groups(IEnumerable<string> names)
        {
            var values = (names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();
            return values; // Missing metadata is represented explicitly by NavigatorProjection.
        }
        public static int RecentActivityStyle(int grouping, int storedStyle, bool specified)
        {
            if (!specified && grouping == 4) return 1; // Preserve Detailed for existing Recent Activity users.
            return storedStyle == 1 ? 1 : 0;
        }
        public static int ToggleRecentGrouping(int current, int previous) =>
            current == 4 ? (previous >= 0 && previous != 4 ? previous : 0) : 4;
        public static int? ShortcutGroupingOverride(int saved, int selected) =>
            saved == selected ? (int?)null : selected;
        public static bool ShouldRestoreSelection(Guid desired, IEnumerable<Guid> current, Func<bool> stillVisible) =>
            desired != Guid.Empty && (current == null || !current.Any()) && stillVisible();
        internal static bool IsBackReleaseOrCommand(int message, long wParam, long lParam)
        {
            const int xButtonUp = 0x020C, appCommand = 0x0319;
            const int xButton1 = 1, browseBack = 1, commandMask = 0x0FFF;
            return message == xButtonUp && ((wParam >> 16) & 0xFFFF) == xButton1 ||
                message == appCommand && ((lParam >> 16) & commandMask) == browseBack;
        }
        internal static bool RouteLibraryBack(bool inDetails, bool openedFromGrid,
            Func<bool> returnToGrid, Func<bool> dismissGridPanel) =>
            inDetails ? openedFromGrid && returnToGrid() : dismissGridPanel();
        public static double ClampWidth(double width) { return double.IsNaN(width) || double.IsInfinity(width) ? 230 : Math.Max(180, Math.Min(380, width)); }
        // The available library width is measured in WPF device-independent pixels.
        public static bool Compact(int mode, bool wasCompact, double width)
        {
            if (mode == 1) return true;
            if (mode == 2) return false;
            return width < (wasCompact ? 980 : 900);
        }
    }
}
