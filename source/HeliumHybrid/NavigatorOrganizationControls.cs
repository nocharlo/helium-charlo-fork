using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace HeliumHybrid
{
    internal static class NavigatorOrganizationControls
    {
        internal static void ArrangeEditors(Grid grid, double width)
        {
            bool columns = width >= 540;
            int count = columns ? 2 : 1;
            if (grid.ColumnDefinitions.Count != count || grid.RowDefinitions.Count != (columns ? 1 : 2)) {
                grid.ColumnDefinitions.Clear();
                grid.RowDefinitions.Clear();
                for (int i = 0; i < count; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
                for (int i = 0; i < (columns ? 1 : 2); i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            for (int i = 0; i < grid.Children.Count; i++) {
                var panel = (FrameworkElement)grid.Children[i];
                Grid.SetColumn(panel, columns ? i : 0);
                Grid.SetRow(panel, columns ? 0 : i);
                panel.Margin = new Thickness(columns && i == 1 ? 6 : 0, 0, columns && i == 0 ? 6 : 0, 12);
            }
        }
        internal static void ConfigureSelector(ComboBox combo, Action<OrganizationMode> selected)
        {
            combo.DisplayMemberPath = nameof(OrganizationMode.Label);
            TextSearch.SetTextPath(combo, nameof(OrganizationMode.Label));
            combo.SelectionChanged += (s, e) => {
                if (combo.SelectedItem is OrganizationMode mode) selected(mode);
            };
        }
        internal static void Choices(ComboBox combo, IEnumerable<int> visible, bool grouping, int active)
        {
            var items = NavigatorOrganization.Visible(visible, grouping, active).ToList();
            var view = new ListCollectionView(items);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(OrganizationMode.Category)));
            combo.ItemsSource = view;
            combo.SelectedItem = items.First(m => m.Id == active);
        }

        internal static FrameworkElement VisibilityEditor(Preferences prefs, bool grouping)
        {
            var panel = new StackPanel();
            var search = new TextBox { MinHeight = 26, Padding = new Thickness(6, 2, 6, 2) };
            panel.Children.Add(SearchField(search, grouping ? "Search grouping modes" : "Search sorting modes"));
            var modes = grouping ? NavigatorOrganization.Groups : NavigatorOrganization.Sorts;
            Func<List<int>> get = () => grouping ? prefs.VisibleGroupingModes : prefs.VisibleSortingModes;
            Action changed = null;
            Action<List<int>> set = ids => {
                if (grouping) prefs.VisibleGroupingModes = ids;
                else prefs.VisibleSortingModes = ids;
                changed?.Invoke();
            };
            set(NavigatorOrganization.Normalize(get(), grouping));
            var options = modes.Select(mode => new OrganizationVisibilityOption(mode, get, set)).ToList();
            var view = new ListCollectionView(options);
            view.SortDescriptions.Add(new SortDescription("Order", ListSortDirection.Ascending));
            var list = new ListBox { ItemsSource = view, Height = 210, BorderThickness = new Thickness(0), Padding = new Thickness(2) };
            var rowStyle = new Style(typeof(ListBoxItem));
            rowStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3, 1, 3, 1)));
            rowStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            // Keep keyboard focus distinct from the checkbox's visibility state.
            var template = new ControlTemplate(typeof(ListBoxItem));
            var border = new FrameworkElementFactory(typeof(Border), "Row");
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            border.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.BorderBrushProperty, System.Windows.Media.Brushes.Transparent);
            border.SetValue(Border.PaddingProperty, new Thickness(3, 1, 3, 1));
            border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            template.VisualTree = border;
            var selection = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selection.Setters.Add(new Setter(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(48, 150, 166, 180)), "Row"));
            template.Triggers.Add(selection);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, System.Windows.Media.Brushes.SlateGray, "Row"));
            template.Triggers.Add(focus);
            rowStyle.Setters.Add(new Setter(Control.TemplateProperty, template));
            list.ItemContainerStyle = rowStyle;
            AutomationProperties.SetName(list, grouping ? "Visible grouping modes" : "Visible sorting modes");
            list.SetValue(ScrollViewer.CanContentScrollProperty, true);
            list.SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
            list.SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
            var checkbox = new FrameworkElementFactory(typeof(CheckBox));
            checkbox.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            checkbox.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 3, 2, 3));
            checkbox.SetBinding(CheckBox.ContentProperty, new Binding("Label"));
            checkbox.SetBinding(CheckBox.IsCheckedProperty, new Binding("Visible") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            checkbox.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(OrganizationVisibilityOption.CanToggle)));
            list.ItemTemplate = new DataTemplate { VisualTree = checkbox };
            panel.Children.Add(list);
            search.TextChanged += (s, e) => { var key = Logic.SearchKey(search.Text); view.Filter = item => Logic.SearchKey(((OrganizationVisibilityOption)item).Label).Contains(key); };
            var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            Func<string, Action, Button> add = (label, action) => {
                var button = new Button { Content = label, Margin = new Thickness(0, 0, 6, 4), Padding = new Thickness(8, 4, 8, 4) };
                button.Click += (s, e) => action();
                buttons.Children.Add(button);
                return button;
            };
            add("Show All", () => set(modes.Select(m => m.Id).ToList()));
            add("Restore Defaults", () => set(NavigatorOrganization.Normalize(null, grouping)));
            Action<int> move = delta => {
                if (!(list.SelectedItem is OrganizationVisibilityOption selected)) return;
                set(NavigatorOrganization.Move(get(), selected.Id, delta));
            };
            var up = add("Move Up", () => move(-1));
            var down = add("Move Down", () => move(1));
            Action updateMoves = () => {
                var ids = get();
                var index = list.SelectedItem is OrganizationVisibilityOption option ? ids.IndexOf(option.Id) : -1;
                up.IsEnabled = index > 0;
                down.IsEnabled = index >= 0 && index < ids.Count - 1;
            };
            list.SelectionChanged += (s, e) => updateMoves();
            changed = () => {
                foreach (var option in options) option.Refresh();
                view.Refresh();
                updateMoves();
            };
            updateMoves();
            panel.Children.Add(buttons);
            return NavigatorSettingsView.Section(grouping ? "Visible grouping modes" : "Visible sorting modes", panel);
        }

        private static Grid SearchField(TextBox search, string label)
        {
            AutomationProperties.SetName(search, label);
            var field = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            var placeholder = new TextBlock {
                Text = label + "…", Opacity = 0.65, Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false
            };
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
            var empty = new DataTrigger { Binding = new Binding(nameof(TextBox.Text)) { Source = search }, Value = "" };
            empty.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible));
            style.Triggers.Add(empty);
            placeholder.Style = style;
            field.Children.Add(search);
            field.Children.Add(placeholder);
            return field;
        }
    }
    internal sealed class OrganizationVisibilityOption : INotifyPropertyChanged
    {
        private readonly OrganizationMode mode;
        private readonly Func<List<int>> get;
        private readonly Action<List<int>> set;
        public string Label => mode.Label;
        public int Id => mode.Id;
        public bool CanToggle => !Visible || get().Count > 1;
        public int Order { get { var index = get().IndexOf(mode.Id); return index < 0 ? 1000 + mode.Order : index; } }
        public bool Visible {
            get => get().Contains(mode.Id);
            set {
                var ids = new List<int>(get());
                if (value == Visible || (!value && !CanToggle)) return;
                if (value) ids.Add(mode.Id);
                else ids.Remove(mode.Id);
                set(ids);
                Refresh();
            }
        }
        internal OrganizationVisibilityOption(OrganizationMode mode, Func<List<int>> get, Action<List<int>> set)
        { this.mode = mode; this.get = get; this.set = set; }
        internal void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visible)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Order)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanToggle)));
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
