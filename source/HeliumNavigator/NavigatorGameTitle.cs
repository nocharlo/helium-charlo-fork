using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace HeliumNavigator
{
    // A non-interactive label beside the row avoids moving the mouse into a new input window.
    public sealed class NavigatorGameTitle : TextBlock
    {
        private ListBoxItem row;
        private ListBox list;
        private Window window;
        private Popup expanded;

        public NavigatorGameTitle()
        {
            Loaded += Attach;
            Unloaded += (s, e) => Detach();
            SizeChanged += (s, e) => Close();
            DataContextChanged += (s, e) => Close();
        }

        internal static bool IsTruncated(TextBlock title)
        {
            if (string.IsNullOrEmpty(title.Text) || title.ActualWidth <= 0) return false;
            var full = new TextBlock {
                Text = title.Text, FontFamily = title.FontFamily, FontSize = title.FontSize,
                FontStyle = title.FontStyle, FontWeight = title.FontWeight, FontStretch = title.FontStretch
            };
            full.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return full.DesiredSize.Width > title.ActualWidth + 1;
        }

        private void Attach(object sender, RoutedEventArgs e)
        {
            Detach();
            row = FindRow(this);
            list = row == null ? null : ItemsControl.ItemsControlFromItemContainer(row) as ListBox;
            window = Window.GetWindow(this);
            if (row == null) return;
            row.MouseEnter += Highlight;
            row.MouseLeave += Leave;
            row.IsKeyboardFocusWithinChanged += FocusChanged;
            row.ContextMenuOpening += ContextOpening;
            if (list != null) list.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Scrolled));
            if (window != null) {
                window.SizeChanged += WindowChanged;
                window.LocationChanged += Leave;
                window.Deactivated += Leave;
            }
        }

        private static ListBoxItem FindRow(DependencyObject node)
        {
            for (var current = node; current != null; current = VisualTreeHelper.GetParent(current))
                if (current is ListBoxItem item) return item;
            return null;
        }

        private void Highlight(object sender, MouseEventArgs e) => Show();
        private void Leave(object sender, EventArgs e) => Close();
        private void FocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (row.IsKeyboardFocusWithin) Show(); else Close();
        }
        private void ContextOpening(object sender, ContextMenuEventArgs e) => Close();
        private void WindowChanged(object sender, SizeChangedEventArgs e) => Close();
        private void Scrolled(object sender, ScrollChangedEventArgs e) => Close();

        private void Show()
        {
            if (!IsVisible || !IsTruncated(this) || list?.ContextMenu?.IsOpen == true) return;
            Close();
            var label = new TextBlock {
                Text = Text, Foreground = Foreground, FontFamily = FontFamily, FontSize = FontSize,
                FontWeight = FontWeight, TextWrapping = TextWrapping.Wrap,
                MaxWidth = Math.Max(160, SystemParameters.WorkArea.Width / 2)
            };
            expanded = new Popup {
                PlacementTarget = this, Placement = PlacementMode.Right, HorizontalOffset = 6,
                StaysOpen = true, AllowsTransparency = true, IsHitTestVisible = false,
                Child = new Border {
                    Background = (Brush)FindResource("NavigatorMenuBrush"),
                    BorderBrush = (Brush)FindResource("NavigatorOutlineBrush"), BorderThickness = new Thickness(1),
                    Padding = new Thickness(7, 4, 7, 4), Child = label
                }
            };
            expanded.IsOpen = true;
        }

        private void Close()
        {
            if (expanded == null) return;
            expanded.IsOpen = false;
            expanded.Child = null;
            expanded = null;
        }

        private void Detach()
        {
            Close();
            if (row != null) {
                row.MouseEnter -= Highlight;
                row.MouseLeave -= Leave;
                row.IsKeyboardFocusWithinChanged -= FocusChanged;
                row.ContextMenuOpening -= ContextOpening;
            }
            if (list != null) list.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Scrolled));
            if (window != null) {
                window.SizeChanged -= WindowChanged;
                window.LocationChanged -= Leave;
                window.Deactivated -= Leave;
            }
            row = null; list = null; window = null;
        }
    }
}
