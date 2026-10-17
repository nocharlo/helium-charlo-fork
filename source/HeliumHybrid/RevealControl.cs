using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Playnite.SDK.Controls;
namespace HeliumHybrid
{
    public sealed class RevealControl : PluginUserControl
    {
        public RevealControl(HybridPlugin plugin)
        {
            var button = new Button {
                Content = ">>", Width = 32, Height = 28, FontSize = 11,
                ToolTip = "Expandir lista de jogos", Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(35, 48, 62)),
                Foreground = Brushes.White
            };
            button.Click += (s, e) => plugin.SetCollapsed(false);
            Content = button;
        }
    }
}
