using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace HeliumHybrid
{
    public sealed class NavigatorGroupExpander : Expander
    {
        private NavigatorGroupState state;
        private int mode;
        private string groupName;
        private bool applying;

        public NavigatorGroupExpander()
        {
            Loaded += OnLoaded;
            Expanded += OnChanged;
            Collapsed += OnChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            if (!ReferenceEquals(args.OriginalSource, this)) return;
            var parent = VisualTreeHelper.GetParent(this);
            while (parent != null && !(parent is ListBox)) parent = VisualTreeHelper.GetParent(parent);
            state = (parent as ListBox)?.Tag as NavigatorGroupState;
            groupName = (DataContext as CollectionViewGroup)?.Name?.ToString();
            if (state == null || groupName == null) return;
            mode = state.Mode;
            applying = true;
            try { IsExpanded = state.IsExpanded(mode, groupName); }
            finally { applying = false; }
        }

        private void OnChanged(object sender, RoutedEventArgs args)
        {
            if (!ReferenceEquals(args.OriginalSource, this) || applying || !IsLoaded ||
                state == null || groupName == null) return;
            state.SetExpanded(mode, groupName, IsExpanded);
        }
    }
}
