using System;
using System.Linq;
using System.Diagnostics;
using HeliumHybrid;
class LogicTests
{
    static int assertions;
    static void Check(bool value, string label) { assertions++; if (!value) throw new Exception(label); }
    static void Main()
    {
        Check(Logic.SearchKey("Pokémon — Special Edition").Contains(Logic.SearchKey("pokemon")), "Accent-insensitive search");
        Check(Logic.SearchKey(null) == "", "Missing title");
        Check(Logic.SearchKey("GOD OF WAR").Contains(Logic.SearchKey("god of war")), "Case-insensitive search");
        Check(Logic.Groups(null).Single() == "No group", "No series");
        Check(Logic.Groups(new[]{"Series A", "Series A", "", " ", "Series B"}).Count() == 2, "Unique group membership");
        Check(Logic.ClampWidth(-100) == 180 && Logic.ClampWidth(900) == 380 && Logic.ClampWidth(double.NaN) == 230, "Valid width");
        Check(Logic.Compact(0, false, 850), "Compact at narrow width");
        Check(Logic.Compact(0, true, 930), "Compact mode hysteresis");
        Check(!Logic.Compact(0, true, 1000), "Return to wide mode");
        Check(Logic.Compact(1, false, 2000) && !Logic.Compact(2, true, 400), "Manual modes");
        // Model a large library: no rows lost when assigning multiple memberships and searching.
        var watch = Stopwatch.StartNew();
        var games = Enumerable.Range(0, 50000).Select(i => new { Id = i, Name = "Game " + i,
            Groups = new[] { "Series " + i % 200, "Favorites", "Favorites" } }).ToArray();
        var rows = games.SelectMany(g => Logic.Groups(g.Groups).Select(group => new { g.Id, group, Key = Logic.SearchKey(g.Name) })).ToArray();
        Check(rows.Length == 100000, "50,000 games / 100,000 memberships");
        Check(rows.Select(r=>r.Id).Distinct().Count() == 50000, "No games lost");
        Check(rows.Where(r => r.Key.Contains(Logic.SearchKey("Game 12345"))).Select(r=>r.Id).Distinct().Single() == 12345, "Grouped game search");
        Check(Logic.RecentActivityStyle(0, 0, false) == 0 &&
            Logic.RecentActivityStyle(4, 0, false) == 1 &&
            Logic.RecentActivityStyle(4, 1, true) == 1 &&
            Logic.RecentActivityStyle(4, 7, true) == 0,
            "Activity style defaults to Hybrid but migrates existing activity users to Detailed");
        Console.WriteLine("PASS: " + assertions + " checks; 50,000-game scenario: " + watch.ElapsedMilliseconds + " ms.");
    }
}
