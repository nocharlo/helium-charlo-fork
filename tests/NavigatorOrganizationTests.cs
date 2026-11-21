using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using HeliumNavigator;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

internal static class NavigatorOrganizationTests
{
    private static int checks;
    private static readonly DateTime Now = new DateTime(2026, 10, 20, 12, 0, 0);
    private sealed class RecordingGameCommand : System.Windows.Input.ICommand
    {
        public Game Executed { get; private set; }
        public bool Allowed { get; set; } = true;
        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) => Allowed && parameter is Game;
        public void Execute(object parameter) => Executed = (Game)parameter;
    }
    private sealed class MenuProvider : GenericPlugin
    {
        private readonly Func<GetGameMenuItemsArgs, IEnumerable<GameMenuItem>> discover;
        public MenuProvider(Func<GetGameMenuItemsArgs, IEnumerable<GameMenuItem>> discover) : base(null)
        { this.discover = discover; }
        public override Guid Id { get; } = Guid.NewGuid();
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args) => discover(args);
    }
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static NavigatorSource Game(int id, string name = "Synthetic") => new NavigatorSource { Id = new Guid(id, 0, 0, new byte[8]), Name = name };
    private static List<NavigatorProjectedRow> Build(IEnumerable<NavigatorSource> games, int group, NavigatorSortMode sort = NavigatorSortMode.Name, bool descending = false) =>
        NavigatorProjection.Build(games, group, icon => icon, Now, sort, descending);

    [STAThread]
    private static void Main()
    {
        CatalogAndGroups(); Sorting(); Settings(); RemovedDefaultModes(); NativeOrganization(); OrganizationControls(); FilterWorkspace(); GameActions(); CustomGameActions(); GridPanelAnchor(); GameMetadataActions(); Presentation(); Campaign(); Benchmarks();
        Console.WriteLine("PASS: " + checks + " Navigator organization, settings and WPF model checks.");
    }
    private static void GameActions()
    {
        var installed = new Game { Id = Guid.NewGuid(), IsInstalled = true };
        var uninstalled = new Game { Id = Guid.NewGuid() };
        var games = new Dictionary<Guid, Game> { { installed.Id, installed }, { uninstalled.Id, uninstalled } };
        Game Find(Guid id) => games.TryGetValue(id, out var game) ? game : null;
        var played = new List<Guid>();
        var installs = new List<Guid>();
        var edited = new List<Guid>();
        var actions = new NavigatorGameActions(Find, played.Add, installs.Add, id => { edited.Add(id); return false; },
            game => { throw new InvalidOperationException("Launch and edit must not write metadata"); });

        Check(actions.LaunchAction(installed.Id) == NavigatorLaunchAction.Play &&
            actions.LaunchAction(uninstalled.Id) == NavigatorLaunchAction.Install, "Menu labels follow native game state");
        actions.Launch(installed.Id, NavigatorLaunchAction.Play);
        actions.Launch(uninstalled.Id, NavigatorLaunchAction.Install);
        Check(played.SequenceEqual(new[] { installed.Id }) && installs.SequenceEqual(new[] { uninstalled.Id }),
            "Launch dispatch uses the offered action and the captured game ID");
        uninstalled.IsInstalled = true;
        actions.Launch(uninstalled.Id, NavigatorLaunchAction.Install);
        installed.IsRunning = true;
        actions.Launch(installed.Id, NavigatorLaunchAction.Play);
        Check(actions.LaunchAction(installed.Id) == NavigatorLaunchAction.Unavailable &&
            played.Count == 1 && installs.Count == 1, "Stale and disabled launch actions cannot run");
        actions.Edit(uninstalled.Id);
        games.Remove(uninstalled.Id);
        actions.Edit(uninstalled.Id);
        Check(actions.HasNativeEditor && edited.SequenceEqual(new[] { uninstalled.Id }),
            "Editor receives exactly the requested existing game");
        var legacy = new NavigatorGameActions(Find, played.Add, installs.Add, null,
            game => { throw new InvalidOperationException("Launch and edit must not write metadata"); });
        legacy.Edit(installed.Id);
        Check(!legacy.HasNativeEditor && edited.Count == 1 &&
            legacy.LaunchAction(Guid.NewGuid()) == NavigatorLaunchAction.Unavailable, "Legacy SDK and missing games degrade safely");
        var native = new RecordingGameCommand();
        var fresh = installed.GetCopy();
        games[installed.Id] = fresh;
        Check(actions.RunNative(installed.Id, native, game => game.IsInstalled) &&
            ReferenceEquals(native.Executed, fresh), "Native commands receive the fresh game for the captured ID");
        native.Allowed = false;
        Check(!actions.RunNative(installed.Id, native, game => true) &&
            !actions.RunNative(uninstalled.Id, native, game => true),
            "Native command availability and deleted games are respected");
        Check(NavigatorGameActions.WebLink("https://example.com/game") != null &&
            NavigatorGameActions.WebLink("http://example.com/game") != null &&
            NavigatorGameActions.WebLink("file:///game.exe") == null &&
            NavigatorGameActions.WebLink("custom://host/game") == null &&
            NavigatorGameActions.WebLink("game") == null, "Only absolute HTTP and HTTPS links are dispatched");
    }
    private static void CustomGameActions()
    {
        var game = new Game("Synthetic action target") { Id = Guid.NewGuid() };
        var current = game;
        var executions = new List<GameAction>();
        var errors = 0;
        var target = Guid.Empty;
        var actions = new NavigatorGameActions(id => current?.Id == id ? current : null,
            null, null, null, _ => { throw new Exception("Actions must not modify metadata"); },
            (id, action) => { target = id; executions.Add(action); });
        var menu = new MenuItem();
        void Populate() => actions.PopulateCustomActions(menu, game.Id, _ => errors++);
        void Click(int index) => ((MenuItem)menu.Items[index]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Populate();
        Check(menu.Items.Count == 0 && menu.Visibility == Visibility.Collapsed, "No auxiliary actions means no empty menu");
        game.GameActions = new System.Collections.ObjectModel.ObservableCollection<GameAction> {
            new GameAction { Name = "Play", IsPlayAction = true },
            new GameAction { Name = "Same", Type = GameActionType.File, Path = "first", Arguments = "{InstallDir}" },
            new GameAction { Name = "Same", Type = GameActionType.Script, Script = "synthetic" },
            new GameAction { Name = "Emulator", Type = GameActionType.Emulator },
            new GameAction { Name = "URL", Type = GameActionType.URL }
        };
        Populate();
        Check(menu.Visibility == Visibility.Visible && menu.Items.Cast<MenuItem>().Select(item => (string)item.Header)
            .SequenceEqual(new[] { "Same", "Same", "Emulator", "URL" }), "All auxiliary types preserve order and duplicate names");
        for (var i = 0; i < 4; i++) Click(i);
        Check(executions.Count == 4 && target == game.Id && ReferenceEquals(executions[1], game.GameActions[2]) &&
            ReferenceEquals(executions[0], game.GameActions[1]), "Exact game and action instances reach the native executor once");
        game.GameActions[1].WorkingDir = "changed";
        Click(0);
        Check(executions.Count == 4, "An edited action invalidates the open menu");
        Populate(); Click(0);
        Check(executions.Count == 5 && executions.Last().WorkingDir == "changed", "Reopening reads current actions");
        game.GameActions.Move(1, 2);
        Click(0);
        Check(executions.Count == 5, "A reordered list cannot execute a different action");
        Populate(); current = null; Click(0);
        Check(executions.Count == 5, "Deleted games do not execute stale actions");
        current = new Game("Another synthetic target") { Id = Guid.NewGuid() };
        Populate();
        Check(menu.Items.Count == 0, "Switching to a missing target clears the old menu");
        current = game;
        game.GameActions.Clear();
        game.GameActions.Add(new GameAction { Name = "Only", Type = GameActionType.URL });
        Populate();
        Check(menu.Items.Count == 1, "A single auxiliary action is offered");
        game.IsLaunching = true; Click(0);
        Check(executions.Count == 5 && errors == 0, "Busy-state changes safely suppress execution");
        game.IsLaunching = false;
        game.GameActions.Clear(); Click(0);
        Check(executions.Count == 5, "Removing the action while the menu is open does not execute a substitute");
        game.GameActions.Add(new GameAction { Name = "Only", Type = GameActionType.File });
        var failing = new NavigatorGameActions(_ => game, null, null, null, null,
            (id, action) => { throw new InvalidOperationException("synthetic failure"); });
        failing.PopulateCustomActions(menu, game.Id, _ => errors++);
        Click(0);
        Check(errors == 1, "An executor failure is contained and reported once");
    }
    private static void GridPanelAnchor()
    {
        var list = new ListBox { Width = 260, Height = 220 };
        ScrollViewer.SetCanContentScroll(list, false);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        var panel = new FrameworkElementFactory(typeof(WrapPanel));
        list.ItemsPanel = new ItemsPanelTemplate(panel);
        var tile = new FrameworkElementFactory(typeof(Border));
        tile.SetValue(FrameworkElement.WidthProperty, 94.0);
        tile.SetValue(FrameworkElement.HeightProperty, 70.0);
        list.ItemTemplate = new DataTemplate { VisualTree = tile };
        for (var i = 0; i < 100; i++) list.Items.Add(i);
        var window = new Window { Content = list, Width = 700, Height = 400, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        window.Show();
        try {
            window.UpdateLayout();
            var scroll = Descendant<ScrollViewer>(list);
            scroll.ScrollToVerticalOffset(910); list.UpdateLayout();
            list.SelectedItem = 60;
            var anchor = NavigatorGridAnchor.Capture(list);
            Check(anchor != null, "The first visible tile can be captured in a scrolled Grid");
            list.Width = 620;
            list.UpdateLayout();
            Check(anchor.Restore(list) && Equals(list.SelectedItem, 60), "Width changes restore the visual anchor without selecting it");
            list.UpdateLayout();
            Check(scroll.VerticalOffset < 910, "Widening to more columns changes the offset instead of keeping a different row at the top");
            Check(NavigatorGridAnchor.Capture(null) == null && !anchor.Restore(null), "Unavailable views safely skip anchor restoration");
            list.Items.Clear();
            Check(!anchor.Restore(list), "A removed anchor does not restore stale library content");
        } finally { window.Close(); }
    }
    private static T Descendant<T>(DependencyObject node) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is T match) return match;
            var found = Descendant<T>(child);
            if (found != null) return found;
        }
        return null;
    }
    private static void GameMetadataActions()
    {
        var original = new Game("Synthetic target") { Id = Guid.NewGuid(), Favorite = true };
        var other = new Game("Synthetic other") { Id = Guid.NewGuid() };
        var games = new Dictionary<Guid, Game> { { original.Id, original }, { other.Id, other } };
        var first = new Category { Id = Guid.NewGuid(), Name = "First" };
        var second = new Category { Id = Guid.NewGuid(), Name = "Second" };
        var backlog = new CompletionStatus { Id = Guid.NewGuid(), Name = "Backlog" };
        var done = new CompletionStatus { Id = Guid.NewGuid(), Name = "Done" };
        var writes = 0;
        var actions = new NavigatorGameActions(id => games.TryGetValue(id, out var game) ? game : null,
            null, null, null, game => { games[game.Id] = game; writes++; });

        var changed = actions.ToggleCategory(original.Id, first);
        Check(changed && games[original.Id].CategoryIds.SequenceEqual(new[] { first.Id }) &&
            original.CategoryIds == null && other.CategoryIds == null, "Assign to the captured uncategorized game only");
        actions.ToggleCategory(original.Id, second);
        Check(games[original.Id].CategoryIds.SequenceEqual(new[] { first.Id, second.Id }), "Add a category without replacing existing categories");
        actions.ToggleCategory(original.Id, first);
        Check(games[original.Id].CategoryIds.SequenceEqual(new[] { second.Id }), "Remove only the chosen category");
        actions.ToggleCategory(original.Id, second);
        Check(games[original.Id].CategoryIds == null && writes == 4, "Remove the last category using the SDK's unassigned representation");

        actions.ToggleCategory(original.Id, first);
        actions.SetCompletionStatus(original.Id, backlog.Id, backlog);
        actions.SetCompletionStatus(original.Id, done.Id, done);
        Check(games[original.Id].CompletionStatusId == done.Id && other.CompletionStatusId == Guid.Empty &&
            games[original.Id].CategoryIds.SequenceEqual(new[] { first.Id }) && games[original.Id].Favorite,
            "Replace only the captured game's status while preserving unrelated metadata");
        var ignored = new[] {
            actions.SetCompletionStatus(original.Id, done.Id, done),
            actions.SetCompletionStatus(original.Id, backlog.Id, null),
            actions.SetCompletionStatus(original.Id, backlog.Id, done),
            actions.ToggleCategory(original.Id, null),
            actions.ToggleCategory(Guid.NewGuid(), first),
            actions.SetCompletionStatus(Guid.NewGuid(), done.Id, done)
        };
        Check(ignored.All(result => !result) && writes == 7 && games[original.Id].CompletionStatusId == done.Id,
            "No writes for unchanged values, stale definitions or missing games");
        actions.SetCompletionStatus(original.Id, Guid.Empty, null);
        Check(games[original.Id].CompletionStatusId == Guid.Empty && writes == 8, "None clears the status using Guid.Empty");

        Check(actions.SetFavorite(original.Id, false) && !games[original.Id].Favorite && original.Favorite &&
            !games[other.Id].Favorite && !actions.SetFavorite(original.Id, false),
            "Remove from Favorites updates only the target and ignores an already-applied action");
        Check(actions.SetFavorite(original.Id, true) && games[original.Id].Favorite,
            "Add to Favorites restores the flag");
        Check(actions.SetHidden(other.Id, true) && games[other.Id].Hidden && !games[original.Id].Hidden &&
            !actions.SetHidden(other.Id, true), "Hide updates only the target and ignores an already-applied action");
        Check(actions.SetHidden(other.Id, false) && !games[other.Id].Hidden &&
            !actions.SetHidden(Guid.Empty, true) && !actions.SetFavorite(Guid.Empty, true) && writes == 12,
            "Unhide restores visibility and missing games cannot be changed");
    }
    private static void Campaign()
    {
        var serializer = new XmlSerializer(typeof(Preferences));
        Preferences Read(string xml) { var p = (Preferences)serializer.Deserialize(new StringReader(xml)); p.MigrateNavigation(); return p; }
        var absent = Read("<Preferences />");
        var empty = Read("<Preferences><VisibleGroupingModes /><VisibleSortingModes /></Preferences>");
        Check(absent.VisibleGroupingModes.Count > 1 && absent.VisibleSortingModes.Count > 1, "Missing lists receive catalog defaults");
        Check(empty.VisibleGroupingModes.SequenceEqual(new[] { 0 }) && empty.VisibleSortingModes.SequenceEqual(new[] { 0 }), "Explicit empty lists retain minimum safe choice");
        var obsolete = Read("<Preferences><NavigatorListDensity>0</NavigatorListDensity><Grouping>2</Grouping></Preferences>");
        Check(obsolete.Grouping == 2, "Obsolete density field does not prevent migration");

        var a = Game(301, "A Synthetic Room"); a.SortingName = "Dark Room";
        var b = Game(302, "The Synthetic Beginning"); b.SortingName = "Beginning";
        var c = Game(303, "Synthetic Finale"); c.SortingName = "Finale";
        var games = new[] { c, a, b };
        var before = Build(games, 0);
        Check(before.Select(x => x.Id).SequenceEqual(new[] { b.Id, a.Id, c.Id }), "Stored sorting names order titles independently");
        a.SortingName = "ZZZ";
        var after = Build(games, 0);
        Check(after.Select(x => x.Id).SequenceEqual(new[] { b.Id, c.Id, a.Id }), "Only sorting name changes relative order");
        Check(NavigatorRefreshDecision.ClassifyRows(before.Where(x => x.Id == a.Id), after.Where(x => x.Id == a.Id)) == NavigatorRowChange.Structural,
            "Sorting name update requests structural refresh");
        Check(after.Single(x => x.Id == a.Id).Name == "A Synthetic Room", "Sorting key does not change visible title");

        var prefs = new Preferences(); prefs.MigrateNavigation();
        var editor = (GroupBox)NavigatorOrganizationControls.VisibilityEditor(prefs, true);
        var panel = (StackPanel)editor.Content;
        var search = ((Grid)panel.Children[0]).Children.OfType<TextBox>().Single();
        var list = panel.Children.OfType<ListBox>().Single();
        var buttons = panel.Children.OfType<WrapPanel>().Single().Children.OfType<Button>().ToArray();
        var initial = prefs.VisibleGroupingModes.ToArray();
        search.Text = "Category";
        list.SelectedIndex = 0;
        buttons[2].RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        search.Text = "";
        Check(prefs.VisibleGroupingModes.SequenceEqual(NavigatorOrganization.Move(initial, 2, -1)), "Filtered reorder updates canonical order");
        Check(prefs.VisibleGroupingModes.Distinct().Count() == initial.Length && list.Items.Count == NavigatorOrganization.Groups.Length,
            "Clearing search keeps every choice without duplicates");
    }
    private static void NavigatorRowAppearance(ListBox list)
    {
        var model = new GameRow { Name = "Synthetic title", Favorite = true };
        var content = new ContentPresenter { Content = model, ContentTemplate = list.ItemTemplate };
        content.ApplyTemplate();
        var row = (Grid)VisualTreeHelper.GetChild(content, 0);
        var title = (TextBlock)row.Children[1];
        var star = (TextBlock)row.Children[2];
        var container = new ListBoxItem { Style = list.ItemContainerStyle, Content = content, DataContext = model };
        container.Measure(new Size(200, 100));
        var border = (Border)container.Template.FindName("RowBorder", container);
        var uninstalled = ((SolidColorBrush)title.Foreground).Color;
        container.IsSelected = true;
        Check(uninstalled == Color.FromRgb(159, 170, 183) && ((SolidColorBrush)title.Foreground).Color == uninstalled &&
            ((SolidColorBrush)border.BorderBrush).Color == Color.FromRgb(113, 144, 172),
            "An uninstalled game keeps its muted title and gets a separate selection accent");
        model.IsInstalled = true;
        Check(container.IsSelected && ((SolidColorBrush)title.Foreground).Color == Color.FromRgb(225, 230, 235),
            "An installed game uses the installed title color while selected");
        Check(star.FontSize == 16 && star.Visibility == Visibility.Visible && container.DesiredSize.Height == 26,
            "The larger favorite star fits the compact row");
        var outline = (Border)container.Template.FindName("ContextOutline", container);
        Check(outline.Visibility == Visibility.Collapsed, "A selected row starts without a context outline");
        model.IsContextTarget = true;
        Check(outline.Visibility == Visibility.Visible && container.IsSelected && !outline.IsHitTestVisible &&
            container.DesiredSize.Height == 26, "The contextual outline is independent of selection and row height");
        container.DataContext = new GameRow();
        Check(outline.Visibility == Visibility.Collapsed, "Recycling the container clears the old outline");
        container.DataContext = model;
        model.IsContextTarget = false;
        Check(outline.Visibility == Visibility.Collapsed && container.IsSelected, "Closing the menu clears only the outline");
        model.Favorite = false;
        Check(star.Visibility == Visibility.Collapsed, "The star follows the favorite state");
    }
    private static void NavigatorIconDimming(Grid root)
    {
        var list = (ListBox)root.FindName("Games");
        var serializer = new XmlSerializer(typeof(Preferences));
        var prefs = (Preferences)serializer.Deserialize(new StringReader("<Preferences />"));
        var installed = new GameRow { Name = "Installed", IsInstalled = true };
        var uninstalled = new GameRow { Name = "Uninstalled", Favorite = true };
        list.DataContext = prefs;
        list.ItemsSource = new[] { installed, uninstalled };
        var window = new Window { Content = root, Width = 320, Height = 300, ShowInTaskbar = false };
        window.Show();
        try {
            window.UpdateLayout();
            var installedRow = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(installed);
            var uninstalledRow = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(uninstalled);
            var installedIcon = Descendant<Image>(installedRow);
            var uninstalledIcon = Descendant<Image>(uninstalledRow);
            var title = Descendant<NavigatorGameTitle>(uninstalledRow);
            var foreground = title.Foreground;
            Check(!prefs.DimUninstalledGameIcons && installedIcon.Opacity == 1 && uninstalledIcon.Opacity == 1,
                "Older settings keep both icons at full opacity");
            prefs.BeginEdit();
            prefs.DimUninstalledGameIcons = true;
            list.SelectedItem = uninstalled;
            uninstalled.IsContextTarget = true;
            uninstalledIcon.Width = 24;
            Check(installedIcon.Opacity == 1 && uninstalledIcon.Opacity == 0.65 && uninstalledRow.Opacity == 1 &&
                title.Opacity == 1 && ReferenceEquals(title.Foreground, foreground) && uninstalledRow.IsSelected,
                "Only the uninstalled icon dims across icon widths, selection and context targeting");
            uninstalled.IsInstalled = true;
            Check(uninstalledIcon.Opacity == 1 && ReferenceEquals(uninstalledRow, list.ItemContainerGenerator.ContainerFromItem(uninstalled)),
                "An installation update restores opacity on the existing row");
            uninstalled.IsInstalled = false;
            Check(uninstalledIcon.Opacity == 0.65 && installedIcon.Opacity == 1,
                "An uninstall update dims only the affected icon");
            prefs.CancelEdit();
            Check(!prefs.DimUninstalledGameIcons && uninstalledIcon.Opacity == 1,
                "Cancel restores the previous preference and icon opacity");
            prefs.BeginEdit();
            prefs.DimUninstalledGameIcons = true;
            prefs.EndEdit();
            using (var writer = new StringWriter()) {
                serializer.Serialize(writer, prefs);
                var reloaded = (Preferences)serializer.Deserialize(new StringReader(writer.ToString()));
                Check(reloaded.DimUninstalledGameIcons, "Enabled icon dimming survives settings reload");
            }
        } finally {
            window.Content = null;
            window.Close();
            list.ItemsSource = null;
        }
    }
    private static void NavigatorTitleExpansion(Grid root)
    {
        var list = (ListBox)root.FindName("Games");
        var oldDataContext = list.DataContext;
        list.DataContext = new Preferences { DimUninstalledGameIcons = true };
        list.ItemsSource = new[] {
            new GameRow { Name = "Short" },
            new GameRow { Name = new string('W', 160), Favorite = true }
        }.Concat(Enumerable.Range(0, 30).Select(i => new GameRow { Name = "Other " + i })).ToArray();
        var host = new Grid();
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        host.ColumnDefinitions.Add(new ColumnDefinition());
        var neighbor = new Border { Background = Brushes.Black };
        Grid.SetColumn(neighbor, 1);
        host.Children.Add(root);
        host.Children.Add(neighbor);
        var decorator = new AdornerDecorator { Child = host };
        var window = new Window { Content = decorator, Width = 450, Height = 360, ShowInTaskbar = false };
        window.Show();
        try {
            window.UpdateLayout();
            var layer = AdornerLayer.GetAdornerLayer(decorator) ?? decorator.AdornerLayer;
            var shortRow = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
            var longRow = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1);
            var shortTitle = Descendant<NavigatorGameTitle>(shortRow);
            var longTitle = Descendant<NavigatorGameTitle>(longRow);
            var gameIcon = Descendant<Image>(longRow);
            gameIcon.Source = new DrawingImage(new GeometryDrawing(Brushes.Red, null,
                new RectangleGeometry(new Rect(0, 0, 20, 20))));
            window.UpdateLayout();
            var originalSize = longRow.RenderSize;
            var originalPosition = longRow.TranslatePoint(new Point(), list);
            shortTitle.ShowExpansion();
            Check(!NavigatorGameTitle.IsTruncated(shortTitle) && layer.GetAdorners(list) == null,
                "Fitting titles leave the row and adorner layer unchanged");

            longRow.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) {
                RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent
            });
            Check(layer.GetAdorners(list) == null, "Pointer entry does not immediately open the expansion");
            NavigatorGameTitle.Dismiss(list);
            longTitle.ShowExpansion();
            window.UpdateLayout();
            var expansion = layer.GetAdorners(list)?.SingleOrDefault();
            Check(expansion != null && !expansion.IsHitTestVisible,
                "Long titles expand on the outer layer and allow input through");
            var drawing = VisualTreeHelper.GetDrawing(expansion).Bounds;
            Check(Math.Abs(drawing.Left - longTitle.TranslatePoint(new Point(), list).X) < 1 &&
                Math.Abs(drawing.Top - originalPosition.Y) < 1 &&
                Math.Abs(drawing.Height - originalSize.Height) < 1 &&
                drawing.Width > originalSize.Width &&
                drawing.Right <= decorator.ActualWidth - list.TranslatePoint(new Point(), decorator).X + 1,
                "The expansion paints from the title, retains row height, and stops at the window edge");
            var surface = (FrameworkElement)VisualTreeHelper.GetParent(layer);
            var iconCenter = gameIcon.TranslatePoint(new Point(gameIcon.ActualWidth / 2, gameIcon.ActualHeight / 2), surface);
            var frame = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth),
                (int)Math.Ceiling(surface.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            frame.Render(surface);
            var pixel = new byte[4];
            frame.CopyPixels(new Int32Rect((int)iconCenter.X, (int)iconCenter.Y, 1, 1), pixel, 4, 0);
            Check(pixel[2] > 160 && pixel[2] < 200 && pixel[1] < 32 && pixel[0] < 32 && pixel[3] == 255 &&
                gameIcon.Opacity == 0.65 && NavigatorGameTitle.GetIsExpanded(longRow),
                "The rendered expansion leaves the original dimmed game icon visible");
            Check(longRow.RenderSize == originalSize && longRow.TranslatePoint(new Point(), list) == originalPosition &&
                window.InputHitTest(list.TranslatePoint(new Point(drawing.Right - 10, drawing.Top + 5), window)) == neighbor,
                "Showing the continuation leaves layout and neighboring hit targets intact");
            list.SelectedIndex = 0;
            Check(layer.GetAdorners(list) == null && !NavigatorGameTitle.GetIsExpanded(longRow),
                "A selection change dismisses the expansion and clears its row highlight");
            longTitle.ShowExpansion();
            var scroll = Descendant<ScrollViewer>(list);
            scroll.ScrollToVerticalOffset(1);
            window.UpdateLayout();
            Check(layer.GetAdorners(list) == null, "Scrolling removes the expansion");
            longTitle.ShowExpansion();
            list.ItemsSource = null;
            window.UpdateLayout();
            Check(layer.GetAdorners(list) == null, "Removing or recycling a row removes its expansion");
        } finally {
            host.Children.Remove(root);
            window.Close();
            list.ItemsSource = null;
            list.DataContext = oldDataContext;
        }
    }

    private static void ThemeGameMenu()
    {
        // Substitute only the Playnite-specific ContextMenu subclass so WPF can exercise the theme in isolation.
        using (var stream = typeof(NavigatorOrganizationTests).Assembly.GetManifestResourceStream("GameMenu.xaml"))
        using (var reader = new StreamReader(stream)) {
            var dictionary = (ResourceDictionary)XamlReader.Parse(reader.ReadToEnd().Replace("{x:Type GameMenu}", "{x:Type ContextMenu}"));
            var menu = new ContextMenu { Style = (Style)dictionary[typeof(ContextMenu)] };
            var submenu = new MenuItem { Header = "Synthetic submenu" };
            var icon = new TextBlock { Text = "+" };
            var command = new RecordingGameCommand { Allowed = false };
            var child = new MenuItem { Header = "Synthetic extension action", Icon = icon, InputGestureText = "F3",
                Command = command, CommandParameter = new Game() };
            var separator = new Separator();
            submenu.Items.Add(child);
            submenu.Items.Add(separator);
            menu.Items.Add(submenu);
            var anchor = new Border { ContextMenu = menu };
            var window = new Window { Content = anchor, Width = 380, Height = 300, ShowInTaskbar = false };
            window.Show();
            try {
                menu.PlacementTarget = anchor;
                menu.IsOpen = true;
                window.Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.Loaded);
                submenu.ApplyTemplate();
                var popup = submenu.Template.FindName("PART_Popup", submenu) as Popup;
                Check(menu.Padding == new Thickness(3) && submenu.MinHeight == 26 &&
                    popup?.Placement == PlacementMode.Right &&
                    ((UIElement)submenu.Template.FindName("SubmenuArrow", submenu)).Visibility == Visibility.Visible,
                    "Native game menu style sets density and keeps the submenu popup and arrow");
                submenu.IsSubmenuOpen = true;
                window.Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.Loaded);
                Check(popup.IsOpen, "Opening a submenu opens the WPF popup");
                child.ApplyTemplate();
                Check(child.Icon == icon && child.Command == command && child.InputGestureText == "F3" && !child.IsEnabled,
                    "Nested items retain command availability, icon and shortcut");
                Check(((Border)child.Template.FindName("MenuFace", child)).Opacity == 0.45,
                    "Nested disabled items use the scoped style");
                Check(separator.DesiredSize.Height == 9, "Nested separators use the scoped divider spacing");
            } finally {
                menu.IsOpen = false;
                window.Close();
            }
        }
    }
    private static void OrganizationControls()
    {
        PluginMenuActions();
        var glyph = new TextBlock { Text = "Synthetic icon", Foreground = Brushes.Yellow };
        var firstIcon = (TextBlock)NavigatorMenuIcons.Create(glyph);
        var secondIcon = (TextBlock)NavigatorMenuIcons.Create(glyph);
        Check(!ReferenceEquals(firstIcon, glyph) && !ReferenceEquals(firstIcon, secondIcon) &&
            firstIcon.FontSize == 16 && firstIcon.Width == 16 && firstIcon.Foreground == Brushes.Yellow,
            "Theme icons are independently parentable and preserve the theme accent");
        Check(NavigatorMenuIcons.Create(null) == null &&
            ((TextBlock)NavigatorMenuIcons.Create(new TextBlock())).ReadLocalValue(TextBlock.ForegroundProperty) ==
                DependencyProperty.UnsetValue, "Missing icons fall back to text and monochrome glyphs inherit menu foreground");
        var assembly = typeof(NavigatorOrganizationTests).Assembly;
        using (var stream = assembly.GetManifestResourceStream("Navigator.xaml"))
        using (var reader = new StreamReader(stream)) {
            var root = (Grid)XamlReader.Parse(reader.ReadToEnd().Replace("assembly=HeliumCharlo", "assembly=" + assembly.GetName().Name));
            NavigatorRowAppearance((ListBox)root.FindName("Games"));
            NavigatorIconDimming(root);
            NavigatorTitleExpansion(root);
            ThemeGameMenu();
            var context = new ContextMenu { Style = (Style)root.FindResource("NavigatorContextMenuStyle") };
            context.Resources.MergedDictionaries.Add(root.Resources);
            var item = new MenuItem { Header = "Synthetic submenu" };
            item.Items.Add(new MenuItem { Header = "Synthetic action" });
            context.Items.Add(item);
            item.Style = (Style)root.FindResource(typeof(MenuItem));
            item.Measure(new Size(300, 100)); item.ApplyTemplate();
            Check(item.Template.FindName("PART_Popup", item) is System.Windows.Controls.Primitives.Popup &&
                ((UIElement)item.Template.FindName("SubmenuArrow", item)).Visibility == Visibility.Visible,
                "The scoped menu retains the native submenu popup contract and disclosure");
            var filterPopup = (System.Windows.Controls.Primitives.Popup)root.FindName("FiltersPopup");
            var filterBorder = (Border)filterPopup.Child;
            var filterLayout = (Grid)filterBorder.Child;
            var filterScroll = (ScrollViewer)root.FindName("FilterSectionsScroll");
            var titleGrip = (System.Windows.Controls.Primitives.Thumb)root.FindName("FilterTitleGrip");
            var sizeGrip = (System.Windows.Controls.Primitives.Thumb)root.FindName("FilterSizeGrip");
            Check(!filterPopup.StaysOpen && !titleGrip.Focusable && !sizeGrip.Focusable && Grid.GetRow(sizeGrip) == 4,
                "Window gestures stay inside the popup without adding tab stops");
            Check(double.IsNaN(filterBorder.Height) && filterBorder.MaxHeight == 610 &&
                filterLayout.RowDefinitions.Count == 5 && filterLayout.RowDefinitions[3].Height.IsStar,
                "Filter popup gives metadata sections a bounded star row");
            Check(Grid.GetRow(filterScroll) == 3 && !filterScroll.CanContentScroll &&
                filterScroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto && filterScroll.Focusable &&
                Grid.GetRow((TextBox)root.FindName("FilterOptionSearch")) == 1 &&
                Grid.GetRow((ScrollViewer)root.FindName("ActiveFilterScroll")) == 2 &&
                Grid.GetRow((Button)root.FindName("ClearAllFilters")) == 4,
                "Filter header and search stay above a keyboard-focusable scroll area; Clear stays below");
            var filterSections = (FilterWorkspaceColumns)root.FindName("FilterSections");
            var field = new Expander { Header = "Synthetic field", Style = (Style)root.Resources["FilterFieldDisclosureStyle"],
                Content = new Border { Height = 600 } };
            var group = new Expander { Header = "Synthetic group", Style = (Style)root.Resources["FilterGroupDisclosureStyle"],
                Content = field };
            filterSections.Children.Add(group);
            filterBorder.MaxHeight = 460;
            var collapsed = MeasureFilterWorkspace(filterBorder);
            group.IsExpanded = true;
            var expanded = MeasureFilterWorkspace(filterBorder);
            field.IsExpanded = true;
            var bounded = MeasureFilterWorkspace(filterBorder);
            Check(group.FontSize > field.FontSize && group.FontWeight == FontWeights.SemiBold &&
                !Equals(group.Foreground, field.Foreground) && group.Template == field.Template,
                "Groups emphasize the same disclosure affordance with typography and color");
            Check(collapsed < expanded && expanded < bounded && Math.Abs(bounded - filterBorder.MaxHeight) < 1,
                "Groups and fields grow the popup only until its maximum height");
            Check(filterScroll.ScrollableHeight > 0 && filterScroll.ViewportHeight > 0,
                "Expanded metadata sections exceed the finite scroll viewport");
            filterScroll.ScrollToBottom(); filterBorder.UpdateLayout();
            var clear = (Button)root.FindName("ClearAllFilters");
            Check(filterScroll.VerticalOffset > 0 && clear.TranslatePoint(new Point(0, clear.ActualHeight), filterBorder).Y <= bounded + 1,
                "Metadata scroll reaches lower sections while the footer fits");
            group.IsExpanded = false;
            Check(Math.Abs(MeasureFilterWorkspace(filterBorder) - collapsed) < 1 && filterScroll.ScrollableHeight == 0,
                "Collapsing content restores compact height without a scrollbar");
            foreach (var grouping in new[] { true, false }) {
                var combo = (ComboBox)root.FindName(grouping ? "GroupOptions" : "SortOptions");
                int selected = -1;
                NavigatorOrganizationControls.ConfigureSelector(combo, mode => selected = mode.Id);
                NavigatorOrganizationControls.Choices(combo, new[] { 0, 1 }, grouping, 4);
                var items = (ListCollectionView)combo.ItemsSource;
                Check(!combo.IsEditable && combo.GroupStyle.Count == 1 && items.Groups.Count >= 2, "Non-editable categorized selector");
                Check(selected == 4 && combo.Items.Count == 3, "Hidden active choice stays selectable");
                combo.SelectedItem = combo.Items.Cast<OrganizationMode>().Single(mode => mode.Id == 1);
                Check(selected == 1, "Choosing a mode reports its persisted ID");
            }
        }

        var prefs = new Preferences();
        prefs.MigrateNavigation();
        var settings = new NavigatorSettingsView { DataContext = prefs };
        var tabs = (TabControl)settings.Content;
        Check(tabs.Items.Cast<TabItem>().Select(tab => tab.Header.ToString())
            .SequenceEqual(new[] { "Navigation", "Appearance", "Organization", "Filters" }), "Settings tab structure");
        var filters = (StackPanel)((ScrollViewer)((TabItem)tabs.Items[3]).Content).Content;
        var automaticSize = filters.Children.OfType<Button>().Single();
        Check(automaticSize.Visibility == Visibility.Collapsed, "Automatic layout needs no reset control");
        var sized = new Preferences { FilterPopupWidth = 760, FilterPopupHeight = 500 };
        settings.DataContext = sized;
        sized.BeginEdit();
        Check(automaticSize.Visibility == Visibility.Visible, "Manual size can be reset in Filters settings");
        automaticSize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!sized.HasFilterPopupSize, "Reset selects automatic sizing");
        sized.CancelEdit();
        Check(sized.HasFilterPopupSize, "Cancel restores the original manual size");
        settings.DataContext = prefs;
        tabs.SelectedIndex = 2;
        var page = (StackPanel)((ScrollViewer)((TabItem)tabs.SelectedItem).Content).Content;
        var style = (ListBox)((GroupBox)page.Children[0]).Content;
        var binding = BindingOperations.GetBinding(style, ListBox.SelectedIndexProperty);
        Check(binding.Path.Path == nameof(Preferences.RecentActivityGroupingStyle) && binding.Mode == BindingMode.TwoWay,
            "Activity selection is bound to the existing persisted setting");
        Check(style.Items.Cast<ListBoxItem>().Select(item => ((TextBlock)((StackPanel)item.Content).Children[0]).Text)
            .SequenceEqual(new[] { "Hybrid", "Detailed" }), "Activity cards preserve the stored index order");
        var nativeCheck = (CheckBox)page.Children[1];
        Check(BindingOperations.GetBinding(nativeCheck, CheckBox.IsCheckedProperty).Path.Path ==
            nameof(Preferences.ApplySortGroupToPlaynite), "Native organization setting bound in Organization tab");
        var editors = ((Grid)page.Children[2]).Children.Cast<GroupBox>().ToArray();
        Check(editors.Length == 2, "Organization has separate visibility cards");
        var layout = (Grid)page.Children[2];
        NavigatorOrganizationControls.ArrangeEditors(layout, 700);
        Check(layout.ColumnDefinitions.Count == 2 && Grid.GetColumn(editors[1]) == 1, "Wide editors use balanced columns");
        NavigatorOrganizationControls.ArrangeEditors(layout, 420);
        Check(layout.ColumnDefinitions.Count == 1 && Grid.GetRow(editors[1]) == 1, "Narrow editors stack without reconstruction");
        NavigatorOrganizationControls.ArrangeEditors(layout, 700);
        Check(ReferenceEquals(layout.Children[0], editors[0]), "Resize retains editor state");
        layout.Children.Clear();
        layout.Children.Add(NavigatorOrganizationControls.VisibilityEditor(prefs, true));
        layout.Children.Add(NavigatorOrganizationControls.VisibilityEditor(prefs, false));
        NavigatorOrganizationControls.ArrangeEditors(layout, 700);
        Check(Grid.GetColumn(layout.Children[1]) == 1, "Repopulated editors receive existing column layout");
        NavigatorOrganizationControls.ArrangeEditors(layout, 420);
        layout.Children.Clear();
        layout.Children.Add(editors[0]); layout.Children.Add(editors[1]);
        NavigatorOrganizationControls.ArrangeEditors(layout, 420);
        Check(Grid.GetRow(layout.Children[1]) == 1, "Reopened narrow editors do not overlap");
        Check(style.Items.Cast<ListBoxItem>().All(card => double.IsNaN(card.Width) && card.MinHeight == 68), "Equal activity cards stretch to available width");
        Check(style.Items.Cast<ListBoxItem>().All(card => !((TextBlock)((StackPanel)card.Content).Children[1]).Text.EndsWith(".")), "Short card descriptions omit terminal periods");
        foreach (var editor in editors) {
            var panel = (StackPanel)editor.Content;
            var list = panel.Children.OfType<ListBox>().Single();
            Check(list.Height == 210, "Visibility lists have bounded height");
            var actions = panel.Children.OfType<WrapPanel>().Single().Children.OfType<Button>().ToArray();
            Check(actions.Length == 4,
                "Each visibility list owns its customization actions");
            list.SelectedIndex = 0;
            Check(!actions[2].IsEnabled && actions[3].IsEnabled, "First visible mode can only move down");
            list.SelectedIndex = -1;
            Check(!actions[2].IsEnabled && !actions[3].IsEnabled, "Reordering requires a selected mode");
            bool grouping = editor == editors[0];
            Func<List<int>> visible = () => grouping ? prefs.VisibleGroupingModes : prefs.VisibleSortingModes;
            var before = visible().ToArray();
            var search = ((Grid)panel.Children[0]).Children.OfType<TextBox>().Single();
            search.Text = grouping ? "Series" : "Playtime";
            Check(list.Items.Count == 1 && visible().SequenceEqual(before), "Search does not change visibility");
            search.Text = "";
            list.SelectedIndex = 1;
            int moved = ((OrganizationVisibilityOption)list.SelectedItem).Id;
            actions[2].RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Check(visible()[0] == moved, "Move Up preserves the selected option identity");
            actions[3].RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Check(visible().SequenceEqual(before), "Move Down restores the previous sequence");
            var other = (grouping ? prefs.VisibleSortingModes : prefs.VisibleGroupingModes).ToArray();
            actions[0].RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Check(visible().Count == (grouping ? 20 : 16), "Show All affects the complete catalog");
            actions[1].RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Check(visible().SequenceEqual(NavigatorOrganization.Normalize(null, grouping)), "Restore Defaults restores only its list");
            Check((grouping ? prefs.VisibleSortingModes : prefs.VisibleGroupingModes).SequenceEqual(other), "Management actions preserve the other panel");
        }
    }

    private static void PluginMenuActions()
    {
        var game = new Game("Synthetic menu game") { Id = Guid.NewGuid() };
        var fresh = game.GetCopy();
        GameMenuItemActionArgs result = null;
        GetGameMenuItemsArgs discovery = null;
        var contribution = new GameMenuItem { Description = "Test action", MenuSection = "Example|Nested", Action = args => result = args };
        var provider = new MenuProvider(args => {
            discovery = args;
            return new[] { contribution, contribution };
        });
        var broken = new MenuProvider(_ => throw new InvalidOperationException());
        var loaded = new List<Plugin> { broken, provider };
        var errors = 0;
        var menu = new MenuItem();
        void Populate() => NavigatorPluginMenu.Populate(menu, new Grid(), game,
            () => loaded, id => id == fresh.Id ? fresh : null, _ => errors++);
        Populate();
        var section = (MenuItem)menu.Items[0];
        var nested = (MenuItem)section.Items[0];
        var item = (MenuItem)nested.Items[0];
        Check(errors == 1 && menu.Visibility == Visibility.Visible && nested.Items.Count == 1 &&
            discovery.Games.SequenceEqual(new[] { game }) && !discovery.IsGlobalSearchRequest,
            "Contribution discovery uses the target and isolates a failing provider");
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(result?.SourceItem == contribution && result.Games.SequenceEqual(new[] { fresh }),
            "A contribution receives its source item and the fresh target");
        loaded.Clear();
        result = null;
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Populate();
        Check(result == null && menu.Visibility == Visibility.Collapsed,
            "An unloaded extension cannot execute and an empty menu stays hidden");
    }
    private static void CatalogAndGroups()
    {
        Check(NavigatorOrganization.Groups.Select(m => m.Id).Distinct().Count() == 20, "Unique persisted grouping IDs");
        Check(NavigatorOrganization.Sorts.Select(m => m.Id).Distinct().Count() == 16, "Unique sorting IDs");
        Check(NavigatorOrganization.Groups.Take(5).Select(m => m.Label).SequenceEqual(new[] { "Following Playnite", "Series", "Category", "Platform", "Recent Activity" }), "Legacy IDs remain unchanged");
        Check(NavigatorRefreshDecision.EffectiveGrouping(5, "Series") == 0 && NavigatorRefreshDecision.EffectiveGrouping(15, "Series") == 4,
            "Persisted No Grouping and Completion Status do not collide with effective IDs");
        var missing = Game(1); var realNone = Game(2); var realOld = Game(3); var realUnknown = Game(4); var multi = Game(5);
        realNone.Groups = new[] { new NavigatorGroupIdentity("id:none", "None") };
        realOld.Groups = new[] { new NavigatorGroupIdentity("id:old", "No group") };
        realUnknown.Groups = new[] { new NavigatorGroupIdentity("id:unknown", "Unknown") };
        multi.Groups = new[] { new NavigatorGroupIdentity("id:a", "A"), new NavigatorGroupIdentity("id:b", "B"), new NavigatorGroupIdentity("id:a", "A") };
        var games = new[] { missing, realNone, realOld, realUnknown, multi };
        foreach (var mode in NavigatorOrganization.Groups.Where(m => m.GroupPolicy != NavigatorGroupPolicy.None &&
            m.GroupPolicy != NavigatorGroupPolicy.RecentHybrid && m.GroupPolicy != NavigatorGroupPolicy.InstallationStatus &&
            m.GroupPolicy != NavigatorGroupPolicy.Favorites && m.GroupPolicy != NavigatorGroupPolicy.ReleaseYear && m.GroupPolicy != NavigatorGroupPolicy.AddedYear)) {
            foreach (var descending in new[] { false, true }) {
                var rows = Build(games, (int)mode.GroupPolicy, NavigatorSortMode.Name, descending);
                Check(rows.Count == 6 && rows.Last().Id == missing.Id && rows.Last().Group == "None", mode.Label + " missing last, both directions");
                Check(rows.Count(r => r.Id == multi.Id) == 2 && rows.Select(r => r.Id).Distinct().Count() == 5, mode.Label + " multiple occurrences and distinct count");
                Check(!rows.First(r => r.Id == realNone.Id).GroupIdentity.Equals(rows.Last().GroupIdentity), mode.Label + " real None identity distinct");
                Check(rows.Where(r => r.Id == realOld.Id || r.Id == realUnknown.Id).All(r => !r.GroupIdentity.IsUnassigned), mode.Label + " real No group/Unknown assigned");
            }
        }
        var projection = Build(games, 1);
        var view = new ListCollectionView(projection);
        // WPF binding sees public presentation properties, exactly as the real Navigator does.
        var presentation = projection.Select(r => new GameRow { Id = r.Id, GroupIdentity = r.GroupIdentity }).ToList();
        view = new ListCollectionView(presentation);
        view.GroupDescriptions.Add(new PropertyGroupDescription("GroupIdentity"));
        Check(view.Groups.Count == 6 && view.Groups.Cast<CollectionViewGroup>().Sum(g => g.ItemCount) == 6, "WPF retains colliding labels and correct counts");
        Check(view.Groups.Cast<CollectionViewGroup>().Count(g => g.Name.ToString() == "None") == 2, "WPF separates real and unassigned None");
        var state = new NavigatorGroupState();
        var assigned = projection.First(r => r.Id == realNone.Id).GroupIdentity;
        var unassigned = projection.Last().GroupIdentity;
        state.SetExpanded(1, assigned, false);
        Check(!state.IsExpanded(1, assigned) && state.IsExpanded(1, unassigned), "Collapse identity isolation");
        state.SetExpanded(1, unassigned, false); state.SetExpanded(1, assigned, true);
        Check(state.IsExpanded(1, assigned) && !state.IsExpanded(1, unassigned), "Reveal only desired occurrence group");
        state.SetExpanded(2, "B", false);
        Check(!state.IsExpanded(2, new NavigatorGroupIdentity("id:b", "B")), "Legacy label state retained");
        var before = Build(new[] { missing }, 1);
        missing.Groups = new[] { new NavigatorGroupIdentity("new", "None") };
        Check(NavigatorRefreshDecision.ClassifyRows(before, Build(new[] { missing }, 1)) == NavigatorRowChange.Structural, "Missing to real None rebuilds despite same label");
        var older = Game(6); older.ReleaseDate = new DateTime(2000, 1, 1); older.Added = older.ReleaseDate;
        var newer = Game(7); newer.ReleaseDate = new DateTime(2026, 1, 1); newer.Added = newer.ReleaseDate;
        foreach (var policy in new[] { NavigatorGroupPolicy.ReleaseYear, NavigatorGroupPolicy.AddedYear })
            foreach (var descending in new[] { false, true })
                Check(Build(new[] { older, missing, newer }, (int)policy, NavigatorSortMode.PlayCount, descending).Select(r => r.Id).SequenceEqual(new[] { newer.Id, older.Id, missing.Id }), "Years newest first, unknown last, independent of game direction");
        older.IsInstalled = true; newer.Favorite = true;
        Check(Build(new[] { older, newer }, (int)NavigatorGroupPolicy.InstallationStatus).Select(r => r.Group).SequenceEqual(new[] { "Installed", "Not Installed" }), "Installation grouping");
        Check(Build(new[] { older, newer }, (int)NavigatorGroupPolicy.Favorites).Select(r => r.Group).SequenceEqual(new[] { "Favorites", "Not Favorites" }), "Favorite grouping");
        Check(Build(games, NavigatorRefreshDecision.EffectiveGrouping(5, "Series")).All(r => r.Group == ""), "No Grouping remains flat");
        foreach (var policy in new[] { 8, 9 }) {
            older.LastActivity = Now.AddYears(-1); newer.LastActivity = Now.AddDays(-1);
            var recent = Build(new[] { older, newer, missing }, policy);
            Check(recent.Count == 3 && recent[0].Id == newer.Id && recent.Last().Group == "No Recorded Activity", "Recent chronological invariants");
        }
        var activity = new NavigatorGroupIdentity("activity:1", "Activity");
        state.SetExpanded(8, activity, false);
        Check(state.IsExpanded(9, activity), "Hybrid/Detailed expansion isolated");
    }
    private static void Sorting()
    {
        foreach (var mode in NavigatorOrganization.Sorts.Where(m => m.Id != 0)) {
            var low = Game(1, "A"); var high = Game(2, "B"); var missing = Game(3, null);
            low.SortingName = "A"; high.SortingName = "B";
            low.LastActivity = Now.AddDays(-2); high.LastActivity = Now.AddDays(-1);
            low.ReleaseDate = low.Added = low.Modified = Now.AddYears(-2);
            high.ReleaseDate = high.Added = high.Modified = Now.AddYears(-1);
            low.Playtime = low.PlayCount = 0; high.Playtime = high.PlayCount = 1;
            low.UserScore = low.CommunityScore = low.CriticScore = 0; high.UserScore = high.CommunityScore = high.CriticScore = 100;
            low.InstallSize = 0; high.InstallSize = 10;
            low.CompletionStatus = "Custom A"; high.CompletionStatus = "Custom Z";
            high.IsInstalled = high.Favorite = true;
            var pair = new[] { high, low };
            Check(Build(pair, 0, mode.SortPolicy).Select(r => r.Id).SequenceEqual(new[] { low.Id, high.Id }), mode.Label + " ascending");
            Check(Build(pair, 0, mode.SortPolicy, true).Select(r => r.Id).SequenceEqual(new[] { high.Id, low.Id }), mode.Label + " descending");
            bool nullable = mode.SortPolicy != NavigatorSortMode.Playtime && mode.SortPolicy != NavigatorSortMode.PlayCount && mode.SortPolicy != NavigatorSortMode.FavoriteStatus && mode.SortPolicy != NavigatorSortMode.InstallationStatus;
            if (nullable) foreach (bool desc in new[] { false, true })
                Check(Build(new[] { missing, low, high }, 0, mode.SortPolicy, desc).Last().Id == missing.Id, mode.Label + " missing last; zero present");
            var prior = Build(new[] { low }, 0, mode.SortPolicy);
            low.Added = high.Added; low.Modified = high.Modified; low.PlayCount = 2; low.Playtime = 2;
            low.ReleaseDate = high.ReleaseDate; low.LastActivity = high.LastActivity;
            low.UserScore = low.CommunityScore = low.CriticScore = 50; low.InstallSize = 5;
            low.Favorite = low.IsInstalled = true; low.CompletionStatus = "Custom X"; low.SortingName = "Z";
            Check(NavigatorRefreshDecision.ClassifyRows(prior, Build(new[] { low }, 0, mode.SortPolicy)) == NavigatorRowChange.Structural, mode.Label + " changed sort key rebuilds");
        }
        var first = Game(1); var second = Game(2);
        foreach (var mode in NavigatorOrganization.Sorts.Where(m => m.Id != 0))
            Check(Build(new[] { second, first }, 0, mode.SortPolicy, true).First().Id == first.Id, mode.Label + " deterministic tie by ID");
        Check(Build(new[] { second, first }, 0, NavigatorSortMode.FollowingPlaynite, true).First().Id == second.Id, "Native sequence preserved");
        var visualBefore = Build(new[] { first }, 1); first.Favorite = true;
        Check(NavigatorRefreshDecision.ClassifyRows(visualBefore, Build(new[] { first }, 1)) == NavigatorRowChange.Visual, "Unrelated sort keeps favorite update visual");
    }
    private static void Settings()
    {
        var serializer = new XmlSerializer(typeof(Preferences));
        for (int id = 0; id <= 4; id++) {
            var prefs = (Preferences)serializer.Deserialize(new StringReader("<Preferences><Grouping>" + id + "</Grouping><SortMode>3</SortMode><Width>270</Width><SidebarClickAction>0</SidebarClickAction></Preferences>"));
            prefs.MigrateNavigation();
            Check(prefs.Grouping == id && prefs.SortMode == 3 && prefs.Width == 270 && prefs.SidebarClickAction == 0, "Legacy navigation and mode preserved " + id);
            Check(prefs.RecentActivityGroupingStyle == (id == 4 ? 1 : 0), "Legacy activity style " + id);
            Check(prefs.VisibleGroupingModes.Count > 1 && prefs.VisibleSortingModes.Count > 1, "Older file receives catalog visibility defaults");
            Check(prefs.ApplySortGroupToPlaynite, "Older settings default to native organization synchronization");
        }
        var p = new Preferences { Grouping = 15, SortMode = 8 }; p.MigrateNavigation();
        p.BeginEdit(); p.ApplySortGroupToPlaynite = false; p.CancelEdit();
        Check(p.ApplySortGroupToPlaynite, "Cancel restores native synchronization preference");
        p.BeginEdit(); p.ApplySortGroupToPlaynite = false; p.EndEdit();
        Check(!p.ApplySortGroupToPlaynite, "Save keeps native synchronization disabled");
        var original = p.VisibleGroupingModes.ToArray(); var originalSort = p.VisibleSortingModes.ToArray();
        p.BeginEdit(); p.VisibleGroupingModes.Clear(); p.VisibleSortingModes.Clear(); p.Width = 300;
        p.CancelEdit();
        Check(p.VisibleGroupingModes.SequenceEqual(original) && p.VisibleSortingModes.SequenceEqual(originalSort) && p.Width == 230, "CancelEdit restores deep copied lists and scalar values");
        int saves = 0; p.Save = () => saves++;
        p.BeginEdit(); p.VisibleGroupingModes = new List<int> { 1 }; p.VisibleSortingModes = new List<int> { 1 };
        p.EndEdit();
        Check(p.Grouping == 15 && p.SortMode == 8 && saves == 1, "Hide active mode does not change it; EndEdit saves");
        Check(NavigatorOrganization.Visible(p.VisibleGroupingModes, true, p.Grouping).Any(m => m.Id == 15), "Hidden active group available");
        Check(NavigatorOrganization.Visible(p.VisibleSortingModes, false, p.SortMode).Any(m => m.Id == 8), "Hidden active sort available");
        using (var writer = new StringWriter()) {
            serializer.Serialize(writer, p);
            var copy = (Preferences)serializer.Deserialize(new StringReader(writer.ToString())); copy.MigrateNavigation();
            Check(copy.Grouping == 15 && copy.SortMode == 8 && copy.VisibleGroupingModes.SequenceEqual(new[] { 1 }) && copy.VisibleSortingModes.SequenceEqual(new[] { 1 }), "New settings roundtrip");
            Check(!copy.ApplySortGroupToPlaynite, "Native synchronization preference persists");
        }
        p.BeginEdit(); p.VisibleGroupingModes.Clear(); p.VisibleSortingModes = new List<int> { -1, 999 }; p.EndEdit();
        Check(p.VisibleGroupingModes.SequenceEqual(new[] { 0 }) && p.VisibleSortingModes.SequenceEqual(new[] { 0 }), "At least one valid selectable option");
        p.VisibleGroupingModes = new List<int> { 1 };
        var option = new OrganizationVisibilityOption(NavigatorOrganization.Group(1), () => p.VisibleGroupingModes, ids => p.VisibleGroupingModes = ids);
        option.Visible = false;
        Check(option.Visible, "Cannot hide last checkbox");
        Check(Logic.ToggleRecentGrouping(4, 15) == 15, "Recent shortcut restores new grouping modes");
        Check(NavigatorOrganization.Move(new[] { 1, 2, 3 }, 2, -1).SequenceEqual(new[] { 2, 1, 3 }), "Move visible option up");
        Check(NavigatorOrganization.Move(new[] { 1, 2, 3 }, 2, 1).SequenceEqual(new[] { 1, 3, 2 }), "Move visible option down");
        Check(NavigatorOrganization.Move(new[] { 1, 2, 3 }, 9, -1).SequenceEqual(new[] { 1, 2, 3 }), "Cannot reorder a hidden option");
    }
    private static void RemovedDefaultModes()
    {
        var serializer = new XmlSerializer(typeof(Preferences));
        Preferences Read(string xml)
        {
            var p = (Preferences)serializer.Deserialize(new StringReader(xml));
            p.MigrateNavigation();
            return p;
        }

        const string candidateLists =
            "<DefaultOrganizationModesInitialized>true</DefaultOrganizationModesInitialized>" +
            "<VisibleGroupingModes><int>2</int><int>20</int><int>5</int><int>1</int></VisibleGroupingModes>" +
            "<VisibleSortingModes><int>2</int><int>16</int><int>1</int><int>0</int></VisibleSortingModes>";
        var migrated = Read("<Preferences><Grouping>20</Grouping><SortMode>16</SortMode><SortDescending>true</SortDescending>" +
            candidateLists + "</Preferences>");
        Check(migrated.Grouping == (int)NavigatorGroupingMode.None &&
            migrated.SortMode == (int)NavigatorSortMode.Name && !migrated.SortDescending,
            "Candidate defaults migrate to No Grouping and Name ascending");
        Check(migrated.VisibleGroupingModes.SequenceEqual(new[] { 2, 5, 1 }) &&
            migrated.VisibleSortingModes.SequenceEqual(new[] { 2, 1, 0 }), "Removed IDs are dropped without reordering the remaining modes");
        using (var writer = new StringWriter()) {
            serializer.Serialize(writer, migrated);
            var copy = Read(writer.ToString());
            Check(copy.Grouping == migrated.Grouping && copy.SortMode == migrated.SortMode && !copy.SortDescending &&
                copy.VisibleGroupingModes.SequenceEqual(migrated.VisibleGroupingModes) &&
                copy.VisibleSortingModes.SequenceEqual(migrated.VisibleSortingModes), "Migrated organization survives saving and reopening");
        }

        var existing = Read("<Preferences><Grouping>2</Grouping><SortMode>1</SortMode><SortDescending>true</SortDescending>" +
            candidateLists + "</Preferences>");
        Check(existing.Grouping == 2 && existing.SortMode == (int)NavigatorSortMode.Name && existing.SortDescending,
            "An existing Name descending preference is unaffected by obsolete visibility entries");
        var onlyDefaults = Read("<Preferences><Grouping>20</Grouping><SortMode>16</SortMode>" +
            "<VisibleGroupingModes><int>20</int></VisibleGroupingModes>" +
            "<VisibleSortingModes><int>16</int></VisibleSortingModes></Preferences>");
        Check(NavigatorOrganization.Visible(onlyDefaults.VisibleGroupingModes, true, onlyDefaults.Grouping)
                .Select(mode => mode.Id).SequenceEqual(new[] { 0, (int)NavigatorGroupingMode.None }) &&
            NavigatorOrganization.Visible(onlyDefaults.VisibleSortingModes, false, onlyDefaults.SortMode)
                .Select(mode => mode.Id).SequenceEqual(new[] { 0, (int)NavigatorSortMode.Name }),
            "Lists containing only removed IDs keep the safe fallback and the migrated active mode");
    }
    private static void NativeOrganization()
    {
        Check(NavigatorNativeOrganization.Group(1) == GroupableField.Series &&
            NavigatorNativeOrganization.Group(2) == GroupableField.Category &&
            NavigatorNativeOrganization.Group(3) == GroupableField.Platform &&
            NavigatorNativeOrganization.Group(5) == GroupableField.None, "Native core grouping mappings");
        Check(NavigatorNativeOrganization.Group(6) == GroupableField.Genre &&
            NavigatorNativeOrganization.Group(7) == GroupableField.Tag &&
            NavigatorNativeOrganization.Group(15) == GroupableField.CompletionStatus &&
            NavigatorNativeOrganization.Group(16) == GroupableField.InstallationStatus &&
            NavigatorNativeOrganization.Group(18) == GroupableField.ReleaseYear, "Native metadata and status grouping mappings");
        Check(!NavigatorNativeOrganization.Group(0).HasValue &&
            !NavigatorNativeOrganization.Group(4).HasValue &&
            !NavigatorNativeOrganization.Group(17).HasValue &&
            !NavigatorNativeOrganization.Group(19).HasValue, "Following Playnite and custom groups leave native grouping alone");
        Check(NavigatorNativeOrganization.Sort(NavigatorSortMode.Name) == SortOrder.Name &&
            NavigatorNativeOrganization.Sort(NavigatorSortMode.LastPlayed) == SortOrder.LastActivity &&
            NavigatorNativeOrganization.Sort(NavigatorSortMode.PlayCount) == SortOrder.PlayCount &&
            NavigatorNativeOrganization.Sort(NavigatorSortMode.InstallationStatus) == SortOrder.IsInstalled,
            "Native sorting mappings");
        Check(!NavigatorNativeOrganization.Sort(NavigatorSortMode.FollowingPlaynite).HasValue &&
            !NavigatorNativeOrganization.Sort(NavigatorSortMode.SortingName).HasValue,
            "Following Playnite and distinct Sorting Name do not force native sort");
        Check(NavigatorNativeOrganization.Direction(false) == SortOrderDirection.Ascending &&
            NavigatorNativeOrganization.Direction(true) == SortOrderDirection.Descending, "Native direction mapping");
        var filters = new FilterPresetSettings { UseAndFilteringStyle = true, Name = "synthetic",
            Genre = new IdItemFilterItemProperties { Ids = new List<Guid> { Guid.NewGuid() }, Text = "retained" } };
        var preset = NavigatorNativeOrganization.SortPreset(filters, SortOrder.Name, SortOrderDirection.Descending, GroupableField.Series);
        Check(preset.Id == Guid.Empty && preset.SortingOrder == SortOrder.Name &&
            preset.SortingOrderDirection == SortOrderDirection.Descending && preset.GroupingOrder == GroupableField.Series,
            "Native sort preset changes only requested view fields");
        Check(preset.Settings.UseAndFilteringStyle && preset.Settings.Name == "synthetic" &&
            preset.Settings.Genre.Text == "retained" && preset.Settings.Genre.Ids.SequenceEqual(filters.Genre.Ids),
            "Native sort preset copies filter conditions");
        preset.Settings.Genre.Ids.Clear();
        Check(filters.Genre.Ids.Count == 1, "Native sort preset cannot mutate source filters");
    }
    private static void FilterWorkspace()
    {
        var workArea = new Rect(-1000, -20, 1000, 650);
        Check(NavigatorFilterPanel.KeepOnScreen(new Point(50, 400), workArea, new Size(500, 450)) == new Point(-500, 180) &&
            NavigatorFilterPanel.KeepOnScreen(new Point(-1500, -50), workArea, new Size(500, 450)) == new Point(-1000, -20),
            "Popup placement respects the work area including negative monitor coordinates");
        var sizeSerializer = new XmlSerializer(typeof(Preferences));
        using (var writer = new StringWriter()) {
            var manual = new Preferences { FilterPopupWidth = 560, FilterPopupHeight = 480, FilterPopupX = -42, FilterPopupY = 80 };
            sizeSerializer.Serialize(writer, manual);
            manual = (Preferences)sizeSerializer.Deserialize(new StringReader(writer.ToString()));
            manual.MigrateNavigation();
            Check(manual.HasFilterPopupSize && manual.FilterPopupWidth == 560 && manual.FilterPopupHeight == 480,
                "Chosen width and height survive XML reload");
            Check(manual.HasFilterPopupPosition && manual.FilterPopupX == -42 && manual.FilterPopupY == 80,
                "The popup anchor survives settings serialization");
            manual.FilterPopupX = double.PositiveInfinity;
            manual.FilterPopupHeight = double.NaN;
            manual.MigrateNavigation();
            Check(manual.FilterPopupWidth == 0 && manual.FilterPopupHeight == 0, "Invalid saved size restores automatic sizing");
            Check(!manual.HasFilterPopupPosition && manual.FilterPopupX == null && manual.FilterPopupY == null,
                "An invalid saved anchor restores automatic placement");
        }
        Check(FilterWorkspaceCatalog.Groups.Select(group => group.Id).SequenceEqual(
                new[] { "ownership", "classification", "identity", "activity", "ratings" }) &&
            FilterWorkspaceCatalog.Groups.Select(group => group.Label).SequenceEqual(
                new[] { "Library & Status", "Classification", "Game Information", "Activity & History", "Ratings" }),
            "Shared filter headings retain their persisted group IDs and order");
        var preferences = new Preferences();
        preferences.MigrateNavigation();
        Check(preferences.FilterWorkspace.Select(group => group.Id).SequenceEqual(FilterWorkspaceCatalog.Groups.Select(group => group.Id)) &&
            preferences.FilterWorkspace.All(group => !group.Hidden && group.HiddenFields.Count == 0), "Old settings show all native fields in default order");
        var first = preferences.FilterWorkspace[0];
        preferences.BeginEdit();
        first.Hidden = true;
        first.HiddenFields.Add("Platform");
        preferences.CancelEdit();
        Check(!preferences.FilterWorkspace[0].Hidden && preferences.FilterWorkspace[0].HiddenFields.Count == 0,
            "Cancel restores a deep copy of workspace preferences");
        preferences.BeginEdit();
        first = preferences.FilterWorkspace[0];
        first.FieldOrder.Insert(1, "future-field");
        first.HiddenFields.Add("future-field");
        FilterWorkspaceSettings.Move(first.FieldOrder, first.FieldOrder.Where(id => id != "future-field").ToList(), "Platform", 1);
        FilterWorkspaceSettings.Move(preferences.FilterWorkspace, preferences.FilterWorkspace.ToList(), first, 1);
        first.HiddenFields.Add("Platform");
        preferences.EndEdit();
        var serializer = new XmlSerializer(typeof(Preferences));
        using (var writer = new StringWriter()) {
            serializer.Serialize(writer, preferences);
            preferences = (Preferences)serializer.Deserialize(new StringReader(writer.ToString()));
            preferences.MigrateNavigation();
        }
        first = preferences.FilterWorkspace[1];
        Check(first.Id == "ownership" && first.FieldOrder.Take(3).SequenceEqual(new[] { "Library", "future-field", "Platform" }) &&
            first.HiddenFields.SequenceEqual(new[] { "future-field", "Platform" }), "Save/reload preserves order, hidden fields and unknown IDs");
        Check(FilterWorkspaceCatalog.Columns(790) == 3 && FilterWorkspaceCatalog.Columns(650) == 2 &&
            FilterWorkspaceCatalog.Columns(590) == 1, "Workspace width chooses three, two or one column");

        var columns = new FilterWorkspaceColumns();
        foreach (var height in new[] { 120, 30, 40, 25, 20 })
            columns.Children.Add(new Border { Height = height });
        foreach (var width in new[] { 790d, 650d, 590d }) {
            columns.Measure(new Size(width, double.PositiveInfinity));
            columns.Arrange(new Rect(columns.DesiredSize));
            var count = FilterWorkspaceCatalog.Columns(width);
            var last = (Border)columns.Children[4];
            var expectedY = count == 3 ? 30 : count == 2 ? 160 : 215;
            Check(last.TranslatePoint(new Point(), columns) == new Point((4 % count) * width / count, expectedY),
                "Columns preserve group order and stack at each column's own height");
        }
        columns.SetWorkspaceWidth(725);
        columns.Measure(new Size(708, double.PositiveInfinity));
        columns.Arrange(new Rect(columns.DesiredSize));
        Check(columns.Children[4].TranslatePoint(new Point(), columns).Y == 30,
            "An appearing scrollbar does not move groups between columns");
        var host = new FilterWorkspaceColumns();
        var chips = new WrapPanel();
        var search = new TextBox();
        var empty = new TextBlock();
        var platform = Guid.NewGuid();
        var other = Guid.NewGuid();
        var state = new FilterPresetSettings { Hidden = true, UseAndFilteringStyle = true,
            Platform = new IdItemFilterItemProperties { Ids = new List<Guid> { platform, other } } };
        NavigatorFilterPanel workspace = null;
        workspace = new NavigatorFilterPanel(preferences, new Border(), host, chips, search, empty,
            field => field.Id == "Platform" ? new[] { new FilterWorkspaceChoice(platform, "Test PC"),
                new FilterWorkspaceChoice(other, "Test Console") } : new FilterWorkspaceChoice[0],
            edit => { state = edit(state); workspace.Refresh(state); });
        workspace.Refresh(state);
        Check(chips.Children.Count == 2 && workspace.HasManagedSelection, "Hidden active field still supplies removable chips");
        var groupView = (Expander)host.Children[1];
        var fieldsPanel = (StackPanel)groupView.Content;
        Check(!fieldsPanel.Children.Cast<Expander>().Any(field => field.Header.ToString().StartsWith("Platform")),
            "Presentation hides the configured field without clearing its condition");
        ((Button)chips.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(state.Platform.Ids.SequenceEqual(new[] { other }) && chips.Children.Count == 1,
            "Removing one hidden chip preserves the other selected value");
        first.HiddenFields.Remove("Platform");
        workspace.InvalidateLayout();
        workspace.Refresh(state);
        var platformView = fieldsPanel.Children.Cast<Expander>().Single(field => field.Header.ToString().StartsWith("Platform"));
        platformView.IsExpanded = true;
        var platformChoices = ((StackPanel)platformView.Content).Children.OfType<StackPanel>().Single();
        var selected = platformChoices.Children.Cast<CheckBox>().Single(box => box.IsChecked == true);
        var groupOrder = host.Children.Cast<Expander>().ToArray();
        host.Measure(new Size(790, double.PositiveInfinity));
        host.Arrange(new Rect(host.DesiredSize));
        groupView.IsExpanded = false;
        host.Measure(new Size(320, double.PositiveInfinity));
        host.Arrange(new Rect(host.DesiredSize));
        groupView.IsExpanded = true;
        Check(host.Children.Cast<Expander>().SequenceEqual(groupOrder) && platformView.IsExpanded &&
            selected.IsChecked == true && state.Platform.Ids.SequenceEqual(new[] { other }),
            "Responsive layout and disclosure preserve customized order, expansion and selected filters");
        search.Text = "absent";
        workspace.Refresh(state, true);
        Check(empty.Visibility == Visibility.Visible && chips.Children.Count == 1 &&
            platformChoices.Children.Contains(selected) && selected.IsChecked == true &&
            host.Children.Count == 1 && ReferenceEquals(host.Children[0], groupView),
            "No matches preserves the selected value and packs its existing group without blank columns");
        first.Hidden = true;
        workspace.InvalidateLayout();
        workspace.Refresh(state);
        workspace.ClearAll();
        Check(!host.Children.Contains(groupView) && state.Platform == null && chips.Children.Count == 0 &&
            state.Hidden && state.UseAndFilteringStyle, "Clear filters covers hidden groups and preserves unmanaged native state");
    }

    private static double MeasureFilterWorkspace(Border frame)
    {
        frame.Measure(new Size(frame.Width, double.PositiveInfinity));
        frame.Arrange(new Rect(frame.DesiredSize));
        frame.UpdateLayout();
        return frame.ActualHeight;
    }

    private static void Presentation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "helium-icon-tests-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        try {
            var path = Path.Combine(directory, "synthetic.png");
            Check(IconCache.Load(path) == null, "Missing icon returns null");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            Check(IconCache.Load(path) == null, "Incomplete icon returns null");
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 255, 0, 0, 255 }, 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
            var image = IconCache.Load(path);
            Check(image != null && image.IsFrozen && ReferenceEquals(image, IconCache.Load(path)), "Icon recovers after invalid file becomes available and caches success");
            Check(image is BitmapSource small && small.PixelWidth == 1 && small.PixelHeight == 1, "Low-resolution icons are not upscaled during decoding");
            var tallPath = Path.Combine(directory, "tall.png");
            var tall = BitmapSource.Create(60, 120, 96, 96, PixelFormats.Bgra32, null, new byte[60 * 120 * 4], 60 * 4);
            var tallEncoder = new PngBitmapEncoder();
            tallEncoder.Frames.Add(BitmapFrame.Create(tall));
            using (var stream = File.Create(tallPath)) tallEncoder.Save(stream);
            Check(IconCache.Load(tallPath) is BitmapSource reduced && reduced.PixelWidth == 20 && reduced.PixelHeight == 40,
                "Icon decoding bounds the long edge and preserves aspect ratio");
            Check(IconCache.Load("https://invalid.example/icon.png") == null, "No network icon access");
            var rows = new[] { new GameRow(), new GameRow() }; var notified = 0;
            foreach (var row in rows) { row.PropertyChanged += (s, e) => notified++; row.Favorite = true; row.IsInstalled = true; }
            Check(notified == 4, "Every occurrence notifies visual state");
        } finally { Directory.Delete(directory, true); }
    }
    private static void Benchmarks()
    {
        foreach (int size in new[] { 200, 1500, 5000, 10000 }) {
            var games = Enumerable.Range(1, size).Select(i => { var g = Game(i, "Synthetic " + i); g.PlayCount = (ulong)(i % 100); g.GroupNames = new[] { "Group " + i % 50, "Other" }; return g; }).ToArray();
            Build(games, 1, NavigatorSortMode.PlayCount, true); // JIT/warmup, excluded.
            var watch = Stopwatch.StartNew(); var rows = Build(games, 1, NavigatorSortMode.PlayCount, true); watch.Stop();
            var projection = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); var sorted = NavigatorSortPolicy.Order(games, NavigatorSortMode.PlayCount, true, Now).ToArray(); watch.Stop();
            var sorting = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); var grouped = rows.GroupBy(row => row.GroupIdentity).ToList(); watch.Stop();
            var grouping = watch.Elapsed.TotalMilliseconds;
            var pending = games.Take(size / 10).Select(g => g.Id).ToArray();
            watch.Restart(); var oldChecks = games.Count(g => pending.Contains(g.Id));
            foreach (var id in pending) oldChecks += rows.Count(row => row.Id == id); watch.Stop(); var oldMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); var set = new HashSet<Guid>(pending); var matched = games.Count(g => set.Contains(g.Id));
            var lookup = rows.Where(row => set.Contains(row.Id)).ToLookup(row => row.Id);
            foreach (var id in pending) matched += lookup[id].Count(); watch.Stop();
            Check(oldChecks == matched && rows.Count == size * 2 && sorted.Length == size && grouped.Count > 0, "Benchmark retains membership");
            Console.WriteLine("BENCH " + size + ": projection=" + projection.ToString("F2") + "ms sort=" + sorting.ToString("F2") + "ms group=" + grouping.ToString("F2") + "ms update-scan-old=" + oldMs.ToString("F2") + "ms indexed=" + watch.Elapsed.TotalMilliseconds.ToString("F2") + "ms; synthetic, UI NOT MEASURED");
        }
    }
}
