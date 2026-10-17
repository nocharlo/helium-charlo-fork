using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace HeliumHybrid
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
            return values.Length == 0 ? new[] { "Sem grupo" } : values;
        }
        public static double ClampWidth(double width) { return double.IsNaN(width) || double.IsInfinity(width) ? 230 : Math.Max(180, Math.Min(380, width)); }
        // WPF ActualWidth is already in device-independent pixels: no manual DPI multiplication.
        public static bool Compact(int mode, bool wasCompact, double mainWidth, double detailsWidth)
        {
            if (mode == 1) return true;
            if (mode == 2) return false;
            if (double.IsNaN(mainWidth) || mainWidth <= 0) return wasCompact;
            var target = 500 + Math.Max(360, Math.Min(620, detailsWidth)) + 18;
            return wasCompact ? mainWidth < target + 44 : mainWidth < target - 44;
        }
    }
}
