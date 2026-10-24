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
            new NavigatorSource { Id = beta, Name = "BioShock", SortingName = "Zeta", Icon = "bio", GroupNames = new[]{"Series B","Series B","Series A"} },
            new NavigatorSource { Id = alpha, Name = "Pokémon", SortingName = "Alpha", GroupNames = new[]{"Series A"} },
            new NavigatorSource { Id = gamma, Name = null, SortingName = null, Icon = " ", GroupNames = new string[0] }
        };
        var plain = NavigatorProjection.Build(games, 0, icon => { iconCalls++; return "full/" + icon; });
        Check(plain.Select(row => row.Id).SequenceEqual(new[]{gamma,alpha,beta}), "Sort by SortingName, falling back to Name");
        Check(plain.All(row => row.Group == ""), "Alphabetical rows have no group");
        Check(plain[0].Name == "Untitled" && plain[0].Key == "", "Null title keeps prior display and search behavior");
        Check(plain[2].IconPath == "full/bio" && iconCalls == 1, "Resolve nonblank icon once per game");
        Check(NavigatorProjection.Matches(plain[1].Key, NavigatorProjection.SearchKey("pokemon")), "Accent-insensitive search");
        Check(NavigatorProjection.Matches(plain[1].Key, NavigatorProjection.SearchKey("")), "Empty search matches all rows");
        Check(!NavigatorProjection.Matches(plain[2].Key, NavigatorProjection.SearchKey("pokemon")), "Search excludes unrelated rows");
        var grouped = NavigatorProjection.Build(games, 1, icon => { iconCalls++; return "full/" + icon; });
        Check(grouped.Count == 4, "Multi-group rows plus fallback group");
        Check(grouped.Select(row => row.Group).SequenceEqual(new[]{"No group","Series A","Series A","Series B"}), "Group ordering and fallback");
        Check(grouped.Count(row => row.Id == beta) == 2 && iconCalls == 2, "Duplicate memberships removed; icon resolved once per game");
        Check(NavigatorProjection.DistinctGameCount(grouped.Select(row => row.Id)) == 3, "Displayed count deduplicates games");
        Check(NavigatorProjection.Build(null, 0, icon => icon).Count == 0, "Missing collection projects to empty list");
        Console.WriteLine("PASS: " + checks + " NavigatorProjection checks.");
    }
}
