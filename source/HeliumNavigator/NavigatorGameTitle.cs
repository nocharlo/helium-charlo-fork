using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Screen = System.Windows.Forms.Screen;

namespace HeliumNavigator
{
    public sealed class NavigatorGameTitle : TextBlock
    {
        private static readonly DependencyProperty ActiveTitleProperty = DependencyProperty.RegisterAttached(
            "ActiveTitle", typeof(NavigatorGameTitle), typeof(NavigatorGameTitle));
        public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.RegisterAttached(
            "IsExpanded", typeof(bool), typeof(NavigatorGameTitle));

        public static bool GetIsExpanded(DependencyObject element) => (bool)element.GetValue(IsExpandedProperty);

        private ListBoxItem row;
        private ListBox list;
        private Window window;
        private AdornerLayer layer;
        private ExpandedRow expanded;
        private DispatcherTimer hoverTimer;

        public NavigatorGameTitle()
        {
            Loaded += Attach;
            Unloaded += (s, e) => Detach();
            SizeChanged += (s, e) => Close();
            DataContextChanged += (s, e) => Close();
            IsVisibleChanged += (s, e) => Close();
        }

        internal static bool IsTruncated(TextBlock title) =>
            !string.IsNullOrEmpty(title.Text) && title.ActualWidth > 0 &&
            MeasureTitle(title).Width > title.ActualWidth + 1 / VisualTreeHelper.GetDpi(title).DpiScaleX;

        private static FormattedText MeasureTitle(TextBlock title) =>
            new FormattedText(title.Text, title.Language.GetEquivalentCulture(), title.FlowDirection,
                new Typeface(title.FontFamily, title.FontStyle, title.FontWeight, title.FontStretch),
                title.FontSize, title.Foreground, null, TextOptions.GetTextFormattingMode(title),
                VisualTreeHelper.GetDpi(title).PixelsPerDip);

        private void Attach(object sender, RoutedEventArgs e)
        {
            Detach();
            row = FindAncestor<ListBoxItem>(this);
            list = row == null ? null : ItemsControl.ItemsControlFromItemContainer(row) as ListBox;
            window = Window.GetWindow(this);
            if (row == null) return;
            row.MouseEnter += Highlight;
            row.IsKeyboardFocusWithinChanged += FocusChanged;
        }

        private static T FindAncestor<T>(DependencyObject node) where T : DependencyObject
        {
            for (var parent = VisualTreeHelper.GetParent(node); parent != null; parent = VisualTreeHelper.GetParent(parent))
                if (parent is T ancestor) return ancestor;
            return null;
        }

        private static AdornerDecorator WindowDecorator(Visual element)
        {
            // Use the outer layer so the drawing can cross the sidebar and scroll viewport.
            AdornerDecorator decorator = null;
            for (var parent = VisualTreeHelper.GetParent(element); parent != null; parent = VisualTreeHelper.GetParent(parent))
                if (parent is AdornerDecorator ancestor) decorator = ancestor;
            return decorator;
        }

        private void Highlight(object sender, MouseEventArgs e)
        {
            if (expanded != null || list == null || window == null || !IsVisible ||
                list.ContextMenu?.IsOpen == true || Mouse.Captured != null || !IsTruncated(this)) return;
            if (hoverTimer == null) {
                hoverTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher) {
                    Interval = TimeSpan.FromMilliseconds(275)
                };
                hoverTimer.Tick += HoverElapsed;
            }
            TrackInteraction();
            hoverTimer.Start();
        }

        private void HoverElapsed(object sender, EventArgs e)
        {
            hoverTimer.Stop();
            if (row?.IsMouseOver == true) ShowExpansion();
            if (expanded == null) Close();
        }

        private void FocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (row.IsKeyboardFocusWithin) ShowExpansion(); else Close();
        }

        internal static void Dismiss(ListBox list) => (list.GetValue(ActiveTitleProperty) as NavigatorGameTitle)?.Close();

        internal void ShowExpansion()
        {
            if (expanded != null || list == null || window == null || !IsVisible ||
                list.ContextMenu?.IsOpen == true || Mouse.Captured != null || !IsTruncated(this)) return;

            var decorator = WindowDecorator(list);
            var viewport = FindAncestor<ScrollContentPresenter>(row);
            var face = row.Template.FindName("RowBorder", row) as Border;
            if (decorator == null || viewport == null || face == null) return;

            var rowOrigin = row.TranslatePoint(new Point(), list);
            var viewportBounds = new Rect(viewport.TranslatePoint(new Point(), list), viewport.RenderSize);
            var originInWindow = row.TranslatePoint(new Point(), decorator);
            var screenPoint = row.PointToScreen(new Point());
            var monitor = Screen.FromPoint(new System.Drawing.Point((int)screenPoint.X, (int)screenPoint.Y));
            var monitorRight = row.PointFromScreen(new Point(monitor.WorkingArea.Right, screenPoint.Y)).X;
            var available = Math.Min(decorator.ActualWidth - originInWindow.X, monitorRight);
            var titleOrigin = TranslatePoint(new Point(), row);
            var titleRight = titleOrigin.X + ActualWidth;
            var trailing = row.ActualWidth - titleRight;
            var text = MeasureTitle(this);
            var textWidth = Math.Min(Math.Ceiling(text.Width), available - titleOrigin.X - trailing);
            if (textWidth <= ActualWidth) return;

            var bounds = new Rect(rowOrigin, new Size(titleOrigin.X + textWidth + trailing, row.ActualHeight));
            var visible = Rect.Intersect(bounds, new Rect(bounds.Left, viewportBounds.Top, bounds.Width, viewportBounds.Height));
            if (visible.IsEmpty || visible.Height <= 0) return;

            TrackInteraction();
            hoverTimer?.Stop();
            row.SetValue(IsExpandedProperty, true);
            var outline = row.Template.FindName("ContextOutline", row) as Border;
            var favorite = (Parent as Grid)?.FindName("FavoriteStar") as TextBlock;
            layer = decorator.AdornerLayer;
            expanded = new ExpandedRow(list, bounds, visible, titleOrigin, textWidth, text, face.Background,
                outline?.Visibility == Visibility.Visible ? outline.BorderBrush : null,
                favorite?.IsVisible == true ? MeasureTitle(favorite) : null,
                favorite?.TranslatePoint(new Point(), row) ?? new Point(), textWidth - ActualWidth);
            layer.Add(expanded);
        }

        private void TrackInteraction()
        {
            if (ReferenceEquals(list.GetValue(ActiveTitleProperty), this)) return;
            Dismiss(list);
            list.SetValue(ActiveTitleProperty, this);
            list.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Scrolled));
            list.SelectionChanged += CloseOnInput;
            list.ContextMenuOpening += CloseOnInput;
            window.AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(PointerMoved), true);
            window.AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(CloseOnInput), true);
            window.AddHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler(CloseOnInput), true);
            window.AddHandler(PreviewKeyDownEvent, new KeyEventHandler(CloseOnInput), true);
            window.MouseLeave += CloseOnInput;
            window.SizeChanged += CloseOnInput;
            window.LocationChanged += CloseOnInput;
            window.Deactivated += CloseOnInput;
        }

        private void PointerMoved(object sender, MouseEventArgs e)
        {
            if (expanded == null ? !row.IsMouseOver : !expanded.Bounds.Contains(e.GetPosition(list))) Close();
        }

        private void Scrolled(object sender, ScrollChangedEventArgs e) => Close();
        private void CloseOnInput(object sender, EventArgs e) => Close();

        private void Close()
        {
            hoverTimer?.Stop();
            if (!ReferenceEquals(list?.GetValue(ActiveTitleProperty), this)) return;
            list.ClearValue(ActiveTitleProperty);
            if (expanded != null) {
                layer.Remove(expanded);
                expanded = null;
                layer = null;
                row.ClearValue(IsExpandedProperty);
            }
            list.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Scrolled));
            list.SelectionChanged -= CloseOnInput;
            list.ContextMenuOpening -= CloseOnInput;
            window.RemoveHandler(PreviewMouseMoveEvent, new MouseEventHandler(PointerMoved));
            window.RemoveHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(CloseOnInput));
            window.RemoveHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler(CloseOnInput));
            window.RemoveHandler(PreviewKeyDownEvent, new KeyEventHandler(CloseOnInput));
            window.MouseLeave -= CloseOnInput;
            window.SizeChanged -= CloseOnInput;
            window.LocationChanged -= CloseOnInput;
            window.Deactivated -= CloseOnInput;
        }

        private void Detach()
        {
            Close();
            if (row != null) {
                row.MouseEnter -= Highlight;
                row.IsKeyboardFocusWithinChanged -= FocusChanged;
            }
            row = null;
            list = null;
            window = null;
        }

        private sealed class ExpandedRow : Adorner
        {
            internal Rect Bounds { get; }
            private readonly Rect rowBounds;
            private readonly Point sourceTitle;
            private readonly FormattedText text;
            private readonly Brush background;
            private readonly Brush outline;
            private readonly FormattedText favorite;
            private readonly Point favoriteOrigin;

            internal ExpandedRow(ListBox list, Rect bounds, Rect visible, Point titleOrigin, double textWidth,
                FormattedText text, Brush background, Brush outline, FormattedText favorite, Point favoriteOrigin,
                double addedWidth) : base(list)
            {
                Bounds = visible;
                rowBounds = bounds;
                sourceTitle = titleOrigin;
                this.text = text;
                this.background = background;
                this.outline = outline;
                this.favorite = favorite;
                this.favoriteOrigin = new Point(favoriteOrigin.X + addedWidth, favoriteOrigin.Y);
                IsHitTestVisible = false;
                text.MaxLineCount = 1;
                text.MaxTextWidth = textWidth;
                text.Trimming = TextTrimming.CharacterEllipsis;
            }

            protected override void OnRender(DrawingContext dc)
            {
                var titleLeft = rowBounds.Left + sourceTitle.X;
                var paint = new Rect(titleLeft, rowBounds.Top, rowBounds.Right - titleLeft, rowBounds.Height);
                dc.PushClip(new RectangleGeometry(Bounds));
                // The real icon and leading highlight remain visible in the original row.
                dc.DrawRectangle(background, null, paint);
                dc.DrawText(text, new Point(titleLeft, rowBounds.Top + sourceTitle.Y));
                if (favorite != null) dc.DrawText(favorite, rowBounds.TopLeft + (Vector)favoriteOrigin);
                if (outline != null) {
                    dc.DrawRectangle(outline, null, new Rect(titleLeft, rowBounds.Top, paint.Width, 1));
                    dc.DrawRectangle(outline, null, new Rect(titleLeft, rowBounds.Bottom - 1, paint.Width, 1));
                    dc.DrawRectangle(outline, null, new Rect(rowBounds.Right - 1, rowBounds.Top, 1, rowBounds.Height));
                }
                dc.Pop();
            }
        }
    }
}
