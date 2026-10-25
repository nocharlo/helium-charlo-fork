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
        Check(!NavigatorRefreshDecision.ShouldRebuild(false, false),
            "Unchanged membership without dirty flag skips rebuild");
        Check(NavigatorRefreshDecision.ShouldRebuild(true, false),
            "Dirty flag rebuilds");
        Check(NavigatorRefreshDecision.ShouldRebuild(false, true),
            "Membership change rebuilds");

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
            "Native sort change is observable");
        Check(!NavigatorRefreshDecision.ShouldRebuild(false, false),
            "Native sort change alone does not yet rebuild");
        same.SortOrder = "Name";
        same.SortDirection = "Descending";
        Check(NavigatorRefreshDecision.ViewStateChanged(same, baseline),
            "Native direction change is observable");
        same.SortDirection = "Ascending";
        same.Grouping = "CompletionStatus";
        Check(NavigatorRefreshDecision.ViewStateChanged(same, baseline),
            "Native grouping change is observable");
        same.Grouping = "None";
        same.CompletionStatusFilterActive = true;
        Check(NavigatorRefreshDecision.ViewStateChanged(same, baseline),
            "Completion status filter state is observable");
        same.CompletionStatusFilterActive = false;
        same.PresetId = second;
        Check(NavigatorRefreshDecision.ViewStateChanged(same, baseline),
            "Preset switch is observable without logging its ID");
        same.PresetId = first;
        same.NavigatorGrouping = 1;
        Check(NavigatorRefreshDecision.ViewStateChanged(same, baseline),
            "Navigator grouping preference is observable");

        var messages = new List<string>();
        var disabled = new NavigatorDiagnostics(false, messages.Add);
        disabled.Record(baseline, false, false, false, 1, 0, 0);
        Check(messages.Count == 0, "Diagnostics default path emits nothing");
        var enabled = new NavigatorDiagnostics(true, messages.Add);
        enabled.Record(baseline, true, true, true, 4, 2, 1);
        Check(messages.Count == 2 && messages.Any(text => text.Contains("projectionMs=")),
            "Opt-in diagnostics record state and rebuild timing");
        Check(messages.All(text => !text.Contains(first.ToString())),
            "Diagnostics never print preset identifiers");

        Console.WriteLine("PASS: " + checks + " Navigator refresh decision and diagnostic checks.");
    }
}
