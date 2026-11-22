using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml.Serialization;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace HeliumNavigator
{
    public sealed class Preferences : ISettings, INotifyPropertyChanged
    {
        public double Width { get; set; } = 230;
        public bool Collapsed { get; set; }
        public int Grouping { get; set; }
        // 0 follows Playnite; other values sort Navigator rows within each group.
        public int SortMode { get; set; }
        public bool SortDescending { get; set; }
        public bool ApplySortGroupToPlaynite { get; set; } = true;
        // Null means an older file: use catalog defaults. Lists are ordered visibility selections.
        public List<int> VisibleGroupingModes { get; set; }
        public List<int> VisibleSortingModes { get; set; }
        [XmlIgnore] public bool VisibleGroupingModesSpecified { get; set; }
        [XmlIgnore] public bool VisibleSortingModesSpecified { get; set; }
        public List<FilterGroupPreference> FilterWorkspace { get; set; }
        internal const double MinFilterWidth = 320, MaxFilterWidth = 1200, MinFilterHeight = 260, MaxFilterHeight = 900;
        public double FilterPopupWidth { get; set; }
        public double FilterPopupHeight { get; set; }
        public double? FilterPopupX { get; set; }
        public double? FilterPopupY { get; set; }
        [XmlIgnore] internal bool HasFilterPopupPosition => FilterPopupX.HasValue && FilterPopupY.HasValue &&
            Math.Abs(FilterPopupX.Value) <= 100000 && Math.Abs(FilterPopupY.Value) <= 100000;
        public bool ShouldSerializeFilterPopupX() => HasFilterPopupPosition;
        public bool ShouldSerializeFilterPopupY() => HasFilterPopupPosition;
        [XmlIgnore] internal bool HasFilterPopupSize => FilterPopupWidth >= MinFilterWidth && FilterPopupWidth <= MaxFilterWidth &&
            FilterPopupHeight >= MinFilterHeight && FilterPopupHeight <= MaxFilterHeight;
        public bool ShouldSerializeFilterPopupWidth() => HasFilterPopupSize;
        public bool ShouldSerializeFilterPopupHeight() => HasFilterPopupSize;
        public int RecentActivityGroupingStyle { get; set; } // 0: Hybrid, 1: Detailed.
        [XmlIgnore] public bool RecentActivityGroupingStyleSpecified { get; set; }
        // 0: Automatic, 1: Always hidden, 2: Always visible.
        public int LayoutMode { get; set; }
        private bool dimUninstalledGameIcons;
        public bool DimUninstalledGameIcons
        {
            get => dimUninstalledGameIcons;
            set
            {
                if (dimUninstalledGameIcons == value) return;
                dimUninstalledGameIcons = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DimUninstalledGameIcons)));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        [XmlIgnore] public bool ShowCompletionStatusIndicators { get; set; }
        [XmlElement(nameof(ShowCompletionStatusIndicators))]
        public string SerializedCompletionStatusIndicators
        {
            get => ShowCompletionStatusIndicators ? "true" : "false";
            set => ShowCompletionStatusIndicators = bool.TryParse(value, out var enabled) && enabled;
        }
        public List<CompletionStatusIndicatorPreference> CompletionStatusIndicatorColors { get; set; }
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
            // The temporary v0.3.2 Default modes may still be selected in saved settings.
            const int removedDefaultGrouping = 20;
            const int removedDefaultSorting = 16;
            if (Grouping == removedDefaultGrouping) Grouping = (int)NavigatorGroupingMode.None;
            if (SortMode == removedDefaultSorting) {
                SortMode = (int)NavigatorSortMode.Name;
                SortDescending = false;
            }
            SortMode = (int)NavigatorSortPolicy.ValidMode(SortMode);
            Grouping = NavigatorOrganization.Group(Grouping).Id;
            VisibleGroupingModes = NavigatorOrganization.Normalize(VisibleGroupingModesSpecified ? VisibleGroupingModes : null, true);
            VisibleSortingModes = NavigatorOrganization.Normalize(VisibleSortingModesSpecified ? VisibleSortingModes : null, false);
            VisibleGroupingModesSpecified = VisibleSortingModesSpecified = true;
            FilterWorkspace = FilterWorkspaceSettings.Normalize(FilterWorkspace);
            CompletionStatusIndicatorColors = CompletionStatusIndicators.Normalize(CompletionStatusIndicatorColors);
            if (!HasFilterPopupSize) FilterPopupWidth = FilterPopupHeight = 0;
            if (!HasFilterPopupPosition) FilterPopupX = FilterPopupY = null;
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
            VisibleGroupingModes = NavigatorOrganization.Normalize(VisibleGroupingModes, true);
            VisibleSortingModes = NavigatorOrganization.Normalize(VisibleSortingModes, false);
            VisibleGroupingModesSpecified = VisibleSortingModesSpecified = true;
            FilterWorkspace = FilterWorkspaceSettings.Normalize(FilterWorkspace);
            CompletionStatusIndicatorColors = CompletionStatusIndicators.Normalize(CompletionStatusIndicatorColors);
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
            SortMode = SortMode, SortDescending = SortDescending, ApplySortGroupToPlaynite = ApplySortGroupToPlaynite,
            VisibleGroupingModes = VisibleGroupingModes == null ? null : new List<int>(VisibleGroupingModes),
            VisibleSortingModes = VisibleSortingModes == null ? null : new List<int>(VisibleSortingModes),
            FilterWorkspace = FilterWorkspace?.ConvertAll(group => group?.Copy()),
            FilterPopupWidth = FilterPopupWidth, FilterPopupHeight = FilterPopupHeight,
            FilterPopupX = FilterPopupX, FilterPopupY = FilterPopupY,
            VisibleGroupingModesSpecified = VisibleGroupingModesSpecified, VisibleSortingModesSpecified = VisibleSortingModesSpecified,
            LayoutMode = LayoutMode, DimUninstalledGameIcons = DimUninstalledGameIcons,
            ShowCompletionStatusIndicators = ShowCompletionStatusIndicators,
            CompletionStatusIndicatorColors = CompletionStatusIndicators.Normalize(CompletionStatusIndicatorColors),
            SteamMode = SteamMode, OpenFromGridCover = OpenFromGridCover,
            SidebarClickAction = SidebarClickAction, GridCoverClickAction = GridCoverClickAction,
            PreserveGridScroll = PreserveGridScroll, AutomaticallyRevealSelectedGame = AutomaticallyRevealSelectedGame,
            RecentActivityGroupingStyle = RecentActivityGroupingStyle,
            RecentActivityGroupingStyleSpecified = RecentActivityGroupingStyleSpecified };
        private void Assign(Preferences value)
        {
            Width = value.Width; Collapsed = value.Collapsed; Grouping = value.Grouping;
            SortMode = value.SortMode; SortDescending = value.SortDescending;
            ApplySortGroupToPlaynite = value.ApplySortGroupToPlaynite;
            VisibleGroupingModes = value.VisibleGroupingModes == null ? null : new List<int>(value.VisibleGroupingModes);
            VisibleSortingModes = value.VisibleSortingModes == null ? null : new List<int>(value.VisibleSortingModes);
            FilterWorkspace = value.FilterWorkspace?.ConvertAll(group => group?.Copy());
            FilterPopupWidth = value.FilterPopupWidth; FilterPopupHeight = value.FilterPopupHeight;
            FilterPopupX = value.FilterPopupX; FilterPopupY = value.FilterPopupY;
            VisibleGroupingModesSpecified = value.VisibleGroupingModesSpecified;
            VisibleSortingModesSpecified = value.VisibleSortingModesSpecified;
            LayoutMode = value.LayoutMode;
            DimUninstalledGameIcons = value.DimUninstalledGameIcons;
            ShowCompletionStatusIndicators = value.ShowCompletionStatusIndicators;
            CompletionStatusIndicatorColors = CompletionStatusIndicators.Normalize(value.CompletionStatusIndicatorColors);
            SteamMode = value.SteamMode;
            OpenFromGridCover = value.OpenFromGridCover; PreserveGridScroll = value.PreserveGridScroll;
            AutomaticallyRevealSelectedGame = value.AutomaticallyRevealSelectedGame;
            RecentActivityGroupingStyle = value.RecentActivityGroupingStyle;
            RecentActivityGroupingStyleSpecified = value.RecentActivityGroupingStyleSpecified;
            SidebarClickAction = value.SidebarClickAction; GridCoverClickAction = value.GridCoverClickAction;
        }
    }

    public sealed class NavigatorSettingsView : UserControl
    {
        public NavigatorSettingsView() : this(null) { }

        internal NavigatorSettingsView(Func<IEnumerable<CompletionStatus>> completionStatuses)
        {
            var clicks = new StackPanel();
            var clickActions = new[] { "Open Full Details View", "Open Grid Details Panel" };
            clicks.Children.Add(Choice("Sidebar game click", "SidebarClickAction", clickActions));
            clicks.Children.Add(Choice("Grid cover click", "GridCoverClickAction", clickActions));
            var selection = new StackPanel();
            selection.Children.Add(BoundCheck("Restore grid scroll position when returning", "PreserveGridScroll"));
            selection.Children.Add(BoundCheck("Automatically reveal selected game", "AutomaticallyRevealSelectedGame"));

            var width = new TextBox { Width = 96, MinHeight = 26, HorizontalAlignment = HorizontalAlignment.Left, ToolTip = "180–380 px" };
            System.Windows.Automation.AutomationProperties.SetName(width, "Sidebar width");
            width.SetBinding(TextBox.TextProperty, new Binding("Width") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

            var activity = Section("Recent Activity", ActivityStyle());
            var nativeOrganization = BoundCheck("Apply Sort & Group changes to Playnite", nameof(Preferences.ApplySortGroupToPlaynite));
            nativeOrganization.Margin = new Thickness(4, 0, 0, 12);
            var organization = new Grid();
            var filters = new ContentControl();
            var automaticSize = new Button { Content = "Use automatic filter panel size", HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 8), Visibility = Visibility.Collapsed };
            automaticSize.Click += (s, e) => {
                if (!(DataContext is Preferences prefs)) return;
                prefs.FilterPopupWidth = prefs.FilterPopupHeight = 0;
                automaticSize.Visibility = Visibility.Collapsed;
            };
            Action populate = () => {
                organization.Children.Clear();
                filters.Content = null;
                automaticSize.Visibility = Visibility.Collapsed;
                if (!(DataContext is Preferences prefs)) return;
                organization.Children.Add(NavigatorOrganizationControls.VisibilityEditor(prefs, true));
                organization.Children.Add(NavigatorOrganizationControls.VisibilityEditor(prefs, false));
                NavigatorOrganizationControls.ArrangeEditors(organization, organization.ActualWidth);
                if (prefs.FilterWorkspace == null) prefs.FilterWorkspace = FilterWorkspaceSettings.Normalize(null);
                filters.Content = FilterWorkspaceSettings.Editor(prefs);
                automaticSize.Visibility = prefs.HasFilterPopupSize ? Visibility.Visible : Visibility.Collapsed;
            };
            organization.SizeChanged += (s, e) => NavigatorOrganizationControls.ArrangeEditors(organization, e.NewSize.Width);
            Loaded += (s, e) => populate();
            DataContextChanged += (s, e) => populate();

            var tabs = new TabControl();
            tabs.Items.Add(Page("Navigation", Section("Click actions", clicks), Section("Selection and scroll", selection)));
            tabs.Items.Add(Page("Appearance", Section("Grid details panel",
                Choice("Display mode", "LayoutMode", "Automatic", "Always hidden", "Always visible")),
                Section("Sidebar width", width),
                Section("Game icons", BoundCheck("Dim uninstalled game icons", nameof(Preferences.DimUninstalledGameIcons))),
                CompletionStatusSection(completionStatuses)));
            tabs.Items.Add(Page("Organization", activity, nativeOrganization, organization));
            tabs.Items.Add(Page("Filters", automaticSize, filters));
            Content = tabs;
        }

        private FrameworkElement CompletionStatusSection(Func<IEnumerable<CompletionStatus>> completionStatuses)
        {
            var panel = new StackPanel();
            panel.Children.Add(BoundCheck("Show completion status indicators", nameof(Preferences.ShowCompletionStatusIndicators)));
            var rows = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            panel.Children.Add(rows);
            var reset = new Button { Content = "Reset colors", HorizontalAlignment = HorizontalAlignment.Left };
            panel.Children.Add(reset);
            void Populate()
            {
                rows.Children.Clear();
                reset.IsEnabled = false;
                if (!(DataContext is Preferences prefs) || completionStatuses == null) return;
                var options = prefs.CompletionStatusIndicatorColors ?? (prefs.CompletionStatusIndicatorColors =
                    new List<CompletionStatusIndicatorPreference>());
                foreach (var status in completionStatuses().OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)) {
                    var id = status.Id.ToString("D");
                    var option = options.Find(item => item.StatusId == id);
                    if (option == null) {
                        option = new CompletionStatusIndicatorPreference { StatusId = id };
                        options.Add(option);
                    }
                    var row = CompletionStatusColorRow(status.Name, option, () =>
                        reset.IsEnabled = options.Any(item => !string.IsNullOrEmpty(item.Color)));
                    rows.Children.Add(row);
                }
                reset.IsEnabled = options.Any(item => !string.IsNullOrEmpty(item.Color));
            }
            reset.Click += (sender, args) => {
                if (!(DataContext is Preferences prefs)) return;
                foreach (var option in prefs.CompletionStatusIndicatorColors) option.Color = null;
                Populate();
            };
            panel.Loaded += (sender, args) => Populate();
            panel.DataContextChanged += (sender, args) => { if (panel.IsLoaded) Populate(); };
            return Section("Completion Status Indicators", panel);
        }

        private static FrameworkElement CompletionStatusColorRow(string status, CompletionStatusIndicatorPreference option,
            Action colorChanged)
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var swatch = new Border { Width = 16, Height = 12, CornerRadius = new CornerRadius(2) };
            var colorButton = new Button { Content = swatch, MinWidth = 34, MinHeight = 24, Margin = new Thickness(8, 0, 0, 0) };
            System.Windows.Automation.AutomationProperties.SetName(colorButton, "Color for " + status);
            void UpdateSwatch()
            {
                var hex = option.Color ?? CompletionStatusIndicators.DefaultColor;
                var color = CompletionStatusIndicators.ParseColor(hex).Value;
                swatch.Background = new System.Windows.Media.SolidColorBrush(color);
                colorButton.ToolTip = hex;
            }
            UpdateSwatch();
            colorButton.Click += (sender, args) => {
                var color = ((System.Windows.Media.SolidColorBrush)swatch.Background).Color;
                using (var picker = new System.Windows.Forms.ColorDialog {
                    FullOpen = true,
                    Color = System.Drawing.Color.FromArgb(color.R, color.G, color.B),
                    CustomColors = new[] { 0xE87979, 0x78C58E, 0xE7C969, 0xDC91BF, 0xA3AEBB, 0x67B3FF, 0xE5A36D }
                        .Select(rgb => System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(unchecked((int)0xFF000000) | rgb)))
                        .ToArray()
                }) {
                    var window = Window.GetWindow(colorButton);
                    var owner = window == null ? null : new ColorDialogOwner(new System.Windows.Interop.WindowInteropHelper(window).Handle);
                    if (picker.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK) return;
                    option.Color = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
                    UpdateSwatch();
                    colorChanged();
                }
            };
            DockPanel.SetDock(colorButton, Dock.Right);
            row.Children.Add(colorButton);
            var enabled = new CheckBox { Content = new TextBlock { Text = status, TextTrimming = TextTrimming.CharacterEllipsis },
                ToolTip = status, IsChecked = option.IsEnabled, VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Automation.AutomationProperties.SetName(enabled, "Show indicator for " + status);
            enabled.Checked += (sender, args) => option.IsEnabled = true;
            enabled.Unchecked += (sender, args) => option.IsEnabled = false;
            row.Children.Add(enabled);
            return row;
        }

        private sealed class ColorDialogOwner : System.Windows.Forms.IWin32Window
        {
            internal ColorDialogOwner(IntPtr handle) { Handle = handle; }
            public IntPtr Handle { get; }
        }

        internal static GroupBox Section(string title, FrameworkElement content) => new GroupBox {
            Header = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold },
            Content = content, Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(12, 8, 12, 10), HorizontalContentAlignment = HorizontalAlignment.Stretch
        };

        private static ListBox ActivityStyle()
        {
            var cards = new ListBox {
                BorderThickness = new Thickness(0), Padding = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            System.Windows.Automation.AutomationProperties.SetName(cards, "Recent Activity grouping style");
            ScrollViewer.SetHorizontalScrollBarVisibility(cards, ScrollBarVisibility.Disabled);
            var columns = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.UniformGrid));
            columns.SetValue(System.Windows.Controls.Primitives.UniformGrid.ColumnsProperty, 2);
            cards.ItemsPanel = new ItemsPanelTemplate(columns);
            cards.Items.Add(ActivityCard("Hybrid", "This year by month, older years grouped"));
            cards.Items.Add(ActivityCard("Detailed", "Month-by-month history"));
            cards.SetBinding(ListBox.SelectedIndexProperty, new Binding(nameof(Preferences.RecentActivityGroupingStyle)) { Mode = BindingMode.TwoWay });
            return cards;
        }

        private static ListBoxItem ActivityCard(string title, string description)
        {
            var panel = new StackPanel { Margin = new Thickness(8, 5, 8, 5) };
            panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
            var card = new ListBoxItem {
                Content = panel, MinHeight = 68, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 4)
            };
            System.Windows.Automation.AutomationProperties.SetName(card, title + ". " + description);
            return card;
        }

        private static TabItem Page(string title, params FrameworkElement[] sections)
        {
            var panel = new StackPanel { Margin = new Thickness(14) };
            foreach (var section in sections) panel.Children.Add(section);
            return new TabItem { Header = title, Content = new ScrollViewer {
                Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            }};
        }

        private static StackPanel Choice(string title, string path, params string[] options)
        {
            var group = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            group.Children.Add(new TextBlock { Text = title });
            var choice = new ComboBox { MinWidth = 160, MaxWidth = 260, MinHeight = 26, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 0, 0) };
            System.Windows.Automation.AutomationProperties.SetName(choice, title);
            foreach (var option in options) choice.Items.Add(option);
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
