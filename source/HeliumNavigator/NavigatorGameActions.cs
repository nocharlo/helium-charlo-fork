using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace HeliumNavigator
{
    internal enum NavigatorLaunchAction { Unavailable, Play, Install }

    internal sealed class NavigatorGameActions
    {
        private readonly Func<Guid, Game> findGame;
        private readonly Action<Guid> play, install;
        private readonly Func<Guid, bool?> edit;
        private readonly Action<Game> update;
        private readonly Action<Guid, GameAction> startAction;

        internal NavigatorGameActions(Func<Guid, Game> findGame, Action<Guid> play, Action<Guid> install,
            Func<Guid, bool?> edit, Action<Game> update, Action<Guid, GameAction> startAction = null)
        {
            this.findGame = findGame;
            this.play = play;
            this.install = install;
            this.edit = edit;
            this.update = update;
            this.startAction = startAction;
        }

        internal static Func<Guid, bool?> BindNativeEditor(IMainViewAPI mainView)
        {
            return mainView == null ? null : new Func<Guid, bool?>(mainView.OpenEditDialog);
        }

        internal void PopulateCustomActions(System.Windows.Controls.MenuItem menu, Guid gameId, Action<Exception> report)
        {
            menu.Items.Clear();
            var offered = findGame(gameId)?.GameActions?.Select(action => action?.GetCopy()).ToArray()
                ?? new GameAction[0];
            for (var index = 0; index < offered.Length; index++) {
                var action = offered[index];
                if (action == null || action.IsPlayAction || !Enum.IsDefined(typeof(GameActionType), action.Type)) continue;
                var capturedIndex = index;
                var item = new System.Windows.Controls.MenuItem { Header = action.Name };
                item.Click += (s, e) => {
                    e.Handled = true;
                    try {
                        var game = findGame(gameId);
                        var current = game?.GameActions;
                        // Actions have no persistent ID. Reject a stale menu instead of matching by name.
                        if (startAction == null || game == null || game.IsRunning || game.IsLaunching ||
                            game.IsInstalling || game.IsUninstalling || current == null || current.Count != offered.Length ||
                            !current.Zip(offered, (left, right) => GameAction.Equals(left, right)).All(equal => equal)) return;
                        startAction(gameId, current[capturedIndex]);
                    } catch (Exception error) { report(error); }
                };
                menu.Items.Add(item);
            }
            menu.Visibility = menu.HasItems ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        internal bool HasNativeEditor => edit != null;

        internal NavigatorLaunchAction LaunchAction(Guid id)
        {
            var game = findGame(id);
            if (game == null || game.IsRunning || game.IsLaunching || game.IsInstalling || game.IsUninstalling)
                return NavigatorLaunchAction.Unavailable;
            return game.IsInstalled ? NavigatorLaunchAction.Play : NavigatorLaunchAction.Install;
        }

        internal void Launch(Guid id, NavigatorLaunchAction offered)
        {
            // A change while the menu is open must not turn an Install click into Play or vice versa.
            if (LaunchAction(id) != offered) return;
            if (offered == NavigatorLaunchAction.Play) play(id);
            else if (offered == NavigatorLaunchAction.Install) install(id);
        }

        internal void Edit(Guid id)
        {
            if (edit != null && findGame(id) != null) edit(id);
        }

        internal bool RunNative(Guid id, ICommand command, Func<Game, bool> available)
        {
            var game = findGame(id);
            if (game == null || command == null || !available(game) || !command.CanExecute(game)) return false;
            command.Execute(game);
            return true;
        }

        internal static bool HasExistingDirectory(string path) =>
            !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && Directory.Exists(path);

        internal static bool HasExistingManual(string path) =>
            !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && File.Exists(path);

        internal static Uri WebLink(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)) return null;
            return uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp ? uri : null;
        }

        internal bool SetFavorite(Guid id, bool value)
        {
            var game = findGame(id);
            if (game == null || game.Favorite == value) return false;
            var updated = game.GetCopy();
            updated.Favorite = value;
            update(updated);
            return true;
        }

        internal bool SetHidden(Guid id, bool value)
        {
            var game = findGame(id);
            if (game == null || game.Hidden == value) return false;
            var updated = game.GetCopy();
            updated.Hidden = value;
            update(updated);
            return true;
        }

        internal bool ToggleCategory(Guid id, Category category)
        {
            var game = findGame(id);
            if (game == null || category == null) return false;

            var categories = game.CategoryIds == null ? new List<Guid>() : new List<Guid>(game.CategoryIds);
            if (categories.RemoveAll(categoryId => categoryId == category.Id) == 0) categories.Add(category.Id);
            var updated = game.GetCopy();
            updated.CategoryIds = categories.Count == 0 ? null : categories;
            update(updated);
            return true;
        }

        internal bool SetCompletionStatus(Guid id, Guid statusId, CompletionStatus status)
        {
            var game = findGame(id);
            if (game == null || game.CompletionStatusId == statusId ||
                (statusId != Guid.Empty && status?.Id != statusId)) return false;

            var updated = game.GetCopy();
            updated.CompletionStatusId = statusId;
            update(updated);
            return true;
        }
    }
}
