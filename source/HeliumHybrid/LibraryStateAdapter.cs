using System;
using System.Collections.Generic;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace HeliumHybrid
{
    // The only place where Navigator reads live games and view state from Playnite.
    internal sealed class LibraryStateAdapter
    {
        private readonly IPlayniteAPI api;

        internal LibraryStateAdapter(IPlayniteAPI api) { this.api = api; }

        internal DesktopView ActiveDesktopView => api.MainView.ActiveDesktopView;
        internal IEnumerable<Game> FilteredGames => api.MainView.FilteredGames ?? new List<Game>();
        internal IEnumerable<Game> SelectedGames => api.MainView.SelectedGames;
        internal bool IsSelected(Guid id) => SelectedGames?.Any(game => game.Id == id) == true;
        internal HashSet<Guid> SelectedIds() => SelectedGames?.Select(game => game.Id).ToHashSetCompat() ?? new HashSet<Guid>();
        internal Guid[] SortedFilteredIds() => api.MainView.FilteredGames?.Select(game => game.Id).OrderBy(id => id).ToArray();
        internal string SortOrder => api.MainView.SortOrder.ToString();
        internal string SortDirection => api.MainView.SortOrderDirection.ToString();
        internal string Grouping => api.MainView.Grouping.ToString();
        internal string ResolveIcon(string icon) => api.Database.GetFullFilePath(icon);

        internal IEnumerable<NavigatorSource> Sources(IEnumerable<Game> games, int grouping)
        {
            return games.Select(game => new NavigatorSource {
                Id = game.Id, Name = game.Name, SortingName = game.SortingName,
                Icon = game.Icon, GroupNames = GroupNames(game, grouping)
            });
        }

        private static IEnumerable<string> GroupNames(Game game, int grouping)
        {
            switch (grouping) {
                case 1: return game.Series?.Select(value => value.Name);
                case 2: return game.Categories?.Select(value => value.Name);
                case 3: return game.Platforms?.Select(value => value.Name);
                default: return null;
            }
        }
    }
}
