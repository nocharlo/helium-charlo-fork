using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace HeliumHybrid
{
    public sealed class FilterGroupPreference
    {
        public string Id { get; set; }
        public bool Hidden { get; set; }
        public List<string> FieldOrder { get; set; } = new List<string>();
        public List<string> HiddenFields { get; set; } = new List<string>();

        internal FilterGroupPreference Copy() => new FilterGroupPreference {
            Id = Id, Hidden = Hidden, FieldOrder = new List<string>(FieldOrder ?? new List<string>()),
            HiddenFields = new List<string>(HiddenFields ?? new List<string>())
        };
    }

    internal static class FilterWorkspaceSettings
    {
        internal static List<FilterGroupPreference> Normalize(IEnumerable<FilterGroupPreference> preferences)
        {
            // Retain unknown IDs so a temporarily unavailable group or field keeps its position and visibility.
            var result = (preferences ?? Enumerable.Empty<FilterGroupPreference>())
                .Where(group => !string.IsNullOrWhiteSpace(group?.Id))
                .GroupBy(group => group.Id).Select(group => group.First().Copy()).ToList();
            foreach (var catalogGroup in FilterWorkspaceCatalog.Groups) {
                var group = result.FirstOrDefault(item => item.Id == catalogGroup.Id);
                if (group == null) {
                    group = new FilterGroupPreference { Id = catalogGroup.Id };
                    result.Add(group);
                }
                group.FieldOrder = group.FieldOrder.Concat(catalogGroup.Fields.Select(field => field.Id)).Distinct().ToList();
                group.HiddenFields = group.HiddenFields.Distinct().ToList();
            }
            return result;
        }

        internal static void Move<T>(List<T> all, List<T> available, T item, int delta)
        {
            var index = available.IndexOf(item);
            var target = index + delta;
            if (index < 0 || target < 0 || target >= available.Count) return;
            var from = all.IndexOf(item);
            var to = all.IndexOf(available[target]);
            all[from] = all[to];
            all[to] = item;
        }

        internal static FrameworkElement Editor(Preferences preferences)
        {
            var host = new StackPanel();
            var groups = preferences.FilterWorkspace;
            var known = FilterWorkspaceCatalog.Groups.ToDictionary(group => group.Id);
            var groupControls = new Dictionary<FilterGroupPreference, FrameworkElement>();
            foreach (var group in groups.Where(group => known.ContainsKey(group.Id))) {
                var catalog = known[group.Id];
                var fields = catalog.Fields.ToDictionary(field => field.Id);
                var fieldList = new StackPanel { Margin = new Thickness(20, 2, 0, 6) };
                var fieldControls = new Dictionary<string, FrameworkElement>();
                foreach (var id in group.FieldOrder.Where(fields.ContainsKey)) {
                    var field = fields[id];
                    fieldControls[id] = Row(field.Label, !group.HiddenFields.Contains(id), visible => {
                        if (visible) group.HiddenFields.Remove(id);
                        else group.HiddenFields.Add(id);
                    }, delta => MoveAndArrange(fieldList, group.FieldOrder, fieldControls, id, delta));
                }
                Arrange(fieldList, group.FieldOrder, fieldControls);
                var expander = new Expander { Content = fieldList, Margin = new Thickness(0, 2, 0, 3) };
                expander.Header = Row(catalog.Label, !group.Hidden, visible => group.Hidden = !visible,
                    delta => MoveAndArrange(host, groups, groupControls, group, delta));
                groupControls[group] = expander;
            }
            Arrange(host, groups, groupControls);
            return host;
        }

        private static void MoveAndArrange<T>(Panel host, List<T> order, Dictionary<T, FrameworkElement> controls, T item, int delta)
        {
            var focused = System.Windows.Input.Keyboard.FocusedElement;
            Move(order, order.Where(controls.ContainsKey).ToList(), item, delta);
            Arrange(host, order, controls);
            if (focused != null) System.Windows.Input.Keyboard.Focus(focused);
        }

        private static void Arrange<T>(Panel host, List<T> order, Dictionary<T, FrameworkElement> controls)
        {
            var index = 0;
            foreach (var item in order.Where(controls.ContainsKey)) {
                var control = controls[item];
                if (host.Children.IndexOf(control) != index) {
                    host.Children.Remove(control);
                    host.Children.Insert(index, control);
                }
                var row = control is Expander group ? (DockPanel)group.Header : (DockPanel)control;
                var actions = (StackPanel)row.Children[0];
                ((Button)actions.Children[0]).IsEnabled = index > 0;
                ((Button)actions.Children[1]).IsEnabled = index < controls.Count - 1;
                index++;
            }
        }

        private static DockPanel Row(string label, bool visible, Action<bool> toggle, Action<int> move)
        {
            var row = new DockPanel { MinWidth = 170, Margin = new Thickness(2) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(buttons, Dock.Right);
            row.Children.Add(buttons);
            foreach (var delta in new[] { -1, 1 }) {
                var direction = delta;
                var caption = delta < 0 ? "Move " + label + " up" : "Move " + label + " down";
                var button = new Button { Content = delta < 0 ? "↑" : "↓", ToolTip = caption,
                    MinWidth = 26, Margin = new Thickness(2, 0, 0, 0) };
                AutomationProperties.SetName(button, caption);
                button.Click += (s, e) => { move(direction); e.Handled = true; };
                buttons.Children.Add(button);
            }
            var check = new CheckBox { Content = label, IsChecked = visible, VerticalAlignment = VerticalAlignment.Center };
            check.Click += (s, e) => { toggle(check.IsChecked == true); e.Handled = true; };
            row.Children.Add(check);
            return row;
        }
    }
}
