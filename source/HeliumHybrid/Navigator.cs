using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Threading;
using System.Xml.Serialization;
using System.Runtime.InteropServices;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;
namespace HeliumHybrid
{
    public sealed class GameRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
        public string Key { get; set; }
        public string IconPath { get; set; }
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
        private readonly FrameworkElement tools;
        private readonly Button backButton;
        private Button reopenButton;
        private readonly DispatcherTimer timer, searchTimer;
        private readonly DispatcherTimer coverClickTimer, sidebarClickTimer;
        private Guid pendingSidebarId;
        private bool panelRequested;
        private DateTime panelRequestedAt;
        private ListBox nativeGridList;
        private ListBoxItem coverDownItem;
        private Point coverDownPosition;
        private Guid pendingCoverId;
        private readonly Preferences prefs;
        private bool active, syncing, reportedError;
        private bool compact, navigatedDetails;
        private FrameworkElement detailsBorder;
        private BindingBase detailsVisibilityBinding;
        private FrameworkElement nativeDetailsList;
        private double gridOffset;
        private Guid[] gridIds;
        private string gridSort, gridSortDirection, gridGrouping;
        private Window window;
        private DesktopView lastDesktopView;
        private int dirty = 1;
        private Guid[] lastIds = new Guid[0];
        private List<GameRow> rows = new List<GameRow>();
        private ListCollectionView view;
        public Navigator(IPlayniteAPI api, HybridPlugin plugin)
        {
            this.api = api; this.plugin = plugin;
            prefs = plugin.Settings;
            prefs.Width = Logic.ClampWidth(prefs.Width);
            using (var stream = typeof(Navigator).Assembly.GetManifestResourceStream("HeliumHybrid.Navigator.xaml"))
                root = (Grid)XamlReader.Load(stream);
            Content = root;
            list = (ListBox)root.FindName("Games"); search = (TextBox)root.FindName("Search");
            grouping = (ComboBox)root.FindName("Grouping"); count = (TextBlock)root.FindName("Count");
            status = (TextBlock)root.FindName("Status"); toggle = (Button)root.FindName("CollapseButton");
            grip = (Thumb)root.FindName("ResizeGrip"); tools = (FrameworkElement)root.FindName("Tools");
            backButton = (Button)root.FindName("BackButton");
            grouping.SelectedIndex = Math.Max(0, Math.Min(3, prefs.Grouping));
            timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(1500) };
            timer.Tick += (s, e) => { RecomputeLayout(); Refresh(); };
            searchTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(180) };
            searchTimer.Tick += (s, e) => { searchTimer.Stop(); ApplySearch(); };
            coverClickTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher);
            coverClickTimer.Tick += (s, e) => {
                coverClickTimer.Stop();
                if (active && api.MainView.ActiveDesktopView == DesktopView.Grid &&
                    api.MainView.SelectedGames?.Any(game => game.Id == pendingCoverId) == true)
                    Safe(() => Navigate(pendingCoverId, prefs.GridCoverClickAction));
            };
            sidebarClickTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher);
            sidebarClickTimer.Tick += (s, e) => {
                sidebarClickTimer.Stop();
                if (active && api.MainView.SelectedGames?.Any(game => game.Id == pendingSidebarId) == true)
                    Safe(() => Navigate(pendingSidebarId, prefs.SidebarClickAction));
            };
            search.TextChanged += (s, e) => { searchTimer.Stop(); searchTimer.Start(); };
            grouping.SelectionChanged += (s, e) => { prefs.Grouping = grouping.SelectedIndex; Interlocked.Exchange(ref dirty, 1); Refresh(); Save(); };
            backButton.Click += (sender, args) => Safe(ReturnToGrid);
            toggle.Click += (sender, args) => {
                prefs.Collapsed = !prefs.Collapsed;
                ApplySize(); RecomputeLayout(); Save(); if (!IsCollapsed()) Refresh();
            };
            grip.DragDelta += (s, e) => { prefs.Width = Logic.ClampWidth(prefs.Width + e.HorizontalChange); ApplySize(); RecomputeLayout(); };
            grip.DragCompleted += (s, e) => Save();
            list.SelectionChanged += (s, e) => {
                if (!syncing && list.SelectedItem is GameRow row) Safe(() => SelectGame(row.Id));
            };
            list.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) => {
                if (e.ClickCount > 1) sidebarClickTimer.Stop();
            }), true);
            list.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) => {
                if (e.ClickCount != 1 || Keyboard.Modifiers != ModifierKeys.None) return;
                var item = RowUnderPointer(e.OriginalSource as DependencyObject);
                if (!(item?.DataContext is GameRow row)) return;
                pendingSidebarId = row.Id;
                sidebarClickTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(200, (int)GetDoubleClickTime()) + 60);
                sidebarClickTimer.Stop(); sidebarClickTimer.Start();
            }), true);
            list.MouseDoubleClick += (s, e) => {
                sidebarClickTimer.Stop();
                if (RowUnderPointer(e.OriginalSource as DependencyObject) != null) StartSelected();
            };
            list.PreviewKeyDown += (s, e) => { if (e.Key == Key.Enter && list.SelectedItem != null) { StartSelected(); e.Handled = true; } };
            // These commands use public APIs. The native grid keeps its complete original menu.
            list.ContextMenuOpening += ContextOpening;
            var menu = new ContextMenu(); var play = new MenuItem { Header = "Play" }; play.Click += (s, e) => StartSelected();
            
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
            list.SelectedItem = container.DataContext;
        }
        private void SelectGame(Guid id) => api.MainView.SelectGame(id);
        private void Navigate(Guid id, int action)
        {
            if (action == 0) OpenNativeDetails(id);
            else OpenGridPanel(id);
        }
        private void OpenNativeDetails(Guid id)
        {
            if (!active || api.MainView.ActiveDesktopView != DesktopView.Grid ||
                api.MainView.SelectedGames?.Any(game => game.Id == id) != true) return;
            var switchButton = FindNamed(window, "HeliumCharlo_SwitchDetails") as Button;
            if (switchButton?.Command == null || !switchButton.Command.CanExecute(null)) return;
            CaptureGridScroll();
            navigatedDetails = true;
            panelRequested = false;
            switchButton.Command.Execute(null);
            RecomputeLayout();
        }
        private void OpenGridPanel(Guid id, int remainingSwitchChecks = 3)
        {
            if (!active || api.MainView.SelectedGames?.Any(game => game.Id == id) != true) return;
            if (api.MainView.ActiveDesktopView == DesktopView.Details) {
                if (navigatedDetails) ReturnToGrid();
                else {
                    var button = FindNamed(window, "HeliumCharlo_SwitchGrid") as Button;
                    if (button?.Command == null || !button.Command.CanExecute(null)) return;
                    button.Command.Execute(null);
                }
                if (remainingSwitchChecks > 0)
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                        new Action(() => Safe(() => OpenGridPanel(id, remainingSwitchChecks - 1))));
                return;
            }
            if (api.MainView.ActiveDesktopView != DesktopView.Grid) return;
            var host = FindNamed(window, "HeliumCharlo_Navigator");
            var command = host?.Tag as ICommand;
            var selected = host?.DataContext;
            if (command == null || selected == null || !command.CanExecute(selected)) return;
            // The theme binds this parameter to MainViewModel.SelectedGame, as the grid info button does.
            panelRequested = true;
            panelRequestedAt = DateTime.UtcNow;
            command.Execute(selected);
            RecomputeLayout();
        }
        private void ReturnToGrid()
        {
            if (!navigatedDetails || api.MainView.ActiveDesktopView != DesktopView.Details) return;
            var switchButton = FindNamed(window, "HeliumCharlo_SwitchGrid") as Button;
            if (switchButton?.Command == null || !switchButton.Command.CanExecute(null)) return;
            switchButton.Command.Execute(null);
            navigatedDetails = false;
            RecomputeLayout();
            if (prefs.PreserveGridScroll) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RestoreGridScroll));
        }
        private void CaptureGridScroll()
        {
            gridIds = api.MainView.FilteredGames?.Select(game => game.Id).OrderBy(id => id).ToArray();
            gridSort = api.MainView.SortOrder.ToString();
            gridSortDirection = api.MainView.SortOrderDirection.ToString();
            gridGrouping = api.MainView.Grouping.ToString();
            var grid = FindNamed(window, "PART_ViewGrid") as Control;
            var games = grid?.Template?.FindName("PART_ListGames", grid) as DependencyObject;
            gridOffset = FindScroll(games)?.VerticalOffset ?? 0;
        }
        private void RestoreGridScroll()
        {
            if (!active || api.MainView.ActiveDesktopView != DesktopView.Grid || gridIds == null) return;
            var currentIds = api.MainView.FilteredGames?.Select(game => game.Id).OrderBy(id => id).ToArray();
            if (currentIds == null || !gridIds.SequenceEqual(currentIds) ||
                gridSort != api.MainView.SortOrder.ToString() ||
                gridSortDirection != api.MainView.SortOrderDirection.ToString() ||
                gridGrouping != api.MainView.Grouping.ToString()) { gridIds = null; return; }
            var grid = FindNamed(window, "PART_ViewGrid") as Control;
            var games = grid?.Template?.FindName("PART_ListGames", grid) as DependencyObject;
            FindScroll(games)?.ScrollToVerticalOffset(gridOffset);
            gridIds = null;
        }
        private static ScrollViewer FindScroll(DependencyObject node)
        {
            if (node is ScrollViewer scroll) return scroll;
            if (node == null) return null;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) {
                var found = FindScroll(VisualTreeHelper.GetChild(node, i));
                if (found != null) return found;
            }
            return null;
        }
        private void StartSelected() { if (list.SelectedItem is GameRow row) Safe(() => api.StartGame(row.Id)); }
        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            if (active) return; active = true;
            plugin.SelectionChanged += SelectionChanged;
            plugin.SettingsChanged += SettingsUpdated;
            api.Database.Games.ItemUpdated += GamesUpdated;
            api.Database.Games.ItemCollectionChanged += GamesChanged;
            api.Database.Series.ItemUpdated += MetadataUpdated;
            api.Database.Categories.ItemUpdated += MetadataUpdated;
            api.Database.Platforms.ItemUpdated += MetadataUpdated;
            api.Database.Series.ItemCollectionChanged += MetadataChanged;
            api.Database.Categories.ItemCollectionChanged += MetadataChanged;
            api.Database.Platforms.ItemCollectionChanged += MetadataChanged;
            lastDesktopView = api.MainView.ActiveDesktopView;
            window = Window.GetWindow(this);
            if (window != null) window.SizeChanged += WindowSizeChanged;
            RecomputeLayout();
            Interlocked.Exchange(ref dirty, 1); Refresh(); timer.Start();
        }
        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            if (!active) return; active = false; timer.Stop(); searchTimer.Stop(); coverClickTimer.Stop(); sidebarClickTimer.Stop(); panelRequested = false; UnbindGridClicks();
            plugin.SelectionChanged -= SelectionChanged;
            plugin.SettingsChanged -= SettingsUpdated;
            api.Database.Games.ItemUpdated -= GamesUpdated;
            api.Database.Games.ItemCollectionChanged -= GamesChanged;
            api.Database.Series.ItemUpdated -= MetadataUpdated;
            api.Database.Categories.ItemUpdated -= MetadataUpdated;
            api.Database.Platforms.ItemUpdated -= MetadataUpdated;
            api.Database.Series.ItemCollectionChanged -= MetadataChanged;
            api.Database.Categories.ItemCollectionChanged -= MetadataChanged;
            api.Database.Platforms.ItemCollectionChanged -= MetadataChanged;
            if (window != null) window.SizeChanged -= WindowSizeChanged;
            if (reopenButton != null) { reopenButton.Click -= ReopenClicked; reopenButton.Visibility = Visibility.Collapsed; reopenButton = null; }
            window = null;
            RestoreDetails();
            ShowNativeList();
            Save();
        }
        private void SettingsUpdated()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(SettingsUpdated)); return; }
            if (!active) return;
            prefs.Width = Logic.ClampWidth(prefs.Width);
            ApplySize(); RecomputeLayout();
        }
        private void MetadataUpdated<T>(object sender, ItemUpdatedEventArgs<T> args) where T : DatabaseObject { Interlocked.Exchange(ref dirty, 1); }
        private void MetadataChanged<T>(object sender, ItemCollectionChangedEventArgs<T> args) where T : DatabaseObject { Interlocked.Exchange(ref dirty, 1); }
        private void GamesUpdated(object sender, ItemUpdatedEventArgs<Game> args) { Interlocked.Exchange(ref dirty, 1); }
        private void GamesChanged(object sender, ItemCollectionChangedEventArgs<Game> args) { Interlocked.Exchange(ref dirty, 1); }
        private void SelectionChanged()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(SelectionChanged)); return; }
            if (active) Safe(() => SyncSelection(true));
        }
        public override void GameContextChanged(Game oldContext, Game newContext) { SelectionChanged(); }
        private void Refresh()
        {
            if (!active || IsCollapsed()) return;
            Safe(() => {
                var games = api.MainView.FilteredGames ?? new List<Game>();
                // Filters have no SDK change event; compare membership at background priority.
                var ids = games.Select(g => g.Id).OrderBy(id => id).ToArray();
                var changed = Interlocked.Exchange(ref dirty, 0) != 0;
                if (!changed && ids.SequenceEqual(lastIds)) { SyncSelection(false); return; }
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
                    rebuilt.Add(new GameRow { Id = game.Id, Name = game.Name ?? "Untitled", Key = Logic.SearchKey(game.Name), Group = name, IconPath = iconPath });
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
        private void ApplySearch()
        {
            if (view == null) return;
            var key = Logic.SearchKey(search.Text);
            syncing = true;
            try { view.Filter = obj => ((GameRow)obj).Key.Contains(key); view.Refresh(); }
            finally { syncing = false; }
            count.Text = view.Cast<GameRow>().Select(r => r.Id).Distinct().Count() + " games";
            status.Text = view.IsEmpty ? "No games found. Check the global filters too." : "";
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
            if (active && (api.MainView.ActiveDesktopView == DesktopView.List ||
                (api.MainView.ActiveDesktopView == DesktopView.Details && !navigatedDetails))) {
                Visibility = Visibility.Collapsed;
                Width = 0;
                if (reopenButton != null) reopenButton.Visibility = Visibility.Collapsed;
                return;
            }
            Visibility = Visibility.Visible;
            bool collapsed = IsCollapsed();
            Width = collapsed ? 0 : prefs.Width;
            list.Visibility = tools.Visibility = status.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            grip.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            toggle.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            if (reopenButton != null) reopenButton.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
            backButton.Visibility = navigatedDetails && api.MainView.ActiveDesktopView == DesktopView.Details && !collapsed ? Visibility.Visible : Visibility.Collapsed;
            toggle.Content = "<<";
        }
        private void ReopenClicked(object sender, RoutedEventArgs args)
        {
            prefs.Collapsed = false;
            ApplySize(); RecomputeLayout(); Save(); Refresh();
        }
        private void BindReopenButton(DependencyObject host)
        {
            var found = FindNamed(host, "HeliumCharlo_Reopen") as Button;
            if (ReferenceEquals(reopenButton, found)) return;
            if (reopenButton != null) { reopenButton.Click -= ReopenClicked; reopenButton.Visibility = Visibility.Collapsed; }
            reopenButton = found;
            if (reopenButton != null) reopenButton.Click += ReopenClicked;
        }
        private bool IsCollapsed() => prefs.Collapsed;
        private void WindowSizeChanged(object sender, SizeChangedEventArgs args) { RecomputeLayout(); }
        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();
        private void BindGridClicks(Control gridView)
        {
            var found = gridView?.Template?.FindName("PART_ListGames", gridView) as ListBox;
            if (ReferenceEquals(nativeGridList, found)) return;
            UnbindGridClicks();
            nativeGridList = found;
            if (nativeGridList == null) return;
            nativeGridList.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(GridCoverDown), true);
            nativeGridList.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(GridCoverUp), true);
        }
        private void UnbindGridClicks()
        {
            coverClickTimer.Stop();
            coverDownItem = null;
            if (nativeGridList == null) return;
            nativeGridList.RemoveHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(GridCoverDown));
            nativeGridList.RemoveHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(GridCoverUp));
            nativeGridList = null;
        }
        private static bool IsInteractiveChild(DependencyObject source, DependencyObject item)
        {
            for (var current = source; current != null && current != item; current = VisualTreeHelper.GetParent(current))
                if (current is ButtonBase || current is TextBox) return true;
            return false;
        }
        private void GridCoverDown(object sender, MouseButtonEventArgs args)
        {
            coverClickTimer.Stop();
            coverDownItem = null;
            if (args.ClickCount != 1 ||
                api.MainView.ActiveDesktopView != DesktopView.Grid || Keyboard.Modifiers != ModifierKeys.None) return;
            var source = args.OriginalSource as DependencyObject;
            var item = source == null ? null : ItemsControl.ContainerFromElement(nativeGridList, source) as ListBoxItem;
            if (item == null || IsInteractiveChild(source, item)) return;
            coverDownItem = item;
            coverDownPosition = args.GetPosition(nativeGridList);
        }
        private void GridCoverUp(object sender, MouseButtonEventArgs args)
        {
            var item = coverDownItem;
            coverDownItem = null;
            if (item == null || args.ClickCount != 1 ||
                api.MainView.ActiveDesktopView != DesktopView.Grid) return;
            var source = args.OriginalSource as DependencyObject;
            if (source == null || ItemsControl.ContainerFromElement(nativeGridList, source) != item) return;
            var position = args.GetPosition(nativeGridList);
            if (Math.Abs(position.X - coverDownPosition.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - coverDownPosition.Y) > SystemParameters.MinimumVerticalDragDistance) return;
            var selected = api.MainView.SelectedGames;
            if (selected == null || selected.Count() != 1) return;
            pendingCoverId = selected.First().Id;
            coverClickTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(200, (int)GetDoubleClickTime()) + 60);
            coverClickTimer.Start();
        }
        private static FrameworkElement FindNamed(DependencyObject node, string name)
        {
            if (node == null) return null;
            if (node is FrameworkElement element && element.Name == name) return element;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) {
                var found = FindNamed(VisualTreeHelper.GetChild(node, i), name);
                if (found != null) return found;
            }
            return null;
        }
        private void RestoreDetails()
        {
            if (detailsBorder != null) {
                detailsBorder.ClearValue(VisibilityProperty);
                if (detailsVisibilityBinding != null) BindingOperations.SetBinding(detailsBorder, VisibilityProperty, detailsVisibilityBinding);
            }
            detailsBorder = null;
            detailsVisibilityBinding = null;
        }
        private void ShowNativeList()
        {
            if (nativeDetailsList != null) nativeDetailsList.ClearValue(VisibilityProperty);
            nativeDetailsList = null;
        }
        private void RecomputeLayout()
        {
            if (!active) return;
            if (lastDesktopView != api.MainView.ActiveDesktopView) {
                if (lastDesktopView == DesktopView.Details)
                    navigatedDetails = false;
                lastDesktopView = api.MainView.ActiveDesktopView;
            }
            ApplySize();
            var host = window ?? Window.GetWindow(this);
            if (host == null) return;
            BindReopenButton(host);
            ApplySize();
            if (navigatedDetails && api.MainView.ActiveDesktopView == DesktopView.Details) {
                nativeDetailsList = FindNamed(host, "HeliumCharlo_NativeDetailsList");
                if (nativeDetailsList != null) nativeDetailsList.Visibility = Visibility.Collapsed;
            } else ShowNativeList();
            var gridView = FindNamed(host, "PART_ViewGrid") as Control;
            BindGridClicks(gridView);
            // Do not alter the converter's XAML or the overview itself. Its host border owns the space.
            if (gridView == null || api.MainView.ActiveDesktopView != DesktopView.Grid) { RestoreDetails(); return; }
            var overview = gridView.Template?.FindName("PART_ControlGameView", gridView) as FrameworkElement;
            var border = overview == null ? null : VisualTreeHelper.GetParent(overview) as FrameworkElement;
            if (border == null) return;
            if (!ReferenceEquals(detailsBorder, border)) { RestoreDetails(); detailsBorder = border; detailsVisibilityBinding = BindingOperations.GetBindingBase(border, VisibilityProperty); }
            compact = Logic.Compact(prefs.LayoutMode, compact, gridView.ActualWidth);
            if (panelRequested && overview.Visibility == Visibility.Collapsed &&
                DateTime.UtcNow - panelRequestedAt > TimeSpan.FromSeconds(2)) panelRequested = false;
            if (compact && !panelRequested) border.Visibility = Visibility.Collapsed;
            else {
                border.ClearValue(VisibilityProperty);
                if (detailsVisibilityBinding != null) BindingOperations.SetBinding(border, VisibilityProperty, detailsVisibilityBinding);
            }
        }
        private void Save() => plugin.SaveSettings();
        private void Safe(Action action)
        {
            try { action(); }
            catch (Exception e) {
                Interlocked.Exchange(ref dirty, 1);
                status.Text = "Could not refresh the game list. Check playnite.log (Helium Charlo).";
                if (!reportedError) LogManager.GetLogger().Error(e, "Helium Charlo: navigator error.");
                reportedError = true;
            }
        }
    }
    internal static class EnumerableCompatibility
    {
        internal static HashSet<T> ToHashSetCompat<T>(this IEnumerable<T> items) { return new HashSet<T>(items); }
    }
}
