using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using System.Runtime.InteropServices;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;
namespace HeliumHybrid
{
    public sealed class Navigator : PluginUserControl
    {
        private readonly IPlayniteAPI api;
        private readonly LibraryStateAdapter library;
        private readonly NavigatorDiagnostics diagnostics;
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
        private ListBoxItem sidebarDownItem;
        private Point sidebarDownPosition;
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
        private Guid[] lastOrderIds = new Guid[0];
        private NavigatorViewSnapshot lastViewState;
        private List<GameRow> rows = new List<GameRow>();
        private ListCollectionView view;
        public Navigator(IPlayniteAPI api, HybridPlugin plugin)
        {
            this.api = api; this.plugin = plugin; library = new LibraryStateAdapter(api);
            diagnostics = new NavigatorDiagnostics(NavigatorDiagnostics.Requested(),
                message => LogManager.GetLogger().Info(message));
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
                if (active && library.ActiveDesktopView == DesktopView.Grid &&
                    library.IsSelected(pendingCoverId))
                    Safe(() => Navigate(pendingCoverId, prefs.GridCoverClickAction));
            };
            sidebarClickTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher);
            sidebarClickTimer.Tick += (s, e) => {
                sidebarClickTimer.Stop();
                if (active && library.IsSelected(pendingSidebarId))
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
            list.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(SidebarDown), true);
            list.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(SidebarUp), true);
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
            // Keyboard invocation belongs to the selected row, independent of cursor position.
            if (e.CursorLeft < 0 || e.CursorTop < 0) {
                if (!(list.SelectedItem is GameRow)) e.Handled = true;
                return;
            }
            var container = RowUnderPointer(Mouse.DirectlyOver as DependencyObject);
            if (container == null) { e.Handled = true; return; }
            list.SelectedItem = container.DataContext;
        }
        private void SidebarDown(object sender, MouseButtonEventArgs args)
        {
            sidebarClickTimer.Stop();
            sidebarDownItem = null;
            if (args.ClickCount != 1 || Keyboard.Modifiers != ModifierKeys.None) return;
            var source = args.OriginalSource as DependencyObject;
            var item = RowUnderPointer(source);
            if (item == null || IsInteractiveChild(source, item)) return;
            sidebarDownItem = item;
            sidebarDownPosition = args.GetPosition(list);
        }
        private void SidebarUp(object sender, MouseButtonEventArgs args)
        {
            var item = sidebarDownItem;
            sidebarDownItem = null;
            if (item == null || args.ClickCount != 1 || Keyboard.Modifiers != ModifierKeys.None) return;
            var source = args.OriginalSource as DependencyObject;
            if (RowUnderPointer(source) != item || IsInteractiveChild(source, item)) return;
            var position = args.GetPosition(list);
            if (Math.Abs(position.X - sidebarDownPosition.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - sidebarDownPosition.Y) > SystemParameters.MinimumVerticalDragDistance) return;
            if (!(item.DataContext is GameRow row) ||
                !library.IsSelected(row.Id)) return;
            pendingSidebarId = row.Id;
            sidebarClickTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(200, (int)GetDoubleClickTime()) + 60);
            sidebarClickTimer.Start();
        }
        private void SelectGame(Guid id) => api.MainView.SelectGame(id);
        private void Navigate(Guid id, int action)
        {
            // Selection is already synchronized. Click actions only open views from Grid.
            if (library.ActiveDesktopView != DesktopView.Grid) return;
            if (action == 0) OpenNativeDetails(id);
            else OpenGridPanel(id);
        }
        private void OpenNativeDetails(Guid id)
        {
            if (!active || library.ActiveDesktopView != DesktopView.Grid ||
                !library.IsSelected(id)) return;
            var switchButton = FindNamed(window, "HeliumCharlo_SwitchDetails") as Button;
            if (switchButton?.Command == null || !switchButton.Command.CanExecute(null)) return;
            CaptureGridScroll();
            navigatedDetails = true;
            panelRequested = false;
            switchButton.Command.Execute(null);
            RecomputeLayout();
        }
        private void OpenGridPanel(Guid id)
        {
            if (!active || library.ActiveDesktopView != DesktopView.Grid ||
                !library.IsSelected(id)) return;
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
            if (!navigatedDetails || library.ActiveDesktopView != DesktopView.Details) return;
            var switchButton = FindNamed(window, "HeliumCharlo_SwitchGrid") as Button;
            if (switchButton?.Command == null || !switchButton.Command.CanExecute(null)) return;
            switchButton.Command.Execute(null);
            navigatedDetails = false;
            RecomputeLayout();
            if (prefs.PreserveGridScroll) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RestoreGridScroll));
        }
        private void CaptureGridScroll()
        {
            gridIds = library.SortedFilteredIds();
            gridSort = library.SortOrder;
            gridSortDirection = library.SortDirection;
            gridGrouping = library.Grouping;
            var grid = FindNamed(window, "PART_ViewGrid") as Control;
            var games = grid?.Template?.FindName("PART_ListGames", grid) as DependencyObject;
            gridOffset = FindScroll(games)?.VerticalOffset ?? 0;
        }
        private void RestoreGridScroll()
        {
            if (!active || library.ActiveDesktopView != DesktopView.Grid || gridIds == null) return;
            var currentIds = library.SortedFilteredIds();
            if (currentIds == null || !gridIds.SequenceEqual(currentIds) ||
                gridSort != library.SortOrder ||
                gridSortDirection != library.SortDirection ||
                gridGrouping != library.Grouping) { gridIds = null; return; }
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
            lastDesktopView = library.ActiveDesktopView;
            window = Window.GetWindow(this);
            if (window != null) window.SizeChanged += WindowSizeChanged;
            RecomputeLayout();
            Interlocked.Exchange(ref dirty, 1); Refresh(); timer.Start();
        }
        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            if (!active) return; active = false; timer.Stop(); searchTimer.Stop(); coverClickTimer.Stop(); sidebarClickTimer.Stop(); sidebarDownItem = null; panelRequested = false; UnbindGridClicks();
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
                var refreshStart = diagnostics.Enabled ? Stopwatch.GetTimestamp() : 0;
                var games = library.FilteredGames;
                // Filter membership, native sequence, and view state are separate change signals.
                var orderIds = games.Select(g => g.Id).ToArray();
                var ids = NavigatorRefreshDecision.SortedIds(orderIds);
                var changed = Interlocked.Exchange(ref dirty, 0) != 0;
                var membershipChanged = NavigatorRefreshDecision.MembershipChanged(ids, lastIds);
                var orderChanged = NavigatorRefreshDecision.OrderChanged(orderIds, lastOrderIds);
                var viewState = library.ViewState(prefs.Grouping);
                var viewStateChanged = NavigatorRefreshDecision.ViewStateChanged(viewState, lastViewState);
                var rebuild = NavigatorRefreshDecision.ShouldRebuild(changed, membershipChanged,
                    orderChanged, viewStateChanged);
                var diagnosticState = diagnostics.Enabled
                    ? diagnostics.Capture(() => library.DiagnosticViewState(prefs.Grouping)) : null;
                if (!rebuild) {
                    SyncSelection(false);
                    diagnostics.Record(diagnosticState, changed, membershipChanged, orderChanged,
                        viewStateChanged, false,
                        diagnostics.Enabled ? NavigatorDiagnostics.MillisecondsSince(refreshStart) : 0, 0, 0);
                    return;
                }
                lastIds = ids;
                lastOrderIds = orderIds;
                lastViewState = viewState;
                var projectionStart = diagnostics.Enabled ? Stopwatch.GetTimestamp() : 0;
                var projected = NavigatorProjection.Build(library.Sources(games, prefs.Grouping), prefs.Grouping, library.ResolveIcon);
                rows = projected.Select(row => new GameRow {
                    Id = row.Id, Name = row.Name, Key = row.Key, Group = row.Group, IconPath = row.IconPath
                }).ToList();
                var projectionMs = diagnostics.Enabled ? NavigatorDiagnostics.MillisecondsSince(projectionStart) : 0;
                var viewStart = diagnostics.Enabled ? Stopwatch.GetTimestamp() : 0;
                syncing = true;
                try {
                    view = new ListCollectionView(rows);
                    if (prefs.Grouping != 0) view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
                    list.ItemsSource = view; ApplySearch();
                } finally { syncing = false; }
                SyncSelection(false); status.Text = ""; reportedError = false;
                diagnostics.Record(diagnosticState, changed, membershipChanged, orderChanged,
                    viewStateChanged, true,
                    diagnostics.Enabled ? NavigatorDiagnostics.MillisecondsSince(refreshStart) : 0,
                    projectionMs, diagnostics.Enabled ? NavigatorDiagnostics.MillisecondsSince(viewStart) : 0);
            });
        }
        private void ApplySearch()
        {
            if (view == null) return;
            var key = NavigatorProjection.SearchKey(search.Text);
            syncing = true;
            try { view.Filter = obj => NavigatorProjection.Matches(((GameRow)obj).Key, key); view.Refresh(); }
            finally { syncing = false; }
            count.Text = NavigatorProjection.DistinctGameCount(view.Cast<GameRow>().Select(r => r.Id)) + " games";
            status.Text = view.IsEmpty ? "No games found. Check the global filters too." : "";
            SyncSelection(false);
        }
        private void SyncSelection(bool scroll)
        {
            if (syncing || view == null) return;
            var selected = library.SelectedIds();
            var current = list.SelectedItem as GameRow;
            if (current != null && selected.Contains(current.Id) && view.Contains(current)) return;
            var row = view.Cast<GameRow>().FirstOrDefault(r => selected.Contains(r.Id));
            syncing = true;
            try { list.SelectedItem = row; if (scroll && row != null) list.ScrollIntoView(row); }
            finally { syncing = false; }
        }
        private void ApplySize()
        {
            if (active && (library.ActiveDesktopView == DesktopView.List ||
                (library.ActiveDesktopView == DesktopView.Details && !navigatedDetails))) {
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
            backButton.Visibility = navigatedDetails && library.ActiveDesktopView == DesktopView.Details && !collapsed ? Visibility.Visible : Visibility.Collapsed;
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
                library.ActiveDesktopView != DesktopView.Grid || Keyboard.Modifiers != ModifierKeys.None) return;
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
                library.ActiveDesktopView != DesktopView.Grid) return;
            var source = args.OriginalSource as DependencyObject;
            if (source == null || ItemsControl.ContainerFromElement(nativeGridList, source) != item) return;
            var position = args.GetPosition(nativeGridList);
            if (Math.Abs(position.X - coverDownPosition.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - coverDownPosition.Y) > SystemParameters.MinimumVerticalDragDistance) return;
            var selected = library.SelectedGames;
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
            if (lastDesktopView != library.ActiveDesktopView) {
                sidebarClickTimer.Stop();
                coverClickTimer.Stop();
                sidebarDownItem = null;
                if (lastDesktopView == DesktopView.Details)
                    navigatedDetails = false;
                lastDesktopView = library.ActiveDesktopView;
            }
            ApplySize();
            var host = window ?? Window.GetWindow(this);
            if (host == null) return;
            BindReopenButton(host);
            ApplySize();
            if (navigatedDetails && library.ActiveDesktopView == DesktopView.Details) {
                nativeDetailsList = FindNamed(host, "HeliumCharlo_NativeDetailsList");
                if (nativeDetailsList != null) nativeDetailsList.Visibility = Visibility.Collapsed;
            } else ShowNativeList();
            var gridView = FindNamed(host, "PART_ViewGrid") as Control;
            BindGridClicks(gridView);
            // Do not alter the converter's XAML or the overview itself. Its host border owns the space.
            if (gridView == null || library.ActiveDesktopView != DesktopView.Grid) { RestoreDetails(); return; }
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
}
