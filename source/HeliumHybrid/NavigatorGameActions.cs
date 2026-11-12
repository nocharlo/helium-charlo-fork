using System;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace HeliumHybrid
{
    internal enum NavigatorLaunchAction { Unavailable, Play, Install }

    internal sealed class NavigatorGameActions
    {
        private readonly Func<Guid, Game> findGame;
        private readonly Action<Guid> play, install;
        private readonly Func<Guid, bool?> edit;

        internal NavigatorGameActions(Func<Guid, Game> findGame, Action<Guid> play, Action<Guid> install, Func<Guid, bool?> edit)
        {
            this.findGame = findGame;
            this.play = play;
            this.install = install;
            this.edit = edit;
        }

        internal static Func<Guid, bool?> BindNativeEditor(IMainViewAPI mainView)
        {
            // The host supplies the SDK interface. OpenEditDialog was added in SDK 6.13.
            var method = typeof(IMainViewAPI).GetMethod("OpenEditDialog", new[] { typeof(Guid) });
            return method == null || mainView == null ? null
                : (Func<Guid, bool?>)Delegate.CreateDelegate(typeof(Func<Guid, bool?>), mainView, method, false);
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
    }
}
