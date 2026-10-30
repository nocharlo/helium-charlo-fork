using System;
using System.Collections.Generic;
using System.Linq;
using HeliumHybrid;

class NavigatorRefreshDecisionTests
{
    static int checks;

    static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new Exception(name);
    }

    static void Main()
    {
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var current = NavigatorRefreshDecision.SortedIds(new[] { second, first });
        var previous = NavigatorRefreshDecision.SortedIds(new[] { first, second });
        Check(current.SequenceEqual(previous), "Membership snapshot discards native order");
        Check(!NavigatorRefreshDecision.MembershipChanged(current, previous),
            "Native reordering alone does not change membership");
        Check(NavigatorRefreshDecision.MembershipChanged(current, new[] { first }),
            "Added member changes snapshot");
        Check(!NavigatorRefreshDecision.OrderChanged(new[] { first, second }, new[] { first, second }),
            "Stable native order does not change");
        Check(NavigatorRefreshDecision.OrderChanged(new[] { second, first }, new[] { first, second }),
            "Same members in new native order are detected");
        Check(!NavigatorRefreshDecision.ShouldRebuild(false, false, false, false),
            "Stable state skips rebuild");
        Check(NavigatorRefreshDecision.ShouldRebuild(true, false, false, false),
            "Dirty flag rebuilds");
        Check(NavigatorRefreshDecision.ShouldRebuild(false, true, false, false),
            "Membership change rebuilds");
        Check(NavigatorRefreshDecision.ShouldRebuild(false, false, true, false),
            "Native order change rebuilds without membership change");
        Check(NavigatorRefreshDecision.ShouldRebuild(false, false, false, true),
            "Native view state change rebuilds without membership change");

        var baseline = new NavigatorViewSnapshot {
            SortOrder = "Name", SortDirection = "Ascending", Grouping = "None",
            PresetId = first, NavigatorGrouping = 0
        };
        var same = new NavigatorViewSnapshot {
            SortOrder = "Name", SortDirection = "Ascending", Grouping = "None",
            PresetId = first, NavigatorGrouping = 0
        };
        Check(!NavigatorRefreshDecision.ViewStateChanged(baseline, same),
            "Identical native state is stable");
        same.SortOrder = "PlayCount";
        Check(NavigatorRefreshDecision.ViewStateChanged(same, baseline),
            "Most Played sort field change is detected");
        same.SortOrder = "Name";
        same.SortDirection = "Descending";
        Check(NavigatorRefreshDecision.ViewStateChanged(same, baseline),
            "Recently Played direction change is detected");
        same.SortDirection = "Ascending";
        same.SortOrder = "UserScore";
        Check(NavigatorRefreshDecision.ViewStateChanged(same, baseline),
            "Rating sort field change is detected");
        same.SortOrder = "Name";
        same.Grouping = "CompletionStatus";
        Check(NavigatorRefreshDecision.ViewStateChanged(same, baseline),
            "Native grouping change is detected");
        same.Grouping = "None";
        same.CompletionStatusFilterActive = true;
        Check(NavigatorRefreshDecision.ViewStateChanged(same, baseline),
            "Completion status filter state is observable in diagnostics");
        same.CompletionStatusFilterActive = false;
        same.PresetId = second;
        Check(NavigatorRefreshDecision.ViewStateChanged(same, baseline),
            "Preset switch is detected with unchanged membership and order");
        same.PresetId = first;
        same.NavigatorGrouping = 1;
        Check(NavigatorRefreshDecision.ViewStateChanged(same, baseline),
            "Independent Navigator grouping change is detected");

        var oldRows = new[] { new NavigatorProjectedRow {
            Id = first, Name = "Item", Key = "item", Group = "A", IconPath = "icon",
            IsInstalled = false, Favorite = false
        }, new NavigatorProjectedRow {
            Id = first, Name = "Item", Key = "item", Group = "B", IconPath = "icon"
        }};
        var visualRows = oldRows.Select(row => new NavigatorProjectedRow {
            Id = row.Id, Name = row.Name, Key = row.Key, Group = row.Group,
            IconPath = row.IconPath, IsInstalled = true, Favorite = true
        }).ToArray();
        Check(NavigatorRefreshDecision.ClassifyRows(oldRows, oldRows) == NavigatorRowChange.None,
            "Unrelated update skips rebuild");
        Check(NavigatorRefreshDecision.ClassifyRows(oldRows, visualRows) == NavigatorRowChange.Visual,
            "Indicator changes are visual across duplicate group rows");
        visualRows[0].Name = "Renamed";
        Check(NavigatorRefreshDecision.ClassifyRows(oldRows, visualRows) == NavigatorRowChange.Structural,
            "Name changes need rebuild");
        visualRows[0].Name = "Item"; visualRows[1].Group = "C";
        Check(NavigatorRefreshDecision.ClassifyRows(oldRows, visualRows) == NavigatorRowChange.Structural,
            "Group changes need rebuild");
        Check(NavigatorRefreshDecision.ClassifyRows(oldRows, visualRows.Take(1)) == NavigatorRowChange.Structural,
            "Group occurrence changes need rebuild");
        var activityBefore = new[] { new NavigatorProjectedRow { Id = first, Name = "Item", Group = "Last 7 Days", ActivityTicks = 10, GroupOrder = long.MaxValue } };
        var activityAfter = new[] { new NavigatorProjectedRow { Id = first, Name = "Item", Group = "Last 7 Days", ActivityTicks = 11, GroupOrder = long.MaxValue } };
        Check(NavigatorRefreshDecision.ClassifyRows(activityBefore, activityAfter) == NavigatorRowChange.Structural,
            "Updated activity within the same group rebuilds chronological order");
        var sortBefore = new[] { new NavigatorProjectedRow { Id = first, Name = "Item", Group = "A", SortValue = 10 } };
        var sortAfter = new[] { new NavigatorProjectedRow { Id = first, Name = "Item", Group = "A", SortValue = 11 } };
        Check(NavigatorRefreshDecision.ClassifyRows(sortBefore, sortAfter) == NavigatorRowChange.Structural,
            "Changed sort metadata rebuilds rows even when membership and title are unchanged");
        Check(NavigatorRefreshDecision.EffectiveGrouping(0, "CompletionStatus") == 4,
            "Default follows native completion grouping");
        Check(NavigatorRefreshDecision.EffectiveGrouping(0, "Series") == 5 &&
            NavigatorRefreshDecision.EffectiveGrouping(0, "Category") == 6 &&
            NavigatorRefreshDecision.EffectiveGrouping(0, "Platform") == 7,
            "Default follows supported native groups");
        Check(NavigatorRefreshDecision.EffectiveGrouping(2, "CompletionStatus") == 2,
            "Explicit Navigator grouping takes precedence");
        Check(NavigatorRefreshDecision.EffectiveGrouping(1, "Category") == 1 &&
            NavigatorRefreshDecision.EffectiveGrouping(0, "Category") == 6,
            "Custom rating categories follow native grouping only in default mode");
        Check(NavigatorRefreshDecision.EffectiveGrouping(4, "Category") == 8 &&
            NavigatorRefreshDecision.EffectiveGrouping(3, "Category") == 3,
            "Recent Activity mode is independent and old grouping indexes remain stable");
        Check(NavigatorRefreshDecision.EffectiveGrouping(4, "Category", 0) == 8 &&
            NavigatorRefreshDecision.EffectiveGrouping(4, "Category", 1) == 9 &&
            NavigatorRefreshDecision.EffectiveGrouping(2, "Category", 1) == 2,
            "Activity style changes only Recent Activity effective mode");
        Check(NavigatorRefreshDecision.EffectiveGrouping(0, "Name") == 0,
            "Unsupported native group remains flat");
        var groupState = new NavigatorGroupState();
        Check(groupState.IsExpanded(1, "Shared"), "New groups expand by default");
        groupState.SetExpanded(1, "Shared", false);
        Check(!groupState.IsExpanded(1, "Shared"), "Collapsed group survives view replacement");
        Check(groupState.IsExpanded(2, "Shared"), "Same group name in another mode is independent");
        groupState.SetExpanded(2, "Shared", true);
        Check(!groupState.IsExpanded(1, "Shared"), "Returning to prior mode restores its state");
        groupState.SetExpanded(1, "Offscreen", false);
        Check(!groupState.IsExpanded(1, "Offscreen"), "Offscreen group state does not need a WPF container");
        groupState.SetExpanded(1, "Shared", true);
        Check(groupState.IsExpanded(1, "Shared"), "Expanded group state survives view replacement");
        groupState.SetExpanded(8, "Last 7 Days", false);
        Check(!groupState.IsExpanded(8, "Last 7 Days") && groupState.IsExpanded(8, "Earlier This Month"),
            "Recent Activity preserves one collapsed group without closing others");
        groupState.SetExpanded(9, "Last 7 Days", true);
        Check(!groupState.IsExpanded(8, "Last 7 Days") && groupState.IsExpanded(9, "Last 7 Days"),
            "Hybrid and Detailed expansion states are independent");
        var messages = new List<string>();
        var disabled = new NavigatorDiagnostics(false, messages.Add);
        disabled.Record(baseline, false, false, false, false, false, 1, 0, 0);
        Check(messages.Count == 0, "Diagnostics default path emits nothing");
        var enabled = new NavigatorDiagnostics(true, messages.Add);
        enabled.Record(baseline, false, false, true, true, true, 4, 2, 1);
        Check(messages.Count == 2 && messages.Any(text => text.Contains("orderChanged=True") &&
            text.Contains("viewStateChanged=True") && text.Contains("projectionMs=")),
            "Opt-in diagnostics record reasons and rebuild timing");
        enabled.VisualUpdate(2);
        Check(messages.Any(text => text.Contains("visual-update occurrences=2")),
            "Opt-in diagnostics distinguish visual row updates from rebuilds");
        Check(messages.All(text => !text.Contains(first.ToString())),
            "Diagnostics never print preset identifiers");

        Console.WriteLine("PASS: " + checks + " Navigator refresh decision and diagnostic checks.");
    }
}
