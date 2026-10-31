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
        private readonly Action<Func<FilterPresetSettings, FilterPresetSettings>> apply;
        private readonly Dictionary<string, bool> expanded = new Dictionary<string, bool>();
        private FilterPresetSettings shown;

        internal NavigatorFilterPanel(IPlayniteAPI api, StackPanel host,
            Action<Func<FilterPresetSettings, FilterPresetSettings>> apply)
        {
            this.api = api;
            this.host = host;
            this.apply = apply;
        }

        internal void Refresh(FilterPresetSettings current, bool force = false)
        {
            if (!force && NavigatorFilterComposer.Same(shown, current)) return;
            shown = NavigatorFilterComposer.Copy(current);
            host.Children.Clear();
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
        }

        private void AddBooleanSection(string title,
            IEnumerable<Tuple<string, bool, Func<FilterPresetSettings, FilterPresetSettings>>> choices, bool defaultOpen)
        {
            var panel = new StackPanel();
            foreach (var item in choices) {
                var box = Choice(item.Item1, item.Item2);
                box.Click += (s, e) => apply(item.Item3);
                panel.Children.Add(box);
            }
            Section(title, panel, defaultOpen);
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
            }
            Section(title + (selectedIds.Count > 0 ? " (" + selectedIds.Count + ")" : ""), panel, false, title);
        }

        private static CheckBox Choice(string label, bool selected) => new CheckBox {
            Content = label, IsChecked = selected, Foreground = Brushes.AliceBlue,
            Margin = new Thickness(4, 2, 2, 2), ToolTip = label
        };

        private void Section(string title, UIElement content, bool defaultOpen, string key = null)
        {
            key = key ?? title;
            var section = new Expander { Header = title, Content = content,
                Foreground = Brushes.AliceBlue, Margin = new Thickness(0, 0, 0, 5),
                IsExpanded = expanded.TryGetValue(key, out var prior) ? prior : defaultOpen };
            section.Expanded += (s, e) => expanded[key] = true;
            section.Collapsed += (s, e) => expanded[key] = false;
            host.Children.Add(section);
        }

    }
}
