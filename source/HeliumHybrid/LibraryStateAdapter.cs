using System;
using System.Collections.Generic;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

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
        internal HashSet<Guid> SelectedIds()
        {
            var selected = SelectedGames;
            return selected == null ? new HashSet<Guid>() : new HashSet<Guid>(selected.Select(game => game.Id));
        }
        internal Guid[] SortedFilteredIds() => api.MainView.FilteredGames?.Select(game => game.Id).OrderBy(id => id).ToArray();
        internal string SortOrder => api.MainView.SortOrder.ToString();
        internal string SortDirection => api.MainView.SortOrderDirection.ToString();
        internal string Grouping => api.MainView.Grouping.ToString();
        internal string ResolveIcon(string icon) => api.Database.GetFullFilePath(icon);

        internal NavigatorViewSnapshot ViewState(int navigatorGrouping)
        {
            return new NavigatorViewSnapshot {
                SortOrder = SortOrder,
                SortDirection = SortDirection,
                Grouping = Grouping,
                PresetId = api.MainView.GetActiveFilterPreset(),
                NavigatorGrouping = navigatorGrouping
            };
        }

        internal NavigatorViewSnapshot DiagnosticViewState(int navigatorGrouping)
        {
            var state = ViewState(navigatorGrouping);
            var filter = api.MainView.GetCurrentFilterSettings()?.CompletionStatuses;
            state.CompletionStatusFilterActive = filter != null &&
                (filter.Ids?.Count > 0 || !string.IsNullOrWhiteSpace(filter.Text));
            return state;
        }

        internal IEnumerable<NavigatorSource> Sources(IEnumerable<Game> games, int grouping)
        {
            var libraries = grouping == (int)NavigatorGroupPolicy.Library
                ? api.Addons.Plugins.OfType<LibraryPlugin>().ToDictionary(p => p.Id, p => p.Name) : null;
            return games.Select(game => new NavigatorSource {
                Id = game.Id, Name = game.Name, SortingName = game.SortingName,
                Icon = game.Icon, Groups = Groups(game, grouping, libraries),
                IsInstalled = game.IsInstalled, Favorite = game.Favorite,
                LastActivity = game.LastActivity, Playtime = game.Playtime,
                ReleaseDate = game.ReleaseDate?.Date, Added = game.Added, Modified = game.Modified,
                PlayCount = game.PlayCount, InstallSize = game.InstallSize, UserScore = game.UserScore,
                CommunityScore = game.CommunityScore, CriticScore = game.CriticScore,
                CompletionStatus = game.CompletionStatus?.Name
            });
        }

        private static IEnumerable<NavigatorGroupIdentity> Metadata(IEnumerable<DatabaseObject> values) =>
            values?.Where(value => value != null && !string.IsNullOrWhiteSpace(value.Name))
                .Select(value => new NavigatorGroupIdentity("metadata:" + value.Id, value.Name));

        internal static IEnumerable<NavigatorGroupIdentity> Groups(Game game, int grouping, IDictionary<Guid, string> libraries = null)
        {
            switch (grouping) {
                case 1: case 5: return Metadata(game.Series);
                case 2: case 6: return Metadata(game.Categories);
                case 3: case 7: return Metadata(game.Platforms);
                case 4: return Metadata(new[] { game.CompletionStatus });
                case (int)NavigatorGroupPolicy.Genre: return Metadata(game.Genres);
                case (int)NavigatorGroupPolicy.Tags: return Metadata(game.Tags);
                case (int)NavigatorGroupPolicy.Developer: return Metadata(game.Developers);
                case (int)NavigatorGroupPolicy.Publisher: return Metadata(game.Publishers);
                case (int)NavigatorGroupPolicy.Features: return Metadata(game.Features);
                case (int)NavigatorGroupPolicy.Source: return Metadata(new[] { game.Source });
                case (int)NavigatorGroupPolicy.Region: return Metadata(game.Regions);
                case (int)NavigatorGroupPolicy.AgeRating: return Metadata(game.AgeRatings);
                case (int)NavigatorGroupPolicy.Library:
                    if (game.PluginId == Guid.Empty) return new[] { new NavigatorGroupIdentity("library:manual", "Manually Added") };
                    string name;
                    return new[] { new NavigatorGroupIdentity("library:" + game.PluginId,
                        libraries != null && libraries.TryGetValue(game.PluginId, out name) ? name : "Unavailable library") };
                default: return null;
            }
        }
    }
}
