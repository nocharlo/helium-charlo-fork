using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;
using System.Xml.Serialization;

namespace HeliumNavigator
{
    public sealed class CompletionStatusIndicatorPreference
    {
        public string StatusId { get; set; }
        public string Color { get; set; }
        public string Enabled { get; set; }

        [XmlIgnore]
        internal bool IsEnabled
        {
            get => bool.TryParse(Enabled, out var enabled) && enabled;
            set => Enabled = value ? "true" : "false";
        }
    }

    internal static class CompletionStatusIndicators
    {
        internal const string DefaultColor = "#67B3FF";
        // Playnite creates its built-in statuses with library-specific IDs. The SDK's
        // configurable assignment roles do not identify the original built-in statuses.
        internal static IReadOnlyList<KeyValuePair<string, string>> Palette { get; } = Array.AsReadOnly(new[] {
            new KeyValuePair<string, string>("Not Played", "#8792A2"),
            new KeyValuePair<string, string>("Plan to Play", "#7599D5"),
            new KeyValuePair<string, string>("Played", "#9C91B9"),
            new KeyValuePair<string, string>("Playing", "#58B4E5"),
            new KeyValuePair<string, string>("On Hold", "#D5A052"),
            new KeyValuePair<string, string>("Abandoned", "#C97076"),
            new KeyValuePair<string, string>("Beaten", "#4FB5A5"),
            new KeyValuePair<string, string>("Completed", "#70B981")
        });

        internal static Color? ParseColor(string value)
        {
            if (value?.Length != 7 || value[0] != '#' ||
                !int.TryParse(value.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
                return null;
            return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }

        internal static List<CompletionStatusIndicatorPreference> Normalize(
            IEnumerable<CompletionStatusIndicatorPreference> preferences)
        {
            var result = new List<CompletionStatusIndicatorPreference>();
            if (preferences == null) return result;

            var seen = new HashSet<Guid>();
            foreach (var option in preferences) {
                if (!Guid.TryParse(option?.StatusId, out var id) || id == Guid.Empty) continue;
                var color = string.IsNullOrWhiteSpace(option.Color) ? null : option.Color.Trim();
                if (!string.IsNullOrEmpty(color) && !ParseColor(color).HasValue) continue;
                if (!option.IsEnabled && string.IsNullOrEmpty(color)) continue;
                if (!seen.Add(id)) continue;
                result.Add(new CompletionStatusIndicatorPreference {
                    StatusId = id.ToString("D"), Color = color?.ToUpperInvariant(), IsEnabled = option.IsEnabled
                });
            }
            return result;
        }

        internal static Dictionary<Guid, Brush> CreateBrushes(Preferences prefs)
        {
            var brushes = new Dictionary<Guid, Brush>();
            if (!prefs.ShowCompletionStatusIndicators) return brushes;
            foreach (var option in Normalize(prefs.CompletionStatusIndicatorColors)) {
                if (!option.IsEnabled) continue;
                var brush = new SolidColorBrush(ParseColor(option.Color) ?? ParseColor(DefaultColor).Value);
                brush.Freeze();
                brushes.Add(Guid.Parse(option.StatusId), brush);
            }
            return brushes;
        }
    }
}
