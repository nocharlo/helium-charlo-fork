using System;
using System.Collections.Generic;
using HeliumHybrid;
using Playnite.SDK.Models;

internal static class NavigatorFilterComposerTests
{
    private static int checks;
    private static void Check(bool value, string name)
    {
        checks++;
        if (!value) throw new Exception(name);
    }
    private static void Main()
    {
        var genre = Guid.NewGuid();
        var platform = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var original = new FilterPresetSettings {
            UseAndFilteringStyle = true, IsUnInstalled = true, Favorite = true,
            Name = "kept", Genre = new IdItemFilterItemProperties { Ids = new List<Guid> { genre }, Text = "extra" },
            Platform = new IdItemFilterItemProperties { Ids = new List<Guid> { platform } },
            LastActivity = new EnumFilterItemProperties { Values = new List<int> { 1 } },
            ReleaseYear = new StringFilterItemProperties { Values = new List<string> { "2025" } }
        };
        var copy = NavigatorFilterComposer.Copy(original);
        copy.Genre.Ids.Add(missing);
        copy.LastActivity.Values.Add(2);
        copy.ReleaseYear.Values.Clear();
        Check(original.Genre.Ids.Count == 1 && original.LastActivity.Values.Count == 1 && original.ReleaseYear.Values.Count == 1, "deep copy");
        Check(copy.UseAndFilteringStyle && copy.Name == "kept" && copy.Favorite, "scalar copy");
        Check(NavigatorFilterComposer.Same(original, NavigatorFilterComposer.Copy(original)), "same external state");
        Check(!NavigatorFilterComposer.Same(original, copy), "external value change detected");
        var changed = NavigatorFilterComposer.ToggleId(original, NavigatorFilterField.Genre, missing);
        Check(changed.Genre.Ids.Count == 2 && changed.Genre.Text == "extra", "multiple selection and text");
        Check(changed.Platform.Ids[0] == platform && changed.Name == "kept" && changed.UseAndFilteringStyle, "unrelated filters preserved");
        Check(original.Genre.Ids.Count == 1, "original unchanged");
        foreach (NavigatorFilterField field in Enum.GetValues(typeof(NavigatorFilterField))) {
            var fieldChanged = NavigatorFilterComposer.ToggleId(original, field, missing);
            Check(NavigatorFilterComposer.IdField(fieldChanged, field).Ids.Contains(missing), "field toggle " + field);
            Check(NavigatorFilterComposer.IdField(original, field)?.Ids?.Contains(missing) != true, "field original " + field);
        }
        var temporary = NavigatorFilterComposer.TemporaryPreset(original);
        Check(temporary.Id == Guid.Empty && temporary.SortingOrder == null &&
              temporary.SortingOrderDirection == null && temporary.GroupingOrder == null, "unsaved preset leaves view options alone");
        temporary.Settings.Genre.Ids.Clear();
        Check(original.Genre.Ids.Count == 1, "temporary preset independent");
        changed = NavigatorFilterComposer.ToggleId(changed, NavigatorFilterField.Genre, missing);
        Check(changed.Genre.Ids.Count == 1 && changed.Genre.Ids[0] == genre, "remove selected value");
        var ready = new ReadyToPlayState();
        var installed = ready.Toggle(original);
        Check(installed.IsInstalled && !installed.IsUnInstalled && installed.Favorite, "ready applies globally");
        var externallyEdited = NavigatorFilterComposer.ToggleId(installed, NavigatorFilterField.Category, missing);
        var restored = ready.Toggle(externallyEdited);
        Check(!restored.IsInstalled && restored.IsUnInstalled && restored.Category.Ids.Contains(missing), "ready restores only installation");
        var next = ready.Toggle(original);
        ready.Observe(NavigatorFilterComposer.Installation(next, false, false));
        Check(!ready.IsActive(NavigatorFilterComposer.Installation(next, false, false)), "external installation change observed");
        var externallyInstalled = NavigatorFilterComposer.Installation(original, true, false);
        Check(!new ReadyToPlayState().OwnsRestriction, "no restriction owned initially");
        Check(new ReadyToPlayState().Toggle(externallyInstalled).IsInstalled, "external installed filter preserved");
        var cleared = NavigatorFilterComposer.Clear(original);
        Check(cleared.UseAndFilteringStyle && !cleared.Favorite && !NavigatorFilterComposer.HasActiveFilters(cleared), "clear keeps AND/OR");
        Check(!NavigatorFilterComposer.Same(original, cleared), "clear refresh detected");
        Check(NavigatorFilterComposer.HasActiveFilters(original), "active state");
        Check(Logic.ToggleRecentGrouping(2, 1) == 4 && Logic.ToggleRecentGrouping(4, 2) == 2 &&
              Logic.ToggleRecentGrouping(4, -1) == 0, "Recent Activity toggle");
        Check(Logic.ShortcutGroupingOverride(2, 4) == 4 &&
              Logic.ShortcutGroupingOverride(2, 2) == null, "temporary grouping does not change saved preference");
        Console.WriteLine("PASS: " + checks + " filter and grouping checks.");
    }
}
