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
using Playnite.SDK.Events;
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
        private readonly Popup sortGroupPopup;
        private readonly Button sortGroupButton;
        private readonly TextBlock sortSummary;
        private readonly ComboBox groupOptions, sortOptions;
        private readonly RadioButton orderAscending, orderDescending;
        private readonly FrameworkElement orderSection;
        private bool updatingSortGroupUi;
        private readonly TextBlock count, status;
        private readonly Button toggle;
        private readonly Thumb grip;
        private readonly FrameworkElement tools;
        private readonly Button homeButton;
        private readonly Button recentButton, readyButton, filtersButton;
        private readonly Button closeFilters, clearAllFilters;
        private readonly TextBlock filterIndicator;
        private readonly Popup filtersPopup;
        private readonly TextBox filterOptionSearch;
        private readonly NavigatorFilterPanel filterPanel;
        private readonly ReadyToPlayState readyState = new ReadyToPlayState();
        private bool filtersOpen, shortcutGroupingChange;
        private volatile bool filterOptionsDirty;
        private int? shortcutGroupingOverride;
        private int previousGrouping;
        private int SelectedGrouping
        {
            get => (grouping.SelectedItem as OrganizationMode)?.Id ?? 0;
            set => grouping.SelectedItem = NavigatorOrganization.Group(value);
        }
        private Button reopenButton;
        private readonly DispatcherTimer timer, searchTimer;
        private readonly DispatcherTimer coverClickTimer, sidebarClickTimer;
        private Guid pendingSidebarId;
        private Guid navigatorSelectionId;
        private DateTime navigatorSelectionAt;
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
        private DispatcherOperation pendingViewSelection;
        private DispatcherOperation pendingOrganizationRestore;
        private readonly NavigatorOrganizationSelection organizationSelection = new NavigatorOrganizationSelection();
        private int dirty = 1;
        private Guid[] lastIds = new Guid[0];
        private readonly object updateGate = new object();
        private readonly HashSet<Guid> pendingGameIds = new HashSet<Guid>();
        private Guid[] lastOrderIds = new Guid[0];
        private NavigatorViewSnapshot lastViewState;
        private List<GameRow> rows = new List<GameRow>();
        private ListCollectionView view;
        private int rebuildGeneration;
        private HashSet<Guid> revealAfterExplicitReorganization;
        private int NavigatorGrouping => shortcutGroupingOverride ?? prefs.Grouping;
        private NavigatorSortMode SortMode => NavigatorSortPolicy.ValidMode(prefs.SortMode);
        private int EffectiveGrouping => NavigatorRefreshDecision.EffectiveGrouping(NavigatorGrouping, library.Grouping, prefs.RecentActivityGroupingStyle);
        private readonly NavigatorGroupState groupState = new NavigatorGroupState();
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
            list.Tag = groupState;
            grouping = (ComboBox)root.FindName("Grouping"); count = (TextBlock)root.FindName("Count");
            sortGroupPopup = (Popup)root.FindName("SortGroupPopup");
            sortGroupButton = (Button)root.FindName("SortGroupButton");
            sortSummary = (TextBlock)root.FindName("SortSummary");
            groupOptions = (ComboBox)root.FindName("GroupOptions");
            sortOptions = (ComboBox)root.FindName("SortOptions");
            orderAscending = (RadioButton)root.FindName("OrderAscending");
            orderDescending = (RadioButton)root.FindName("OrderDescending");
            orderSection = (FrameworkElement)root.FindName("OrderSection");
            status = (TextBlock)root.FindName("Status"); toggle = (Button)root.FindName("CollapseButton");
            grip = (Thumb)root.FindName("ResizeGrip"); tools = (FrameworkElement)root.FindName("Tools");
            homeButton = (Button)root.FindName("HomeButton");
            recentButton = (Button)root.FindName("RecentButton");
            readyButton = (Button)root.FindName("ReadyButton");
            filtersButton = (Button)root.FindName("FiltersButton");
            closeFilters = (Button)root.FindName("CloseFilters");
            clearAllFilters = (Button)root.FindName("ClearAllFilters");
            filterIndicator = (TextBlock)root.FindName("FilterIndicator");
            filtersPopup = (Popup)root.FindName("FiltersPopup");
            filterOptionSearch = (TextBox)root.FindName("FilterOptionSearch");
            filterPanel = new NavigatorFilterPanel(prefs, (Border)root.FindName("FilterWorkspaceFrame"),
                (FilterWorkspaceColumns)root.FindName("FilterSections"),
                (WrapPanel)root.FindName("ActiveFilters"), filterOptionSearch,
                (TextBlock)root.FindName("FilterSearchEmpty"),
                field => field.Choices(api),
                change => Safe(() => ApplyGlobalFilterChange(change)));
            grouping.ItemsSource = NavigatorOrganization.Groups;
            SelectedGrouping = prefs.Grouping;
            previousGrouping = SelectedGrouping == 4 ? 0 : SelectedGrouping;
            groupState.Mode = EffectiveGrouping;
            timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(1500) };
            timer.Tick += (s, e) => { RecomputeLayout(); Refresh(); Safe(RefreshGlobalFilterUi); UpdateSortGroupUi(); };
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
            grouping.SelectionChanged += (s, e) => {
                if (!(grouping.SelectedItem is OrganizationMode) || updatingSortGroupUi) return;
                if (shortcutGroupingChange)
                    shortcutGroupingOverride = Logic.ShortcutGroupingOverride(prefs.Grouping, SelectedGrouping);
                else { shortcutGroupingOverride = null; prefs.Grouping = SelectedGrouping; }
                if (SelectedGrouping != 4) previousGrouping = SelectedGrouping;
                groupState.Mode = EffectiveGrouping;
                Interlocked.Exchange(ref dirty, 1); Refresh();
                UpdateShortcutState();
                UpdateSortGroupUi();
                if (!shortcutGroupingChange) Save();
            };
            sortGroupButton.Click += (s, e) => { filtersPopup.IsOpen = false; UpdateSortGroupUi(); sortGroupPopup.IsOpen = !sortGroupPopup.IsOpen; };
            NavigatorOrganizationControls.ConfigureSelector(groupOptions, mode => {
                if (updatingSortGroupUi || mode.Id == SelectedGrouping) return;
                RequestRevealAfterExplicitReorganization();
                if (prefs.ApplySortGroupToPlaynite) Safe(() => ApplyNativeOrganization(() => library.ApplyGrouping(mode.Id)));
                SelectedGrouping = mode.Id;
            });
            NavigatorOrganizationControls.ConfigureSelector(sortOptions, mode => {
                if (!updatingSortGroupUi && mode.Id != (int)SortMode) SetSortMode(mode.SortPolicy);
            });
            PopulateOrganizationChoices();
            sortGroupPopup.Opened += (s, e) => { PopulateOrganizationChoices(); groupOptions.Focus(); };
            sortGroupPopup.Closed += (s, e) => {
                groupOptions.IsDropDownOpen = sortOptions.IsDropDownOpen = false;
            };
            orderAscending.Checked += (s, e) => SetSortDirection(false);
            orderDescending.Checked += (s, e) => SetSortDirection(true);
            ((Button)root.FindName("SortClose")).Click += (s, e) => DismissSortGroup();
            ((Button)root.FindName("SortDone")).Click += (s, e) => DismissSortGroup();
            sortGroupPopup.PreviewKeyDown += SortGroupPopupPreviewKeyDown;
            homeButton.Click += (sender, args) => Safe(ReturnToGrid);
            recentButton.Click += (sender, args) => Safe(() => {
                shortcutGroupingChange = true;
                try { SelectedGrouping = Logic.ToggleRecentGrouping(SelectedGrouping, previousGrouping); }
                finally { shortcutGroupingChange = false; }
            });
            readyButton.Click += (sender, args) => Safe(() => ApplyGlobalFilterChange(state => readyState.Toggle(state)));
            filtersPopup.CustomPopupPlacementCallback = filterPanel.Place;
            filtersButton.Click += (sender, args) => Safe(() => {
                sortGroupPopup.IsOpen = false;
                if (!filtersPopup.IsOpen) filterPanel.Position(filtersPopup, window);
                filtersPopup.IsOpen = !filtersPopup.IsOpen;
            });
            filtersPopup.Opened += (s, e) => {
                filtersOpen = true;
                filterOptionsDirty = true;
                Safe(RefreshGlobalFilterUi);
                filterOptionSearch.Focus();
            };
            filtersPopup.Closed += (s, e) => {
                filtersOpen = false;
                filterPanel.FlushText();
                Safe(RefreshGlobalFilterUi);
                if (active && !IsCollapsed()) filtersButton.Focus();
            };
            filtersPopup.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { filtersPopup.IsOpen = false; e.Handled = true; } };
            closeFilters.Click += (sender, args) => filtersPopup.IsOpen = false;
            clearAllFilters.Click += (sender, args) => filterPanel.ClearAll();
            toggle.Click += (sender, args) => {
                sortGroupPopup.IsOpen = false;
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
            UpdateSortGroupUi();
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
        private void SelectGame(Guid id)
        {
            navigatorSelectionId = id; navigatorSelectionAt = DateTime.UtcNow;
            api.MainView.SelectGame(id);
        }
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
            if (prefs.PreserveGridScroll) CaptureGridScroll();
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
            if (!active || IsCollapsed() || library.ActiveDesktopView != DesktopView.Details) return;
            var switchButton = FindNamed(window, "HeliumCharlo_SwitchGrid") as Button;
            if (switchButton?.Command == null || !switchButton.Command.CanExecute(null)) return;
            switchButton.Command.Execute(null);
            navigatedDetails = false;
            RecomputeLayout();
        }
        private void CaptureGridScroll()
        {
            var grid = FindNamed(window, "PART_ViewGrid") as Control;
            var games = grid?.Template?.FindName("PART_ListGames", grid) as DependencyObject;
            var scroll = FindScroll(games);
            gridIds = scroll == null ? null : library.SortedFilteredIds();
            if (gridIds == null) return;
            gridSort = library.SortOrder;
            gridSortDirection = library.SortDirection;
            gridGrouping = library.Grouping;
            gridOffset = scroll.VerticalOffset;
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
            plugin.SelectionChanged += NativeSelectionChanged;
            plugin.SettingsChanged += SettingsUpdated;
            api.Database.Games.ItemUpdated += GamesUpdated;
            api.Database.Games.ItemCollectionChanged += GamesChanged;
            api.Database.Series.ItemUpdated += MetadataUpdated;
            api.Database.Categories.ItemUpdated += MetadataUpdated;
            api.Database.Platforms.ItemUpdated += MetadataUpdated;
            api.Database.Genres.ItemUpdated += MetadataUpdated;
            api.Database.Tags.ItemUpdated += MetadataUpdated;
            api.Database.Sources.ItemUpdated += MetadataUpdated;
            api.Database.CompletionStatuses.ItemUpdated += MetadataUpdated;
            api.Database.AgeRatings.ItemUpdated += MetadataUpdated;
            api.Database.Regions.ItemUpdated += MetadataUpdated;
            api.Database.Features.ItemUpdated += MetadataUpdated;
            api.Database.Companies.ItemUpdated += MetadataUpdated;
            api.Database.Series.ItemCollectionChanged += MetadataChanged;
            api.Database.Categories.ItemCollectionChanged += MetadataChanged;
            api.Database.Platforms.ItemCollectionChanged += MetadataChanged;
            api.Database.Genres.ItemCollectionChanged += MetadataChanged;
            api.Database.Tags.ItemCollectionChanged += MetadataChanged;
            api.Database.Sources.ItemCollectionChanged += MetadataChanged;
            api.Database.CompletionStatuses.ItemCollectionChanged += MetadataChanged;
            api.Database.AgeRatings.ItemCollectionChanged += MetadataChanged;
            api.Database.Regions.ItemCollectionChanged += MetadataChanged;
            api.Database.Features.ItemCollectionChanged += MetadataChanged;
            api.Database.Companies.ItemCollectionChanged += MetadataChanged;
            lastDesktopView = library.ActiveDesktopView;
            organizationSelection.ObserveOrganization(library.ViewState(NavigatorGrouping));
            InputManager.Current.PreProcessInput += BeforeOrganizationInput;
            window = Window.GetWindow(this);
            if (window != null) {
                window.SizeChanged += WindowSizeChanged;
                window.PreviewKeyDown += WindowPreviewKeyDown;
            }
            RecomputeLayout();
            Interlocked.Exchange(ref dirty, 1); Refresh(); RefreshGlobalFilterUi(); timer.Start();
        }
        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            sortGroupPopup.IsOpen = false;
            filtersPopup.IsOpen = false;
            if (!active) return; active = false; timer.Stop(); searchTimer.Stop(); coverClickTimer.Stop(); sidebarClickTimer.Stop(); sidebarDownItem = null; panelRequested = false; UnbindGridClicks();
            plugin.SelectionChanged -= NativeSelectionChanged;
            InputManager.Current.PreProcessInput -= BeforeOrganizationInput;
            organizationSelection.Cancel();
            pendingOrganizationRestore?.Abort();
            pendingOrganizationRestore = null;
            pendingViewSelection?.Abort();
            pendingViewSelection = null;
            gridIds = null;
            plugin.SettingsChanged -= SettingsUpdated;
            api.Database.Games.ItemUpdated -= GamesUpdated;
            api.Database.Games.ItemCollectionChanged -= GamesChanged;
            api.Database.Series.ItemUpdated -= MetadataUpdated;
            api.Database.Categories.ItemUpdated -= MetadataUpdated;
            api.Database.Platforms.ItemUpdated -= MetadataUpdated;
            api.Database.Genres.ItemUpdated -= MetadataUpdated;
            api.Database.Tags.ItemUpdated -= MetadataUpdated;
            api.Database.Sources.ItemUpdated -= MetadataUpdated;
            api.Database.CompletionStatuses.ItemUpdated -= MetadataUpdated;
            api.Database.AgeRatings.ItemUpdated -= MetadataUpdated;
            api.Database.Regions.ItemUpdated -= MetadataUpdated;
            api.Database.Features.ItemUpdated -= MetadataUpdated;
            api.Database.Companies.ItemUpdated -= MetadataUpdated;
            api.Database.Series.ItemCollectionChanged -= MetadataChanged;
            api.Database.Categories.ItemCollectionChanged -= MetadataChanged;
            api.Database.Platforms.ItemCollectionChanged -= MetadataChanged;
            api.Database.Genres.ItemCollectionChanged -= MetadataChanged;
            api.Database.Tags.ItemCollectionChanged -= MetadataChanged;
            api.Database.Sources.ItemCollectionChanged -= MetadataChanged;
            api.Database.CompletionStatuses.ItemCollectionChanged -= MetadataChanged;
            api.Database.AgeRatings.ItemCollectionChanged -= MetadataChanged;
            api.Database.Regions.ItemCollectionChanged -= MetadataChanged;
            api.Database.Features.ItemCollectionChanged -= MetadataChanged;
            api.Database.Companies.ItemCollectionChanged -= MetadataChanged;
            if (window != null) {
                window.SizeChanged -= WindowSizeChanged;
                window.PreviewKeyDown -= WindowPreviewKeyDown;
            }
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
            PopulateOrganizationChoices();
            filterPanel.InvalidateLayout();
            Safe(RefreshGlobalFilterUi);
            if (groupState.Mode != EffectiveGrouping) {
                groupState.Mode = EffectiveGrouping;
                Interlocked.Exchange(ref dirty, 1);
                Refresh();
            }
        }
        private void MetadataUpdated<T>(object sender, ItemUpdatedEventArgs<T> args) where T : DatabaseObject { filterOptionsDirty = true; Interlocked.Exchange(ref dirty, 1); }
        private void MetadataChanged<T>(object sender, ItemCollectionChangedEventArgs<T> args) where T : DatabaseObject { filterOptionsDirty = true; Interlocked.Exchange(ref dirty, 1); }
        private void GamesUpdated(object sender, ItemUpdatedEventArgs<Game> args)
        {
            lock (updateGate) foreach (var update in args.UpdatedItems) {
                pendingGameIds.Add(update.NewData.Id);
                if (update.OldData.ReleaseYear != update.NewData.ReleaseYear) filterOptionsDirty = true;
            }
        }
        private void GamesChanged(object sender, ItemCollectionChangedEventArgs<Game> args)
        {
            filterOptionsDirty = true;
            Interlocked.Exchange(ref dirty, 1);
        }
        private void NativeSelectionChanged(OnGameSelectedEventArgs args)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => NativeSelectionChanged(args))); return; }
            if (!active) return;
            Safe(() => {
                var target = library.ActiveDesktopView;
                bool enteringDetails = lastDesktopView == DesktopView.Grid && target == DesktopView.Details;
                bool returningToGrid = lastDesktopView == DesktopView.Details && target == DesktopView.Grid;
                var previousId = args.OldValue?.Count == 1 ? args.OldValue[0].Id : Guid.Empty;
                var sameGrid = lastDesktopView == DesktopView.Grid && target == DesktopView.Grid;
                if (organizationSelection.ObserveSelection(library.ViewState(NavigatorGrouping), sameGrid,
                    previousId, args.NewValue?.Count > 0) && pendingOrganizationRestore == null) {
                    pendingOrganizationRestore = Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                        new Action(RestoreOrganizationSelection));
                }
                if ((enteringDetails || returningToGrid) && previousId != Guid.Empty && !(args.NewValue?.Count > 0)) {
                    if (enteringDetails && !navigatedDetails && prefs.PreserveGridScroll) CaptureGridScroll();
                    pendingViewSelection?.Abort();
                    // Playnite clears selection while changing GamesViewType. Apply the previous
                    // selection once the new view has processed its bindings and layout.
                    pendingViewSelection = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
                        pendingViewSelection = null;
                        if (!active || library.ActiveDesktopView != target) return;
                        Safe(() => {
                            RecomputeLayout();
                            if (Logic.ShouldRestoreSelection(previousId, library.SelectedIds(),
                                () => library.FilteredGames.Any(game => game.Id == previousId))) {
                                SelectGame(previousId);
                            }
                            SyncSelection(false);
                            if (returningToGrid && prefs.PreserveGridScroll) RestoreGridScroll();
                        });
                    }));
                    return;
                }
                SelectionChanged();
            });
        }
        private void RestoreOrganizationSelection()
        {
            pendingOrganizationRestore = null;
            if (!active) return;
            Safe(() => {
                var id = organizationSelection.Take(library.ActiveDesktopView == DesktopView.Grid,
                    library.SelectedIds(), candidate => library.FilteredGames.Any(game => game.Id == candidate));
                if (id == Guid.Empty) return;
                SelectGame(id);
                Refresh();
                if (prefs.AutomaticallyRevealSelectedGame) RevealSelected(library.SelectedIds());
            });
        }
        private void BeforeOrganizationInput(object sender, PreProcessInputEventArgs args)
        {
            var input = args.StagingItem.Input.RoutedEvent;
            if (input != Mouse.PreviewMouseDownEvent && input != Keyboard.PreviewKeyDownEvent) return;
            // Native toolbar menus also run on this Dispatcher. A new gesture owns the selection.
            organizationSelection.Cancel();
            organizationSelection.ObserveOrganization(library.ViewState(NavigatorGrouping));
        }
        private void SelectionChanged()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(SelectionChanged)); return; }
            if (active) Safe(() => {
                var selected = library.SelectedIds();
                var fromNavigator = DateTime.UtcNow - navigatorSelectionAt < TimeSpan.FromMilliseconds(750) &&
                    selected.Contains(navigatorSelectionId);
                if (prefs.AutomaticallyRevealSelectedGame && !fromNavigator) RevealSelected(selected);
                else SyncSelection(false);
            });
        }
        public override void GameContextChanged(Game oldContext, Game newContext) { SelectionChanged(); }
        private void Refresh()
        {
            if (!active || IsCollapsed() || Visibility != Visibility.Visible) return;
            Safe(() => {
                var refreshStart = diagnostics.Enabled ? Stopwatch.GetTimestamp() : 0;
                var games = library.FilteredGames.ToList();
                // Filter membership, native sequence, and view state are separate change signals.
                var orderIds = games.Select(g => g.Id).ToArray();
                var orderChanged = NavigatorRefreshDecision.OrderChanged(orderIds, lastOrderIds);
                var ids = orderChanged ? NavigatorRefreshDecision.SortedIds(orderIds) : lastIds;
                var changed = Interlocked.Exchange(ref dirty, 0) != 0;
                var membershipChanged = NavigatorRefreshDecision.MembershipChanged(ids, lastIds);
                var viewState = library.ViewState(NavigatorGrouping);
                organizationSelection.ObserveOrganization(viewState);
                var viewStateChanged = NavigatorRefreshDecision.ViewStateChanged(viewState, lastViewState);
                if (!changed && !membershipChanged && !orderChanged && !viewStateChanged)
                    changed = UpdateGameRows(games);
                else ClearPendingGameUpdates();
                var rebuild = NavigatorRefreshDecision.ShouldRebuild(changed, membershipChanged,
                    orderChanged, viewStateChanged);
                var diagnosticState = diagnostics.Enabled
                    ? diagnostics.Capture(() => library.DiagnosticViewState(NavigatorGrouping)) : null;
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
                var selectedRow = list.SelectedItem as GameRow;
                var scrollOffset = FindScroll(list)?.VerticalOffset ?? 0;
                var effectiveGrouping = EffectiveGrouping;
                groupState.Mode = effectiveGrouping;
                var projectionStart = diagnostics.Enabled ? Stopwatch.GetTimestamp() : 0;
                var projected = NavigatorProjection.Build(library.Sources(games, effectiveGrouping), effectiveGrouping,
                    library.ResolveIcon, sortMode: SortMode, descending: prefs.SortDescending);
                var movedSelected = selectedRow != null && library.IsSelected(selectedRow.Id) &&
                    NavigatorRefreshDecision.SelectedGroupMoved(selectedRow.Id, selectedRow.GroupIdentity, projected);
                rows = projected.Select(row => new GameRow {
                    Id = row.Id, Name = row.Name, Key = row.Key, Group = row.Group, GroupIdentity = row.GroupIdentity, IconPath = row.IconPath, ActivityTicks = row.ActivityTicks, GroupOrder = row.GroupOrder,
                    IsInstalled = row.IsInstalled, Favorite = row.Favorite, SortValue = row.SortValue, SortText = row.SortText
                }).ToList();
                var projectionMs = diagnostics.Enabled ? NavigatorDiagnostics.MillisecondsSince(projectionStart) : 0;
                var viewStart = diagnostics.Enabled ? Stopwatch.GetTimestamp() : 0;
                syncing = true;
                try {
                    view = new ListCollectionView(rows);
                    if (effectiveGrouping != 0) view.GroupDescriptions.Add(new PropertyGroupDescription("GroupIdentity"));
                    list.ItemsSource = view; ApplySearch();
                    if (selectedRow != null) {
                        var occurrence = rows.FirstOrDefault(row => row.Id == selectedRow.Id && Equals(row.GroupIdentity, selectedRow.GroupIdentity) && view.Contains(row));
                        if (occurrence != null && library.IsSelected(occurrence.Id)) list.SelectedItem = occurrence;
                    }
                } finally { syncing = false; }
                SyncSelection(false); status.Text = ""; reportedError = false;
                var generation = ++rebuildGeneration;
                var currentView = view;
                var revealRequest = revealAfterExplicitReorganization;
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
                    if (!active || generation != rebuildGeneration || !ReferenceEquals(view, currentView)) return;
                    if (revealRequest != null) {
                        if (ReferenceEquals(revealAfterExplicitReorganization, revealRequest))
                            revealAfterExplicitReorganization = null;
                        var selected = library.SelectedIds();
                        selected.IntersectWith(revealRequest);
                        if (selected.Count != 0 && currentView.Cast<GameRow>().Any(row => selected.Contains(row.Id))) {
                            RevealSelected(selected);
                            return;
                        }
                    }
                    if (movedSelected) {
                        var selected = library.SelectedIds();
                        if (selected.Contains(selectedRow.Id) && currentView.Cast<GameRow>().Any(row => row.Id == selectedRow.Id)) {
                            RevealSelected(new HashSet<Guid> { selectedRow.Id });
                            return;
                        }
                    }
                    FindScroll(list)?.ScrollToVerticalOffset(scrollOffset);
                }));
                diagnostics.Record(diagnosticState, changed, membershipChanged, orderChanged,
                    viewStateChanged, true,
                    diagnostics.Enabled ? NavigatorDiagnostics.MillisecondsSince(refreshStart) : 0,
                    projectionMs, diagnostics.Enabled ? NavigatorDiagnostics.MillisecondsSince(viewStart) : 0);
            });
        }
        private Guid[] TakePendingGameUpdates()
        {
            lock (updateGate) {
                var ids = pendingGameIds.ToArray();
                pendingGameIds.Clear();
                return ids;
            }
        }
        private void ClearPendingGameUpdates() { TakePendingGameUpdates(); }
        private bool UpdateGameRows(IReadOnlyList<Game> games)
        {
            var pending = TakePendingGameUpdates();
            if (pending.Length == 0) return false;
            var pendingSet = new HashSet<Guid>(pending);
            var byId = games.Where(game => pendingSet.Contains(game.Id)).ToDictionary(game => game.Id);
            var occurrences = rows.Where(row => pendingSet.Contains(row.Id)).ToLookup(row => row.Id);
            foreach (var id in pending) {
                var existing = occurrences[id].ToList();
                if (existing.Count == 0 || !byId.TryGetValue(id, out var game)) continue;
                var projected = NavigatorProjection.Build(library.Sources(new[] { game }, EffectiveGrouping),
                    EffectiveGrouping, library.ResolveIcon, sortMode: SortMode, descending: prefs.SortDescending);
                var prior = existing.Select(row => new NavigatorProjectedRow {
                    Id = row.Id, Name = row.Name, Key = row.Key, Group = row.Group, GroupIdentity = row.GroupIdentity,
                    IconPath = row.IconPath, IsInstalled = row.IsInstalled, Favorite = row.Favorite,
                    ActivityTicks = row.ActivityTicks, GroupOrder = row.GroupOrder, SortValue = row.SortValue, SortText = row.SortText
                });
                var kind = NavigatorRefreshDecision.ClassifyRows(prior, projected);
                if (kind == NavigatorRowChange.Structural) return true;
                if (kind == NavigatorRowChange.Visual) {
                    diagnostics.VisualUpdate(existing.Count);
                    foreach (var row in existing) {
                        row.IsInstalled = game.IsInstalled;
                        row.Favorite = game.Favorite;
                    }
                }
            }
            return false;
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
        private void ApplyGlobalFilterChange(Func<FilterPresetSettings, FilterPresetSettings> change)
        {
            var current = api.MainView.GetCurrentFilterSettings() ?? new FilterPresetSettings();
            var next = change(current);
            if (NavigatorFilterComposer.Same(current, next)) return;
            organizationSelection.Cancel();
            // A temporary, unsaved preset with null view options changes filters without changing native sort or grouping.
            api.MainView.ApplyFilterPreset(NavigatorFilterComposer.TemporaryPreset(next));
            Interlocked.Exchange(ref dirty, 1);
            RefreshGlobalFilterUi();
            Refresh();
        }
        private static string FriendlySort(string value)
        {
            if (value == "LastActivity") return "Last Played";
            if (value == "PlayCount") return "Most Played";
            return System.Text.RegularExpressions.Regex.Replace(value ?? "Name", "(?<=[a-z])(?=[A-Z])", " ");
        }
        private void PopulateOrganizationChoices()
        {
            updatingSortGroupUi = true;
            try {
                NavigatorOrganizationControls.Choices(groupOptions, prefs.VisibleGroupingModes, true, SelectedGrouping);
                NavigatorOrganizationControls.Choices(sortOptions, prefs.VisibleSortingModes, false, (int)SortMode);
            } finally { updatingSortGroupUi = false; }
            UpdateSortGroupUi();
        }
        private void UpdateSortGroupUi()
        {
            if (sortSummary == null) return;
            var recent = SelectedGrouping == 4;
            var mode = SortMode;
            var sort = mode == NavigatorSortMode.FollowingPlaynite ? FriendlySort(library.SortOrder) : NavigatorOrganization.Sort((int)mode).Label;
            var direction = mode == NavigatorSortMode.FollowingPlaynite ? library.SortDirection :
                (prefs.SortDescending ? "Descending" : "Ascending");
            var inherited = mode == NavigatorSortMode.FollowingPlaynite;
            sortSummary.Text = recent && inherited ? "Recent Activity · Newest first" :
                NavigatorOrganization.Group(SelectedGrouping).Label + " · " + sort + " · " + direction;
            updatingSortGroupUi = true;
            try {
                // Hidden active modes stay represented until another mode is explicitly chosen.
                var group = NavigatorOrganization.Group(SelectedGrouping);
                if (!groupOptions.IsKeyboardFocusWithin && !groupOptions.Items.Contains(group)) NavigatorOrganizationControls.Choices(groupOptions, prefs.VisibleGroupingModes, true, SelectedGrouping);
                if (!groupOptions.IsKeyboardFocusWithin) groupOptions.SelectedItem = group;
                if (!sortOptions.IsKeyboardFocusWithin) sortOptions.SelectedItem = NavigatorOrganization.Sort((int)mode);
                orderSection.Visibility = inherited ? Visibility.Collapsed : Visibility.Visible;
                orderAscending.IsChecked = !prefs.SortDescending;
                orderDescending.IsChecked = prefs.SortDescending;
            } finally { updatingSortGroupUi = false; }
        }
        private void SetSortMode(NavigatorSortMode mode)
        {
            if (updatingSortGroupUi) return;
            Safe(() => {
                RequestRevealAfterExplicitReorganization();
                prefs.SortMode = (int)mode;
                if (prefs.ApplySortGroupToPlaynite) ApplyNativeOrganization(() => library.ApplySorting(mode, prefs.SortDescending));
                Interlocked.Exchange(ref dirty, 1); Refresh(); Save();
                UpdateSortGroupUi();
            });
        }
        private void SetSortDirection(bool descending)
        {
            if (updatingSortGroupUi || SortMode == NavigatorSortMode.FollowingPlaynite) return;
            Safe(() => {
                RequestRevealAfterExplicitReorganization();
                prefs.SortDescending = descending;
                if (prefs.ApplySortGroupToPlaynite) ApplyNativeOrganization(() => library.ApplySorting(SortMode, descending));
                Interlocked.Exchange(ref dirty, 1); Refresh(); Save();
                UpdateSortGroupUi();
            });
        }
        private void RequestRevealAfterExplicitReorganization()
        {
            var selected = library.SelectedIds();
            revealAfterExplicitReorganization = selected.Count == 0 ? null : selected;
        }
        private void ApplyNativeOrganization(Action apply)
        {
            organizationSelection.ObserveOrganization(library.ViewState(NavigatorGrouping));
            apply();
        }
        private void SortGroupPopupPreviewKeyDown(object sender, KeyEventArgs args)
        {
            CloseSortGroupOnEscape(args);
        }
        private void WindowPreviewKeyDown(object sender, KeyEventArgs args)
        {
            CloseSortGroupOnEscape(args);
        }
        private void CloseSortGroupOnEscape(KeyEventArgs args)
        {
            if (!sortGroupPopup.IsOpen || args.Key != Key.Escape) return;
            if (groupOptions.IsDropDownOpen || sortOptions.IsDropDownOpen) {
                groupOptions.IsDropDownOpen = sortOptions.IsDropDownOpen = false;
                args.Handled = true;
                return;
            }
            DismissSortGroup();
            args.Handled = true;
        }
        private void DismissSortGroup()
        {
            sortGroupPopup.IsOpen = false;
            sortGroupButton.Focus();
        }
        private void RefreshGlobalFilterUi()
        {
            if (!active || IsCollapsed() || Visibility != Visibility.Visible) return;
            var current = api.MainView.GetCurrentFilterSettings() ?? new FilterPresetSettings();
            readyState.Observe(current);
            var hasFilters = NavigatorFilterComposer.HasActiveFilters(current);
            filterIndicator.Text = hasFilters ? "•" : "";
            filtersButton.ToolTip = hasFilters ? "Library Filters (active)" : "Library Filters";
            readyButton.Foreground = (Brush)root.Resources[readyState.IsActive(current) ? "SteamActiveBrush" : "SteamIconBrush"];
            readyButton.IsEnabled = !readyState.IsActive(current) || readyState.OwnsRestriction;
            readyButton.ToolTip = readyButton.IsEnabled ? "Ready to Play: installed games" :
                "Installed filter is managed by Playnite; edit it in Library Filters";
            filtersButton.Foreground = (Brush)root.Resources[filtersOpen ? "SteamActiveBrush" : "SteamIconBrush"];
            if (filtersOpen) {
                filterPanel.Refresh(current, filterOptionsDirty);
                clearAllFilters.IsEnabled = filterPanel.HasManagedSelection;
                filterOptionsDirty = false;
            }
            UpdateShortcutState();
        }
        private void UpdateShortcutState()
        {
            recentButton.Foreground = (Brush)root.Resources[SelectedGrouping == 4 ? "SteamActiveBrush" : "SteamIconBrush"];
            homeButton.Foreground = (Brush)root.Resources[active && library.ActiveDesktopView == DesktopView.Grid ? "SteamActiveBrush" : "SteamIconBrush"];
        }
        private void RevealSelected(HashSet<Guid> selected)
        {
            if (view == null || IsCollapsed() || Visibility != Visibility.Visible) return;
            var matching = view.Cast<GameRow>().Where(item => selected.Contains(item.Id)).ToList();
            var row = matching.FirstOrDefault(item => groupState.IsExpanded(EffectiveGrouping, item.GroupIdentity)) ??
                matching.FirstOrDefault();
            if (row == null) return;
            var mode = EffectiveGrouping;
            if (mode != 0) {
                groupState.SetExpanded(mode, row.GroupIdentity, true);
                var visibleGroup = FindGroupExpander(list, row.GroupIdentity);
                if (visibleGroup != null) visibleGroup.IsExpanded = true;
            }
            syncing = true;
            try { list.SelectedItem = row; }
            finally { syncing = false; }
            list.ScrollIntoView(row);
            var currentView = view;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
                if (!active || !ReferenceEquals(view, currentView) || !view.Contains(row)) return;
                if (mode != 0) {
                    var group = FindGroupExpander(list, row.GroupIdentity);
                    if (group != null) group.IsExpanded = true;
                }
                list.ScrollIntoView(row);
            }));
        }
        private static NavigatorGroupExpander FindGroupExpander(DependencyObject root, NavigatorGroupIdentity name)
        {
            if (root is NavigatorGroupExpander expander &&
                Equals((expander.DataContext as CollectionViewGroup)?.Name, name)) return expander;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
                var match = FindGroupExpander(VisualTreeHelper.GetChild(root, i), name);
                if (match != null) return match;
            }
            return null;
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
                sortGroupPopup.IsOpen = false;
                filtersPopup.IsOpen = false;
                Visibility = Visibility.Collapsed;
                Width = 0;
                if (reopenButton != null) reopenButton.Visibility = Visibility.Collapsed;
                return;
            }
            Visibility = Visibility.Visible;
            bool collapsed = IsCollapsed();
            if (collapsed) { sortGroupPopup.IsOpen = false; filtersPopup.IsOpen = false; }
            Width = collapsed ? 0 : prefs.Width;
            tools.Visibility = status.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            list.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            grip.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            toggle.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            if (reopenButton != null) reopenButton.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
            homeButton.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            UpdateShortcutState();
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
        private void WindowSizeChanged(object sender, SizeChangedEventArgs args)
        {
            RecomputeLayout();
            if (filtersOpen) filterPanel.Position(filtersPopup, window);
        }
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
                organizationSelection.Cancel();
                sidebarClickTimer.Stop();
                coverClickTimer.Stop();
                sidebarDownItem = null;
                if (lastDesktopView == DesktopView.Details)
                    navigatedDetails = false;
                if (library.ActiveDesktopView == DesktopView.List) gridIds = null;
                lastDesktopView = library.ActiveDesktopView;
            }
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
