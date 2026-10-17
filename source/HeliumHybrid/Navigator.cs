using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Xml.Serialization;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;
namespace HeliumHybrid
{
    public class Preferences
    {
        public double Width { get; set; } = 230;
        public bool Collapsed { get; set; }
        public bool CollapsedOther { get; set; } = true;
        public int Grouping { get; set; }
        public int LayoutMode { get; set; } // 0 auto; 1 compact; 2 wide
        public bool SteamNavigation { get; set; }
        public bool LocateCover { get; set; }
        public bool PreserveScroll { get; set; } = true;

        public static Preferences Load(string ownDirectory, string extensionDataPath)
        {
            var own = Path.Combine(ownDirectory, "navigator.xml");
            var old = Path.Combine(extensionDataPath, "e9e0f96a-b087-4f96-a157-710e6b7da49c", "navigator.xml");
            // Read a previous version only once. Never change the 0.1 file.
            foreach (var path in new[] { own, old }) {
                try {
                    using (var reader = File.OpenRead(path)) {
                        var result = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(reader);
                        result.Width = Logic.ClampWidth(result.Width);
                        return result;
                    }
                } catch { }
            }
            return new Preferences();
        }
        public void Save(string directory)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "navigator.xml");
            var temp = path + ".tmp";
            using (var writer = File.Create(temp)) new XmlSerializer(typeof(Preferences)).Serialize(writer, this);
            File.Copy(temp, path, true); File.Delete(temp);
        }
    }
    public sealed class GameRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string SortName { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
        public string Key { get; set; }
        private string iconPath;
        public string IconPath { get => iconPath; set { if (iconPath != value) { iconPath = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon))); } } }
        public ImageSource Icon => IconCache.Load(IconPath);
    }
    internal static class IconCache
    {
        private static readonly Dictionary<string, ImageSource> cache = new Dictionary<string, ImageSource>();
        internal static ImageSource Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (cache.TryGetValue(path, out var image)) return image;
            try {
                // Local files only, decoded at visible-row size. No online requests.
                if (!Path.IsPathRooted(path) || !File.Exists(path)) return null;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                    var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 32; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); image = bitmap;
                }
            } catch { image = null; }
            if (cache.Count >= 512) cache.Clear();
            cache[path] = image; return image;
        }
    }
    public sealed class Navigator : PluginUserControl
    {
        private readonly IPlayniteAPI api;
        private readonly HybridPlugin plugin;
        private readonly Grid root;
        private readonly ListBox list;
        private readonly TextBox search;
        private readonly ComboBox grouping;
        private readonly TextBlock count, status;
        private readonly Button toggle;
        private readonly Thumb grip;
        private readonly Popup options;
        private readonly Button optionsButton;
        private readonly ComboBox layoutMode;
        private readonly CheckBox steamMode, locateCover, preserveScroll;
        private Control gridView;
        private FrameworkElement hostPanel;
        private FrameworkElement nativeDetails;
        private ListBox nativeGridList;
        private bool suppressNativeOpen;
        private double rememberedDetailWidth = 420;
        private double savedGridOffset;
        private bool hasGridOffset;
        private int refreshQueued;

        private readonly DispatcherTimer timer, searchTimer;
        private readonly Preferences prefs;

        private bool active, syncing, reportedError;
        private int dirty = 1;
        private int fullDirty = 1;
        private readonly object updateGate = new object();
        private readonly HashSet<Guid> pendingGameIds = new HashSet<Guid>();
        private Guid? clickOnSelectedRow;
        private Guid[] lastIds = new Guid[0];
        private List<GameRow> rows = new List<GameRow>();
        private ListCollectionView view;
        public Navigator(IPlayniteAPI api, HybridPlugin plugin)
        {
            this.api = api; this.plugin = plugin;
            prefs = plugin.Prefs;
            prefs.Width = Logic.ClampWidth(prefs.Width);
            using (var stream = typeof(Navigator).Assembly.GetManifestResourceStream("HeliumHybrid.Navigator.xaml"))
                root = (Grid)XamlReader.Load(stream);
            Content = root;
            list = (ListBox)root.FindName("Games"); search = (TextBox)root.FindName("Search");
            grouping = (ComboBox)root.FindName("Grouping"); count = (TextBlock)root.FindName("Count");
            status = (TextBlock)root.FindName("Status"); toggle = (Button)root.FindName("CollapseButton");
            grip = (Thumb)root.FindName("ResizeGrip");
            optionsButton = (Button)root.FindName("OptionsButton");
            options = (Popup)root.FindName("OptionsPopup");
            layoutMode = (ComboBox)root.FindName("LayoutMode");
            steamMode = (CheckBox)root.FindName("SteamMode");
            locateCover = (CheckBox)root.FindName("LocateCover");
            preserveScroll = (CheckBox)root.FindName("PreserveScroll");
            layoutMode.SelectedIndex = Math.Max(0, Math.Min(2, prefs.LayoutMode));
            steamMode.IsChecked = prefs.SteamNavigation; locateCover.IsChecked = prefs.LocateCover;
            preserveScroll.IsChecked = prefs.PreserveScroll;
            optionsButton.Click += (s,e) => options.IsOpen = !options.IsOpen;
            layoutMode.SelectionChanged += (s,e) => { prefs.LayoutMode = layoutMode.SelectedIndex; RecomputeLayout(); Save(); };
            steamMode.Checked += (s,e) => { prefs.SteamNavigation = true; Save(); };
            steamMode.Unchecked += (s,e) => { prefs.SteamNavigation = false; ReturnToGrid(); Save(); };
            locateCover.Checked += (s,e) => { prefs.LocateCover = true; Save(); };
            locateCover.Unchecked += (s,e) => { prefs.LocateCover = false; Save(); };
            preserveScroll.Checked += (s,e) => { prefs.PreserveScroll = true; Save(); };
            preserveScroll.Unchecked += (s,e) => { prefs.PreserveScroll = false; Save(); };
            grouping.SelectedIndex = Math.Max(0, Math.Min(3, prefs.Grouping));
            timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(2500) };
            timer.Tick += (s, e) => { RecomputeLayout(); Refresh(); };
            searchTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(180) };
            searchTimer.Tick += (s, e) => { searchTimer.Stop(); ApplySearch(); };
            search.TextChanged += (s, e) => { searchTimer.Stop(); searchTimer.Start(); };
            grouping.SelectionChanged += (s, e) => { prefs.Grouping = grouping.SelectedIndex; Interlocked.Exchange(ref fullDirty, 1); Interlocked.Exchange(ref dirty, 1); Refresh(); Save(); };
            toggle.Click += (s, e) => SetCollapsed(true);
            grip.DragDelta += (s, e) => { prefs.Width = Logic.ClampWidth(prefs.Width + e.HorizontalChange); ApplySize(); RecomputeLayout(); };
            grip.DragCompleted += (s, e) => Save();
            list.PreviewMouseLeftButtonDown += (s, e) => {
                var row = RowUnderPointer(e.OriginalSource as DependencyObject)?.DataContext as GameRow;
                clickOnSelectedRow = row != null && (list.SelectedItem as GameRow)?.Id == row.Id ? (Guid?)row.Id : null;
            };
            list.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) => {
                var row = RowUnderPointer(e.OriginalSource as DependencyObject)?.DataContext as GameRow;
                if (prefs.SteamNavigation && row != null && clickOnSelectedRow == row.Id && !plugin.State.IsDetailPage)
                    Safe(() => SelectGame(row.Id));
                clickOnSelectedRow = null;
            }), true);
            list.SelectionChanged += (s, e) => {
                if (!syncing && list.SelectedItem is GameRow row) Safe(() => SelectGame(row.Id));
            };
            list.MouseDoubleClick += (s, e) => { if (RowUnderPointer(e.OriginalSource as DependencyObject) != null) StartSelected(); };
            list.PreviewKeyDown += (s, e) => { if (e.Key == Key.Enter && list.SelectedItem != null) { StartSelected(); e.Handled = true; } };
            // These commands use public APIs. The native grid keeps its complete original menu.
            list.ContextMenuOpening += ContextOpening;
            var menu = new ContextMenu(); var play = new MenuItem { Header = "Jogar" }; play.Click += (s, e) => StartSelected();
            
            menu.Items.Add(play); list.ContextMenu = menu;
            Loaded += OnLoaded; Unloaded += OnUnloaded;
            ApplySize();
        }
        private ListBoxItem RowUnderPointer(DependencyObject source)
        {
            return source == null ? null : ItemsControl.ContainerFromElement(list, source) as ListBoxItem;
        }
        private void ContextOpening(object sender, ContextMenuEventArgs e)
        {
            var container = RowUnderPointer(Mouse.DirectlyOver as DependencyObject);
            if (container == null) { e.Handled = true; return; }
            syncing = true;
            try { list.SelectedItem = container.DataContext; } finally { syncing = false; }
        }
        private void SelectGame(Guid id)
        {
            if (prefs.SteamNavigation) CaptureGridOffset();
            api.MainView.SelectGame(id);
            // The selection event alone never opens the detail page; only this explicit action does.
            if (prefs.SteamNavigation) plugin.State.IsDetailPage = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => Safe(() => {
                if (!active || !(api.MainView.SelectedGames?.Any(g => g.Id == id) ?? false)) return;
                if (prefs.LocateCover && !prefs.SteamNavigation) LocateSelectedCover();
                OpenNativeDetails(prefs.SteamNavigation);
            })));
        }
        private void OpenNativeDetails(bool explicitPage)
        {
            var host = GetHost();
            var command = host?.Tag as ICommand;
            if (host?.DataContext == null || command == null || !command.CanExecute(host.DataContext)) return;
            suppressNativeOpen = !explicitPage;
            try { command.Execute(host.DataContext); }
            finally { suppressNativeOpen = false; }
        }
        private static DependencyObject ParentOf(DependencyObject node)
        {
            return node is Visual || node is Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        private ContentControl GetHost()
        {
            DependencyObject parent = this;
            while (parent != null) {
                var host = parent as ContentControl;
                if (host != null && host.Name == "HeliumCharlo_Navigator") return host;
                parent = ParentOf(parent);
            }
            return null;
        }
        private ScrollViewer GridScroll()
        {
            if (nativeGridList == null) return null;
            nativeGridList.ApplyTemplate();
            return FindChild<ScrollViewer>(nativeGridList);
        }
        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found) return found;
                var nested = FindChild<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }
        private void CaptureGridOffset()
        {
            if (!prefs.PreserveScroll) return;
            var scroll = GridScroll();
            if (scroll != null) { savedGridOffset = scroll.VerticalOffset; hasGridOffset = true; }
        }
        private void LocateSelectedCover()
        {
            if (nativeGridList?.SelectedItem != null) nativeGridList.ScrollIntoView(nativeGridList.SelectedItem);
        }
        internal void ReturnToGrid()
        {
            plugin.State.IsDetailPage = false;
            if (!prefs.PreserveScroll || !hasGridOffset) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
                var scroll = GridScroll();
                if (scroll != null) scroll.ScrollToVerticalOffset(savedGridOffset);
                hasGridOffset = false;
            }));
        }
        private void NativeDetailsVisibilityChanged(object sender, EventArgs e)
        {
            if (!active || nativeDetails == null || !plugin.State.InGrid) return;
            if (nativeDetails.Visibility == Visibility.Collapsed && plugin.State.IsDetailPage) ReturnToGrid();
            else if (nativeDetails.Visibility == Visibility.Visible && plugin.State.IsCompact && !suppressNativeOpen)
                plugin.State.IsDetailPage = true;
        }
        private void GridMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!plugin.State.IsCompact || nativeGridList == null) return;
            DependencyObject node = e.OriginalSource as DependencyObject;
            while (node != null && node != nativeGridList) {
                if ((node as FrameworkElement)?.Name == "PART_ButtonInfo") {
                    CaptureGridOffset();
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
                        if (active) { plugin.State.IsDetailPage = true; OpenNativeDetails(true); }
                    }));
                    break;
                }
                node = ParentOf(node);
            }
        }
        private void BindHost()
        {
            var host = GetHost();
            var owner = host?.TemplatedParent as Control;
            gridView = owner?.Template?.FindName("PART_ViewGrid", owner) as Control;
            hostPanel = host?.Parent as FrameworkElement;
            if (gridView == null) return;
            gridView.ApplyTemplate();
            nativeGridList = gridView.Template.FindName("PART_ListGames", gridView) as ListBox;
            nativeDetails = gridView.Template.FindName("PART_ControlGameView", gridView) as FrameworkElement;
            if (hostPanel != null) hostPanel.SizeChanged += LayoutChanged;
            gridView.SizeChanged += LayoutChanged;
            gridView.IsVisibleChanged += LayoutVisibleChanged;
            if (nativeDetails != null)
                DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(UIElement)).AddValueChanged(nativeDetails, NativeDetailsVisibilityChanged);
            if (nativeGridList != null) nativeGridList.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(GridMouseDown), true);
        }
        private void UnbindHost()
        {
            if (hostPanel != null) hostPanel.SizeChanged -= LayoutChanged;
            if (gridView != null) { gridView.SizeChanged -= LayoutChanged; gridView.IsVisibleChanged -= LayoutVisibleChanged; }
            if (nativeDetails != null)
                DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(UIElement)).RemoveValueChanged(nativeDetails, NativeDetailsVisibilityChanged);
            if (nativeGridList != null) nativeGridList.RemoveHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(GridMouseDown));
            gridView = null; hostPanel = null; nativeDetails = null; nativeGridList = null;
        }
        private void LayoutChanged(object sender, SizeChangedEventArgs e) { RecomputeLayout(); }
        private void LayoutVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) { RecomputeLayout(); }
        private void RecomputeLayout()
        {
            if (!active) return;
            var isGrid = api.MainView.ActiveDesktopView == DesktopView.Grid;
            plugin.State.InGrid = isGrid;
            plugin.State.NavigatorVisible = isGrid ? !prefs.Collapsed : !prefs.CollapsedOther;
            if (!isGrid) { plugin.State.IsDetailPage = false; return; }
            if (nativeDetails != null && nativeDetails.ActualWidth > 300 && !plugin.State.IsDetailPage)
                rememberedDetailWidth = nativeDetails.ActualWidth;
            var available = gridView?.ActualWidth ?? 0;
            plugin.State.IsCompact = Logic.Compact(prefs.LayoutMode, plugin.State.IsCompact, available, rememberedDetailWidth);
        }
        internal void SetCollapsed(bool collapsed)
        {
            if (api.MainView.ActiveDesktopView == DesktopView.Grid) prefs.Collapsed = collapsed;
            else prefs.CollapsedOther = collapsed;
            plugin.State.NavigatorVisible = !collapsed;
            ApplySize(); RecomputeLayout(); Save();
            if (!collapsed) Refresh();
        }
        private void StartSelected() { if (list.SelectedItem is GameRow row) Safe(() => api.StartGame(row.Id)); }
        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            if (active) return; active = true;
            plugin.SelectionChanged += SelectionChanged;
            api.Database.Games.ItemUpdated += GamesUpdated;
            api.Database.Games.ItemCollectionChanged += GamesChanged;
            api.Database.Series.ItemUpdated += MetadataUpdated;
            api.Database.Categories.ItemUpdated += MetadataUpdated;
            api.Database.Platforms.ItemUpdated += MetadataUpdated;
            api.Database.Series.ItemCollectionChanged += MetadataChanged;
            api.Database.Categories.ItemCollectionChanged += MetadataChanged;
            api.Database.Platforms.ItemCollectionChanged += MetadataChanged;
            BindHost(); RecomputeLayout();
            Interlocked.Exchange(ref dirty, 1); Refresh(); timer.Start();
        }
        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            if (!active) return; active = false; timer.Stop(); searchTimer.Stop(); UnbindHost();
            plugin.SelectionChanged -= SelectionChanged;
            api.Database.Games.ItemUpdated -= GamesUpdated;
            api.Database.Games.ItemCollectionChanged -= GamesChanged;
            api.Database.Series.ItemUpdated -= MetadataUpdated;
            api.Database.Categories.ItemUpdated -= MetadataUpdated;
            api.Database.Platforms.ItemUpdated -= MetadataUpdated;
            api.Database.Series.ItemCollectionChanged -= MetadataChanged;
            api.Database.Categories.ItemCollectionChanged -= MetadataChanged;
            api.Database.Platforms.ItemCollectionChanged -= MetadataChanged;
            Save();
        }
        private void MetadataUpdated<T>(object sender, ItemUpdatedEventArgs<T> args) where T : DatabaseObject { Interlocked.Exchange(ref fullDirty, 1); Interlocked.Exchange(ref dirty, 1); QueueRefresh(); }
        private void MetadataChanged<T>(object sender, ItemCollectionChangedEventArgs<T> args) where T : DatabaseObject { Interlocked.Exchange(ref fullDirty, 1); Interlocked.Exchange(ref dirty, 1); QueueRefresh(); }
        private void GamesUpdated(object sender, ItemUpdatedEventArgs<Game> args)
        {
            lock (updateGate) foreach (var entry in args.UpdatedItems) if (entry.NewData != null) pendingGameIds.Add(entry.NewData.Id);
            Interlocked.Exchange(ref dirty, 1); QueueRefresh();
        }
        private void GamesChanged(object sender, ItemCollectionChangedEventArgs<Game> args) { Interlocked.Exchange(ref fullDirty, 1); Interlocked.Exchange(ref dirty, 1); QueueRefresh(); }
        private void QueueRefresh()
        {
            if (Interlocked.Exchange(ref refreshQueued, 1) != 0) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                Interlocked.Exchange(ref refreshQueued, 0); if (active) Refresh();
            }));
        }
        private void SelectionChanged()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(SelectionChanged)); return; }
            if (active) Safe(() => SyncSelection(true));
        }
        public override void GameContextChanged(Game oldContext, Game newContext) { SelectionChanged(); }
        private void Refresh()
        {
            if (!active || !plugin.State.NavigatorVisible) return;
            Safe(() => {
                var games = api.MainView.FilteredGames ?? new List<Game>();
                // Filters have no SDK change event; compare membership at background priority.
                var ids = games.Select(g => g.Id).OrderBy(id => id).ToArray();
                var changed = Interlocked.Exchange(ref dirty, 0) != 0;
                bool sameMembership = ids.SequenceEqual(lastIds);
                if (!changed && sameMembership) { SyncSelection(false); return; }
                if (changed && sameMembership && Interlocked.CompareExchange(ref fullDirty, 0, 0) == 0 && TryPatch(games)) {
                    SyncSelection(false); return;
                }
                if (!sameMembership) hasGridOffset = false;
                Interlocked.Exchange(ref fullDirty, 0);
                lock (updateGate) pendingGameIds.Clear();
                lastIds = ids;
                var rebuilt = new List<GameRow>();
                foreach (var game in games.OrderBy(g => string.IsNullOrEmpty(g.SortingName) ? g.Name : g.SortingName, StringComparer.CurrentCultureIgnoreCase)) {
                    IEnumerable<string> names = null;
                    switch (prefs.Grouping) {
                        case 1: names = game.Series?.Select(v => v.Name); break;
                        case 2: names = game.Categories?.Select(v => v.Name); break;
                        case 3: names = game.Platforms?.Select(v => v.Name); break;
                    }
                    string iconPath = null;
                    if (!string.IsNullOrWhiteSpace(game.Icon)) iconPath = api.Database.GetFullFilePath(game.Icon);
                    foreach (var name in prefs.Grouping == 0 ? new[] { "" } : Logic.Groups(names))
                        rebuilt.Add(new GameRow { Id = game.Id, Name = game.Name ?? "Sem título", SortName = game.SortingName ?? game.Name ?? "", Key = Logic.SearchKey(game.Name), Group = name, IconPath = iconPath });
                }
                if (prefs.Grouping != 0) rebuilt = rebuilt.OrderBy(r => r.Group, StringComparer.CurrentCultureIgnoreCase).ToList();
                rows = rebuilt; syncing = true;
                try {
                    view = new ListCollectionView(rows);
                    if (prefs.Grouping != 0) view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
                    list.ItemsSource = view; ApplySearch();
                } finally { syncing = false; }
                SyncSelection(false); status.Text = ""; reportedError = false;
            });
        }
        private bool TryPatch(List<Game> games)
        {
            if (prefs.Grouping != 0 || view == null) return false;
            Guid[] updates;
            lock (updateGate) updates = pendingGameIds.ToArray();
            if (updates.Length == 0 || updates.Length > 16) return false;
            var candidates = games.Where(g => updates.Contains(g.Id)).ToDictionary(g => g.Id);
            var existing = rows.Where(r => candidates.ContainsKey(r.Id)).ToDictionary(r => r.Id);
            if (candidates.Count != updates.Length || existing.Count != updates.Length) return false;
            if (updates.Any(id => existing[id].Name != (candidates[id].Name ?? "Sem título") ||
                existing[id].SortName != (candidates[id].SortingName ?? candidates[id].Name ?? ""))) return false;
            foreach (var id in updates) {
                var game = candidates[id];
                existing[id].IconPath = string.IsNullOrWhiteSpace(game.Icon) ? null : api.Database.GetFullFilePath(game.Icon);
            }
            lock (updateGate) foreach (var id in updates) pendingGameIds.Remove(id);
            return true;
        }
        private void ApplySearch()
        {
            if (view == null) return;
            var key = Logic.SearchKey(search.Text);
            syncing = true;
            try { view.Filter = obj => ((GameRow)obj).Key.Contains(key); view.Refresh(); }
            finally { syncing = false; }
            count.Text = view.Cast<GameRow>().Select(r => r.Id).Distinct().Count() + " jogos";
            status.Text = view.IsEmpty ? "Nenhum jogo nesta busca. Confira também os filtros globais." : "";
            SyncSelection(false);
        }
        private void SyncSelection(bool scroll)
        {
            if (syncing || view == null) return;
            var selected = api.MainView.SelectedGames?.Select(g => g.Id).ToHashSetCompat() ?? new HashSet<Guid>();
            var current = list.SelectedItem as GameRow;
            if (current != null && selected.Contains(current.Id) && view.Contains(current)) return;
            var row = view.Cast<GameRow>().FirstOrDefault(r => selected.Contains(r.Id));
            syncing = true;
            try { list.SelectedItem = row; if (scroll && row != null) list.ScrollIntoView(row); }
            finally { syncing = false; }
        }
        private void ApplySize()
        {
            Width = prefs.Width;
        }
        private void Save() { plugin.Save(); }
        private void Safe(Action action)
        {
            try { action(); }
            catch (Exception e) {
                Interlocked.Exchange(ref dirty, 1);
                status.Text = "Não foi possível atualizar. Consulte playnite.log (Helium Hybrid).";
                if (!reportedError) LogManager.GetLogger().Error(e, "Helium Hybrid: erro no navegador.");
                reportedError = true;
            }
        }
    }
    internal static class EnumerableCompatibility
    {
        internal static HashSet<T> ToHashSetCompat<T>(this IEnumerable<T> items) { return new HashSet<T>(items); }
    }
}
