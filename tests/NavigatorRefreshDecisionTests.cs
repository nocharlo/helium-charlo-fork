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
        var messages = new List<string>();
        var disabled = new NavigatorDiagnostics(false, messages.Add);
        disabled.Record(baseline, false, false, false, false, false, 1, 0, 0);
        Check(messages.Count == 0, "Diagnostics default path emits nothing");
        var enabled = new NavigatorDiagnostics(true, messages.Add);
        enabled.Record(baseline, false, false, true, true, true, 4, 2, 1);
        Check(messages.Count == 2 && messages.Any(text => text.Contains("orderChanged=True") &&
            text.Contains("viewStateChanged=True") && text.Contains("projectionMs=")),
            "Opt-in diagnostics record reasons and rebuild timing");
        Check(messages.All(text => !text.Contains(first.ToString())),
            "Diagnostics never print preset identifiers");

        Console.WriteLine("PASS: " + checks + " Navigator refresh decision and diagnostic checks.");
    }
}
