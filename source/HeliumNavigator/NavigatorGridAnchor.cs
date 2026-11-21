using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HeliumNavigator
{
    // The same pixel offset identifies a different row after the cover column count changes.
    internal sealed class NavigatorGridAnchor
    {
        private readonly object item;
        private readonly double top;

        private NavigatorGridAnchor(object item, double top) { this.item = item; this.top = top; }

        internal static NavigatorGridAnchor Capture(ListBox list)
        {
            if (list == null || !list.IsVisible) return null;
            var viewport = Find<ScrollContentPresenter>(list);
            if (viewport == null || viewport.ActualHeight <= 0) return null;
            ListBoxItem first = null;
            var firstTop = double.PositiveInfinity;
            var firstLeft = double.PositiveInfinity;
            foreach (var row in Descendants<ListBoxItem>(list)) {
                if (ItemsControl.ItemsControlFromItemContainer(row) != list) continue;
                var point = row.TranslatePoint(new Point(), viewport);
                if (point.Y + row.ActualHeight <= 0 || point.Y >= viewport.ActualHeight) continue;
                if (point.Y < firstTop || (Math.Abs(point.Y - firstTop) < 1 && point.X < firstLeft)) {
                    first = row; firstTop = point.Y; firstLeft = point.X;
                }
            }
            return first == null ? null : new NavigatorGridAnchor(
                list.ItemContainerGenerator.ItemFromContainer(first), firstTop);
        }

        internal bool Restore(ListBox list)
        {
            if (list == null || !list.IsVisible || !list.Items.Contains(item)) return false;
            // ScrollIntoView realizes the anchor through the existing virtualized item generator.
            list.ScrollIntoView(item);
            list.UpdateLayout();
            var row = list.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            var viewport = Find<ScrollContentPresenter>(list);
            var scroll = Find<ScrollViewer>(list);
            if (row == null || viewport == null || scroll == null || viewport.ActualHeight <= 0) return false;
            var delta = row.TranslatePoint(new Point(), viewport).Y - top;
            var unitsPerPixel = scroll.ViewportHeight / viewport.ActualHeight;
            scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset + delta * unitsPerPixel));
            return true;
        }

        private static T Find<T>(DependencyObject node) where T : DependencyObject
        {
            foreach (var child in Descendants<T>(node)) return child;
            return null;
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject node) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is T match) yield return match;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }
        }
    }
}
