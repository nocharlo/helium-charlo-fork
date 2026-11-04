using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using HeliumHybrid;

internal static class NavigatorOrganizationTests
{
    private static int checks;
    private static readonly DateTime Now = new DateTime(2026, 10, 20, 12, 0, 0);
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static NavigatorSource Game(int id, string name = "Synthetic") => new NavigatorSource { Id = new Guid(id, 0, 0, new byte[8]), Name = name };
    private static List<NavigatorProjectedRow> Build(IEnumerable<NavigatorSource> games, int group, NavigatorSortMode sort = NavigatorSortMode.Name, bool descending = false) =>
        NavigatorProjection.Build(games, group, icon => icon, Now, sort, descending);

    [STAThread]
    private static void Main()
    {
        CatalogAndGroups(); Sorting(); Settings(); OrganizationControls(); Presentation(); Benchmarks();
        Console.WriteLine("PASS: " + checks + " Navigator organization, settings and WPF model checks.");
    }
    private static void OrganizationControls()
    {
        var assembly = typeof(NavigatorOrganizationTests).Assembly;
        using (var stream = assembly.GetManifestResourceStream("Navigator.xaml"))
        using (var reader = new StreamReader(stream)) {
            var root = (Grid)XamlReader.Parse(reader.ReadToEnd().Replace("assembly=HeliumCharlo", "assembly=" + assembly.GetName().Name));
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
            .SequenceEqual(new[] { "Navigation", "Appearance", "Organization" }), "Settings tab structure");
        tabs.SelectedIndex = 2;
        var page = (StackPanel)((ScrollViewer)((TabItem)tabs.SelectedItem).Content).Content;
        var style = (ListBox)((GroupBox)page.Children[0]).Content;
        var binding = BindingOperations.GetBinding(style, ListBox.SelectedIndexProperty);
        Check(binding.Path.Path == nameof(Preferences.RecentActivityGroupingStyle) && binding.Mode == BindingMode.TwoWay,
            "Activity selection is bound to the existing persisted setting");
        Check(style.Items.Cast<ListBoxItem>().Select(item => ((TextBlock)((StackPanel)item.Content).Children[0]).Text)
            .SequenceEqual(new[] { "Hybrid", "Detailed" }), "Activity cards preserve the stored index order");
        var editors = ((Grid)page.Children[1]).Children.Cast<GroupBox>().ToArray();
        Check(editors.Length == 2, "Organization has separate visibility cards");
        var layout = (Grid)page.Children[1];
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
        }
        var p = new Preferences { Grouping = 15, SortMode = 8 }; p.MigrateNavigation();
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
