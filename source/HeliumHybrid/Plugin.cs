using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.IO;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System.Reflection;
[assembly: AssemblyVersion("0.2.4.0")]
[assembly: AssemblyFileVersion("0.2.4.0")]
namespace HeliumHybrid
{
    public sealed class HybridPlugin : GenericPlugin
    {
        public override Guid Id { get; } = new Guid("b59e3c9f-9c52-4582-a332-0716b56cf023");
        internal event Action SelectionChanged;
        internal event Action SettingsChanged;
        internal string DataDirectory => GetPluginUserDataPath();
        internal Preferences Settings { get; }
        private readonly bool canSaveSettings;
        public HybridPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = true };
            Settings = SettingsFile.Load<Preferences>(Path.Combine(DataDirectory, "navigator.xml"),
                error => LogManager.GetLogger().Warn(error, "Helium Charlo: could not read navigator settings; preserving original."),
                out canSaveSettings);
            Settings.MigrateNavigation();
            Settings.Changed = () => SettingsChanged?.Invoke();
            Settings.Save = SaveSettings;
            AddCustomElementSupport(new AddCustomElementSupportArgs {
                SourceName = "HeliumCharlo", ElementList = new List<string> { "Navigator" }
            });
        }
        public override ISettings GetSettings(bool firstRunSettings) => Settings;
        public override UserControl GetSettingsView(bool firstRunView) => new NavigatorSettingsView();
        internal void SaveSettings()
        {
            if (!canSaveSettings) {
                LogManager.GetLogger().Warn("Helium Charlo: settings backup failed; refusing to overwrite the original file.");
                return;
            }
            try {
                SettingsFile.Save(Path.Combine(DataDirectory, "navigator.xml"), Settings);
            } catch (Exception error) { LogManager.GetLogger().Warn(error, "Helium Charlo: could not save settings."); }
        }
        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            if (args.Mode != ApplicationMode.Desktop || args.Name != "Navigator") return null;
            try { return new Navigator(PlayniteApi, this); }
            catch (Exception e) { LogManager.GetLogger().Error(e, "Helium Charlo: could not create the navigator."); return null; }
        }
        public override void OnGameSelected(OnGameSelectedEventArgs args) { SelectionChanged?.Invoke(); }
    }
}
