using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System.Reflection;
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]
namespace HeliumHybrid
{
    public sealed class HybridPlugin : GenericPlugin
    {
        public override Guid Id { get; } = new Guid("84fd550a-26c3-43d6-8c09-77c48e5a27a6");
        internal event Action SelectionChanged;
        internal Navigator ActiveNavigator;
        public AdaptiveState State { get; } = new AdaptiveState();
        internal Preferences Prefs { get; private set; }
        internal string DataDirectory => GetPluginUserDataPath();
        internal void Save()
        {
            try { Prefs.Save(DataDirectory); }
            catch (Exception e) { LogManager.GetLogger().Warn(e, "Helium Charlo: falha ao salvar preferências."); }
        }
        internal void SetCollapsed(bool value)
        {
            if (ActiveNavigator != null) ActiveNavigator.SetCollapsed(value);
            else {
                if (PlayniteApi.MainView.ActiveDesktopView == DesktopView.Grid) Prefs.Collapsed = value;
                else Prefs.CollapsedOther = value;
                State.NavigatorVisible = !value;
                Save();
            }
        }
        public HybridPlugin(IPlayniteAPI api) : base(api)
        {
            Prefs = Preferences.Load(DataDirectory, api.Paths.ExtensionsDataPath);
            State.NavigatorVisible = !(api.MainView.ActiveDesktopView == DesktopView.Grid ? Prefs.Collapsed : Prefs.CollapsedOther);
            State.BackCommand = new ActionCommand(() => ActiveNavigator?.ReturnToGrid());
            AddSettingsSupport(new AddSettingsSupportArgs { SourceName = "HeliumCharlo", SettingsRoot = nameof(State) });
            AddCustomElementSupport(new AddCustomElementSupportArgs {
                SourceName = "HeliumCharlo", ElementList = new List<string> { "Navigator", "Reveal" }
            });
        }
        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            if (args.Mode != ApplicationMode.Desktop) return null;
            try {
                if (args.Name == "Navigator") return ActiveNavigator = new Navigator(PlayniteApi, this);
                if (args.Name == "Reveal") return new RevealControl(this);
                return null;
            }
            catch (Exception e) { LogManager.GetLogger().Error(e, "Helium Hybrid: falha ao criar navegador."); return null; }
        }
        public override void OnGameSelected(OnGameSelectedEventArgs args) { SelectionChanged?.Invoke(); }
    }
}
