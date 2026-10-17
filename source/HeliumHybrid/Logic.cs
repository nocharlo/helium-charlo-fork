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
            return values.Length == 0 ? new[] { "No group" } : values;
        }
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
