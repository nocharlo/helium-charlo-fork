using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace HeliumHybrid
{
    internal static class NavigatorPluginMenu
    {
        internal static void Populate(MenuItem menu, FrameworkElement owner, Game game,
            Func<IEnumerable<Plugin>> loadedPlugins, Func<Guid, Game> findGame, Action<Exception> report)
        {
            menu.Items.Clear();
            if (game != null) {
                foreach (var plugin in loadedPlugins()) {
                    if (plugin == null) continue;
                    try {
                        var items = plugin.GetGameMenuItems(new GetGameMenuItemsArgs {
                            Games = new List<Game> { game }
                        })?.ToArray();
                        if (items == null) continue;
                        var labels = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                        foreach (var item in items) {
                            if (item == null || string.IsNullOrWhiteSpace(item.Description)) continue;
                            var section = (item.MenuSection ?? "").Split('|').Select(part => part.Trim())
                                .Where(part => part.Length != 0).ToArray();
                            if (item.Description == "-") {
                                var existing = Section(menu, section, false)?.Items;
                                if (existing?.Count > 0 && !(existing[existing.Count - 1] is Separator))
                                    existing.Add(new Separator());
                                continue;
                            }
                            if (!labels.Add(string.Join("|", section) + "|" + item.Description)) continue;
                            var entry = new MenuItem { Header = Label(item.Description), IsEnabled = item.Action != null };
                            if (!string.IsNullOrWhiteSpace(item.Icon)) NavigatorMenuIcons.Set(owner, entry, item.Icon);
                            var id = game.Id;
                            entry.Click += (s, e) => {
                                try {
                                    var current = findGame(id);
                                    if (current == null || !loadedPlugins().Any(loaded => ReferenceEquals(loaded, plugin))) return;
                                    item.Action?.Invoke(new GameMenuItemActionArgs {
                                        Games = new List<Game> { current }, SourceItem = item
                                    });
                                } catch (Exception error) { report(error); }
                            };
                            Section(menu, section, true).Items.Add(entry);
                        }
                    } catch (Exception error) { report(error); }
                }
            }
            TrimSeparators(menu);
            menu.Visibility = menu.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static MenuItem Section(MenuItem menu, IEnumerable<string> path, bool create)
        {
            foreach (var part in path) {
                var label = Label(part);
                var child = menu.Items.OfType<MenuItem>().FirstOrDefault(entry =>
                    Equals(entry.Header, label) && Equals(entry.Tag, typeof(NavigatorPluginMenu)));
                if (child == null) {
                    if (!create) return null;
                    child = new MenuItem { Header = label, Tag = typeof(NavigatorPluginMenu) };
                    menu.Items.Add(child);
                }
                menu = child;
            }
            return menu;
        }

        private static void TrimSeparators(MenuItem menu)
        {
            foreach (var child in menu.Items.OfType<MenuItem>()) TrimSeparators(child);
            if (menu.Items.Count > 0 && menu.Items[menu.Items.Count - 1] is Separator)
                menu.Items.RemoveAt(menu.Items.Count - 1);
        }

        private static string Label(string text) => text.Replace("_", "__");
    }
}
