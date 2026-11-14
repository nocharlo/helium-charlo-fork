using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HeliumHybrid
{
    internal static class NavigatorMenuIcons
    {
        internal static void Set(FrameworkElement owner, MenuItem item, string key)
        {
            item.Icon = Create(owner.TryFindResource(key));
        }

        internal static FrameworkElement Create(object resource)
        {
            if (resource is ImageSource image)
                return new Image { Source = image, Width = 16, Height = 16, Stretch = Stretch.Uniform };
            if (!(resource is TextBlock glyph)) return null;

            // Theme dictionaries may share the original element across different menus.
            var icon = new TextBlock {
                Text = glyph.Text, FontFamily = glyph.FontFamily, FontStyle = glyph.FontStyle,
                FontWeight = glyph.FontWeight, FontStretch = glyph.FontStretch, FontSize = 16,
                TextAlignment = TextAlignment.Center, Width = 16, VerticalAlignment = VerticalAlignment.Center
            };
            if (glyph.ReadLocalValue(TextBlock.ForegroundProperty) != DependencyProperty.UnsetValue)
                icon.Foreground = glyph.Foreground;
            return icon;
        }
    }
}
