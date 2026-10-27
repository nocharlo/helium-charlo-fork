using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml.Serialization;
using Playnite.SDK;

namespace HeliumHybrid
{
    public sealed class Preferences : ISettings
    {
        public double Width { get; set; } = 230;
        public bool Collapsed { get; set; }
        public int Grouping { get; set; }
        public int RecentActivityGroupingStyle { get; set; } // 0: Hybrid, 1: Detailed.
        [XmlIgnore] public bool RecentActivityGroupingStyleSpecified { get; set; }
        // 0: Automatic, 1: Always hidden, 2: Always visible.
        public int LayoutMode { get; set; }
        // Older XML fields are read for migration but omitted when saving the new format.
        public bool SteamMode { get; set; }
        public bool ShouldSerializeSteamMode() => false;
        public bool OpenFromGridCover { get; set; }
        public bool ShouldSerializeOpenFromGridCover() => false;
        // 0: Full Details View; 1: Grid Details Panel. -1 means an older settings file.
        public int SidebarClickAction { get; set; } = -1;
        public int GridCoverClickAction { get; set; } = -1;
        public void MigrateNavigation()
        {
            if (SidebarClickAction < 0 || SidebarClickAction > 1) SidebarClickAction = SteamMode ? 0 : 1;
            if (GridCoverClickAction < 0 || GridCoverClickAction > 1) GridCoverClickAction = OpenFromGridCover ? 0 : 1;
            RecentActivityGroupingStyle = Logic.RecentActivityStyle(Grouping, RecentActivityGroupingStyle,
                RecentActivityGroupingStyleSpecified);
            RecentActivityGroupingStyleSpecified = true;
        }
        public bool PreserveGridScroll { get; set; } = true;
        public bool AutomaticallyRevealSelectedGame { get; set; } = true;
        [XmlIgnore] public Action Changed { get; set; }
        [XmlIgnore] public Action Save { get; set; }
        private Preferences backup;

        public void BeginEdit()
        {
            backup = Copy();
        }
        public void CancelEdit()
        {
            if (backup == null) return;
            Assign(backup);
            backup = null;
            Changed?.Invoke();
        }
        public void EndEdit()
        {
            backup = null;
            Save?.Invoke();
            Changed?.Invoke();
        }
        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (double.IsNaN(Width) || Width < 180 || Width > 380)
                errors.Add("Sidebar width must be between 180 and 380 pixels.");
            return errors.Count == 0;
        }
        private Preferences Copy() => new Preferences { Width = Width, Collapsed = Collapsed, Grouping = Grouping,
            LayoutMode = LayoutMode, SteamMode = SteamMode, OpenFromGridCover = OpenFromGridCover,
            SidebarClickAction = SidebarClickAction, GridCoverClickAction = GridCoverClickAction,
            PreserveGridScroll = PreserveGridScroll, AutomaticallyRevealSelectedGame = AutomaticallyRevealSelectedGame,
            RecentActivityGroupingStyle = RecentActivityGroupingStyle,
            RecentActivityGroupingStyleSpecified = RecentActivityGroupingStyleSpecified };
        private void Assign(Preferences value)
        {
            Width = value.Width; Collapsed = value.Collapsed; Grouping = value.Grouping;
            LayoutMode = value.LayoutMode; SteamMode = value.SteamMode;
            OpenFromGridCover = value.OpenFromGridCover; PreserveGridScroll = value.PreserveGridScroll;
            AutomaticallyRevealSelectedGame = value.AutomaticallyRevealSelectedGame;
            RecentActivityGroupingStyle = value.RecentActivityGroupingStyle;
            RecentActivityGroupingStyleSpecified = value.RecentActivityGroupingStyleSpecified;
            SidebarClickAction = value.SidebarClickAction; GridCoverClickAction = value.GridCoverClickAction;
        }
    }

    public sealed class NavigatorSettingsView : UserControl
    {
        public NavigatorSettingsView()
        {
            var panel = new StackPanel { Margin = new Thickness(18) };
            panel.Children.Add(new TextBlock { Text = "Navigation", FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8) });
            panel.Children.Add(NavigationChoice("Sidebar game click", "SidebarClickAction"));
            panel.Children.Add(NavigationChoice("Grid cover click", "GridCoverClickAction"));
            panel.Children.Add(BoundCheck("Restore grid scroll position when returning", "PreserveGridScroll"));
            panel.Children.Add(BoundCheck("Automatically reveal selected game", "AutomaticallyRevealSelectedGame"));
            var activityStyle = new ComboBox { MaxWidth = 260, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0,4,0,8) };
            activityStyle.Items.Add("Hybrid"); activityStyle.Items.Add("Detailed");
            activityStyle.SetBinding(ComboBox.SelectedIndexProperty, new Binding("RecentActivityGroupingStyle") { Mode = BindingMode.TwoWay });
            panel.Children.Add(new TextBlock { Text = "Recent Activity grouping style" });
            panel.Children.Add(activityStyle);
            panel.Children.Add(new TextBlock { Text = "Grid details panel", FontWeight = FontWeights.Bold, Margin = new Thickness(0,16,0,7) });
            var mode = new ComboBox { Margin = new Thickness(0,0,0,8), MaxWidth = 260, HorizontalAlignment = HorizontalAlignment.Left };
            mode.Items.Add("Automatic"); mode.Items.Add("Always hidden"); mode.Items.Add("Always visible");
            mode.SetBinding(ComboBox.SelectedIndexProperty, new Binding("LayoutMode") { Mode = BindingMode.TwoWay });
            panel.Children.Add(mode);
            panel.Children.Add(new TextBlock { Text = "Sidebar", FontWeight = FontWeights.Bold, Margin = new Thickness(0,16,0,7) });
            panel.Children.Add(new TextBlock { Text = "Width in pixels (180–380)" });
            var width = new TextBox { MaxWidth = 100, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0,4,0,8) };
            width.SetBinding(TextBox.TextProperty, new Binding("Width") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            panel.Children.Add(width);
            panel.Children.Add(new TextBlock { Text = "Width and expanded state are saved automatically when changed in the sidebar.", TextWrapping = TextWrapping.Wrap });
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
        private static StackPanel NavigationChoice(string title, string path)
        {
            var group = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            group.Children.Add(new TextBlock { Text = title });
            var choice = new ComboBox { MaxWidth = 260, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
            choice.Items.Add("Open Full Details View");
            choice.Items.Add("Open Grid Details Panel");
            choice.SetBinding(ComboBox.SelectedIndexProperty, new Binding(path) { Mode = BindingMode.TwoWay });
            group.Children.Add(choice);
            return group;
        }
        private static CheckBox BoundCheck(string title, string path)
        {
            var box = new CheckBox { Content = title, Margin = new Thickness(0,4,0,4) };
            box.SetBinding(CheckBox.IsCheckedProperty, new Binding(path) { Mode = BindingMode.TwoWay });
            return box;
        }
    }
}
