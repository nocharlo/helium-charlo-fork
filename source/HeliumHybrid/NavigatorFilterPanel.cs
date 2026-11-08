using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace HeliumHybrid
{
    // The panel edits Playnite's current global filter state, never a second local game list.
    internal sealed class NavigatorFilterPanel
    {
        private readonly IPlayniteAPI api;
        private readonly StackPanel host;
        private readonly WrapPanel active;
        private readonly TextBox search;
        private readonly TextBlock empty;
        private readonly Action<Func<FilterPresetSettings, FilterPresetSettings>> apply;
        private readonly Dictionary<string, bool> expanded = new Dictionary<string, bool>();
        private readonly List<Tuple<Expander, List<CheckBox>>> searchable = new List<Tuple<Expander, List<CheckBox>>>();
        private bool searching;
        private FilterPresetSettings shown;
        internal bool HasManagedSelection => active.Children.Count != 0;

        internal NavigatorFilterPanel(IPlayniteAPI api, StackPanel host, WrapPanel active, TextBox search, TextBlock empty,
            Action<Func<FilterPresetSettings, FilterPresetSettings>> apply)
        {
            this.api = api;
            this.host = host;
            this.active = active;
            this.search = search;
            this.empty = empty;
            this.apply = apply;
            search.TextChanged += (s, e) => ApplySearch();
        }

        internal void Refresh(FilterPresetSettings current, bool force = false)
        {
            if (!force && NavigatorFilterComposer.Same(shown, current)) return;
            shown = NavigatorFilterComposer.Copy(current);
            host.Children.Clear();
            active.Children.Clear();
            searchable.Clear();
            AddBooleanSection("Installation Status", new[] {
                Tuple.Create("Installed", current.IsInstalled, (Func<FilterPresetSettings, FilterPresetSettings>)(s => NavigatorFilterComposer.Installation(s, !s.IsInstalled, s.IsUnInstalled))),
                Tuple.Create("Not installed", current.IsUnInstalled, (Func<FilterPresetSettings, FilterPresetSettings>)(s => NavigatorFilterComposer.Installation(s, s.IsInstalled, !s.IsUnInstalled)))
            }, true);
            AddBooleanSection("Favorites", new[] {
                Tuple.Create("Favorites only", current.Favorite, (Func<FilterPresetSettings, FilterPresetSettings>)(s => NavigatorFilterComposer.Favorite(s, !s.Favorite)))
            }, true);
            AddIds("Library", NavigatorFilterField.Library,
                api.Addons.Plugins.OfType<LibraryPlugin>().Select(p => Tuple.Create(p.Id, p.Name)), current);
            AddIds("Source", NavigatorFilterField.Source,
                api.Database.Sources.Select(p => Tuple.Create(p.Id, p.Name)), current);
            AddIds("Platform", NavigatorFilterField.Platform,
                api.Database.Platforms.Select(p => Tuple.Create(p.Id, p.Name)), current);
            AddIds("Genre", NavigatorFilterField.Genre,
                api.Database.Genres.Select(p => Tuple.Create(p.Id, p.Name)), current);
            AddIds("Category", NavigatorFilterField.Category,
                api.Database.Categories.Select(p => Tuple.Create(p.Id, p.Name)), current);
            AddIds("Tags", NavigatorFilterField.Tag,
                api.Database.Tags.Select(p => Tuple.Create(p.Id, p.Name)), current);
            AddIds("Completion Status", NavigatorFilterField.CompletionStatus,
                api.Database.CompletionStatuses.Select(p => Tuple.Create(p.Id, p.Name)), current);
            ApplySearch();
        }

        private void AddBooleanSection(string title,
            IEnumerable<Tuple<string, bool, Func<FilterPresetSettings, FilterPresetSettings>>> choices, bool defaultOpen)
        {
            var panel = new StackPanel();
            foreach (var item in choices) {
                var box = Choice(item.Item1, item.Item2);
                box.Click += (s, e) => apply(item.Item3);
                panel.Children.Add(box);
                if (item.Item2) AddActive(item.Item1, item.Item3);
            }
            var section = Section(title, panel, defaultOpen);
            if (panel.Children.OfType<CheckBox>().Any(box => box.IsChecked == true)) {
                var clear = new Button { Content = "Clear", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 3, 3) };
                clear.Click += (s, e) => apply(state => title == "Installation Status"
                    ? NavigatorFilterComposer.Installation(state, false, false)
                    : NavigatorFilterComposer.Favorite(state, false));
                panel.Children.Add(clear);
            }
            searchable.Add(Tuple.Create(section, panel.Children.OfType<CheckBox>().ToList()));
        }

        private void AddIds(string title, NavigatorFilterField field,
            IEnumerable<Tuple<Guid, string>> available, FilterPresetSettings current)
        {
            var selected = NavigatorFilterComposer.IdField(current, field);
            var selectedIds = new HashSet<Guid>(selected?.Ids ?? new List<Guid>());
            var options = available.Where(p => p.Item1 != Guid.Empty)
                .GroupBy(p => p.Item1).Select(group => group.First())
                .Select(p => Tuple.Create(p.Item1, string.IsNullOrWhiteSpace(p.Item2) ? "Unnamed value" : p.Item2))
                .OrderBy(p => p.Item2, StringComparer.CurrentCultureIgnoreCase).ToList();
            foreach (var missing in selectedIds.Where(id => options.All(option => option.Item1 != id)))
                options.Add(Tuple.Create(missing, "Unavailable value (" + missing.ToString("N").Substring(0, 8) + ")"));
            var panel = new StackPanel();
            if (!string.IsNullOrWhiteSpace(selected?.Text))
                panel.Children.Add(new TextBlock { Text = "Additional text condition active in Playnite", TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.LightSteelBlue, FontSize = 11, Margin = new Thickness(3, 2, 0, 4) });
            if (options.Count == 0)
                panel.Children.Add(new TextBlock { Text = "No values available", Foreground = Brushes.LightSteelBlue,
                    Margin = new Thickness(3, 2, 0, 4) });
            foreach (var option in options) {
                var id = option.Item1;
                var box = Choice(option.Item2, selectedIds.Contains(id));
                box.Click += (s, e) => apply(state => NavigatorFilterComposer.ToggleId(state, field, id));
                panel.Children.Add(box);
                if (selectedIds.Contains(id)) AddActive(option.Item2, state => NavigatorFilterComposer.ToggleId(state, field, id));
            }
            if (selectedIds.Count > 0) {
                var clear = new Button { Content = "Clear", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 3, 3) };
                clear.Click += (s, e) => apply(state => NavigatorFilterComposer.ClearSection(state, field));
                panel.Children.Insert(0, clear);
            }
            var section = Section(title + (selectedIds.Count > 0 ? " (" + selectedIds.Count + ")" : ""), panel, false, title);
            searchable.Add(Tuple.Create(section, panel.Children.OfType<CheckBox>().ToList()));
        }

        private void AddActive(string label, Func<FilterPresetSettings, FilterPresetSettings> remove)
        {
            var button = new Button { Content = label + "  ×", ToolTip = "Remove " + label,
                Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(6, 3, 6, 3) };
            button.Click += (s, e) => apply(remove);
            active.Children.Add(button);
        }

        private void ApplySearch()
        {
            if (empty == null) return;
            var query = search.Text?.Trim() ?? "";
            var found = false;
            searching = true;
            try {
            foreach (var entry in searchable) {
                var matches = false;
                foreach (var box in entry.Item2) {
                    var visible = NavigatorFilterComposer.OptionVisible(box.Content as string, query, box.IsChecked == true);
                    box.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                    // Selected options stay accessible, but they are not search hits.
                    matches |= NavigatorFilterComposer.OptionMatches(box.Content as string, query);
                }
                entry.Item1.Visibility = matches || entry.Item2.Any(box => box.IsChecked == true) || query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (query.Length > 0 && matches) entry.Item1.IsExpanded = true;
                else if (query.Length == 0 && expanded.TryGetValue((string)entry.Item1.Tag, out var prior)) entry.Item1.IsExpanded = prior;
                found |= matches;
            }
            empty.Visibility = query.Length > 0 && !found ? Visibility.Visible : Visibility.Collapsed;
            } finally { searching = false; }
        }

        private static CheckBox Choice(string label, bool selected) => new CheckBox {
            Content = label, IsChecked = selected, Foreground = Brushes.AliceBlue,
            Margin = new Thickness(4, 2, 2, 2), ToolTip = label
        };

        private Expander Section(string title, UIElement content, bool defaultOpen, string key = null)
        {
            key = key ?? title;
            var section = new Expander { Header = title, Content = content, Tag = key,
                Foreground = Brushes.AliceBlue, Margin = new Thickness(0, 0, 0, 5),
                IsExpanded = expanded.TryGetValue(key, out var prior) ? prior : defaultOpen };
            section.Expanded += (s, e) => { if (!searching) expanded[key] = true; };
            section.Collapsed += (s, e) => { if (!searching) expanded[key] = false; };
            host.Children.Add(section);
            return section;
        }

    }
}
