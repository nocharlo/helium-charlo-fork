using System;
using System.Linq;
using HeliumHybrid;

class NavigatorProjectionTests
{
    static int checks;
    static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new Exception(name);
    }

    static void Main()
    {
        var alpha = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var beta = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var gamma = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var iconCalls = 0;
        var games = new[] {
            new NavigatorSource { Id = beta, Name = "Item Z", SortingName = "Zeta", Icon = "icon-a", IsInstalled = true, Favorite = true, GroupNames = new[]{"Series B","Series B","Series A"} },
            new NavigatorSource { Id = alpha, Name = "Café", SortingName = "Alpha", GroupNames = new[]{"Series A"} },
            new NavigatorSource { Id = gamma, Name = null, SortingName = null, Icon = " ", GroupNames = new string[0] }
        };
        var plain = NavigatorProjection.Build(games, 0, icon => { iconCalls++; return "full/" + icon; });
        Check(plain.Select(row => row.Id).SequenceEqual(new[]{beta,alpha,gamma}), "Preserve native ungrouped order");
        var reverse = NavigatorProjection.Build(games.Reverse(), 0, icon => icon);
        Check(reverse.Select(row => row.Id).SequenceEqual(new[]{gamma,alpha,beta}),
            "Native direction and changed sequence pass through without alphabetical sorting");
        var tied = NavigatorProjection.Build(new[] {
            new NavigatorSource { Id = beta, Name = "Second", SortingName = "Same" },
            new NavigatorSource { Id = alpha, Name = "First", SortingName = "Same" }
        }, 0, icon => icon);
        Check(tied.Select(row => row.Id).SequenceEqual(new[]{beta,alpha}),
            "Native tie order is preserved");
        Check(plain.All(row => row.Group == ""), "Ungrouped rows have no group");
        Check(plain[0].IsInstalled && plain[0].Favorite && !plain[1].IsInstalled && !plain[1].Favorite,
            "Installation and favorite state project without changing order");
        Check(plain[2].Name == "Untitled" && plain[2].Key == "", "Null title keeps prior display and search behavior");
        Check(plain[0].IconPath == "full/icon-a" && iconCalls == 1, "Resolve nonblank icon once per game");
        Check(NavigatorProjection.Matches(plain[1].Key, NavigatorProjection.SearchKey("cafe")), "Accent-insensitive search");
        Check(NavigatorProjection.Matches(plain[1].Key, NavigatorProjection.SearchKey("")), "Empty search matches all rows");
        Check(!NavigatorProjection.Matches(plain[0].Key, NavigatorProjection.SearchKey("cafe")), "Search excludes unrelated rows");
        var grouped = NavigatorProjection.Build(games, 1, icon => { iconCalls++; return "full/" + icon; });
        Check(grouped.Count == 4, "Multi-group rows plus fallback group");
        Check(grouped.Select(row => row.Group).SequenceEqual(new[]{"No group","Series A","Series A","Series B"}), "Group ordering and fallback");
        Check(grouped.Select(row => row.Id).SequenceEqual(new[]{gamma,beta,alpha,beta}), "Preserve native order within independent groups");
        var groupedReverse = NavigatorProjection.Build(games.Reverse(), 1, icon => icon);
        Check(groupedReverse.Select(row => row.Id).SequenceEqual(new[]{gamma,alpha,beta,beta}),
            "Independent group headings stay sorted while members follow native order");
        Check(grouped.Count(row => row.Id == beta) == 2 && iconCalls == 2, "Duplicate memberships removed; icon resolved once per game");
        Check(NavigatorProjection.DistinctGameCount(grouped.Select(row => row.Id)) == 3, "Displayed count deduplicates games");
        Check(NavigatorProjection.Build(null, 0, icon => icon).Count == 0, "Missing collection projects to empty list");
        Console.WriteLine("PASS: " + checks + " NavigatorProjection checks.");
    }
}
