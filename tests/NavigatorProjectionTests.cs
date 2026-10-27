using System;
using System.Globalization;
using System.Linq;
using System.Threading;
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
        var ratingCategories = NavigatorProjection.Build(new[] {
            new NavigatorSource { Id = alpha, Name = "Unrated item", GroupNames = new[] { "☆" } },
            new NavigatorSource { Id = beta, Name = "Rated item", GroupNames = new[] { "★★★★★" } }
        }, 6, icon => icon);
        Check(ratingCategories.Select(row => row.Group).OrderBy(value => value).SequenceEqual(new[] { "☆", "★★★★★" }.OrderBy(value => value)),
            "Native Category grouping preserves custom star categories including unrated");
        Check(NavigatorProjection.DistinctGameCount(grouped.Select(row => row.Id)) == 3, "Displayed count deduplicates games");
        Check(NavigatorProjection.Build(null, 0, icon => icon).Count == 0, "Missing collection projects to empty list");
        var now = new DateTime(2026, 3, 20, 12, 0, 0);
        var recent = new[] {
            new NavigatorSource { Id = Guid.NewGuid(), Name = "older recent", LastActivity = now.AddDays(-2) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "newer recent", LastActivity = now.AddHours(-1) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "seven-day boundary", LastActivity = now.AddDays(-7) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "earlier month", LastActivity = now.AddDays(-8) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "older February", LastActivity = new DateTime(2026, 2, 1) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "newer February", LastActivity = new DateTime(2026, 2, 28) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "January", LastActivity = new DateTime(2026, 1, 31) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "missing" },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "future", LastActivity = now.AddMinutes(1) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "invalid", LastActivity = DateTime.MinValue }
        };
        var activity = NavigatorProjection.Build(recent, 8, icon => icon, now);
        Check(activity.Count == recent.Length, "Each game has exactly one activity occurrence");
        Check(activity.Take(3).All(row => row.Group == "Last 7 Days") &&
            activity.Take(3).Select(row => row.Name).SequenceEqual(new[] { "newer recent", "older recent", "seven-day boundary" }),
            "Rolling window is inclusive and newest first");
        Check(activity[3].Group == "Earlier This Month", "Earlier current-month activity follows seven-day group");
        Check(activity[4].Name == "newer February" && activity[5].Name == "older February" &&
            activity[4].Group == activity[5].Group && activity[6].Name == "January",
            "Previous months are newest first, with newest games first inside each month");
        Check(activity.Skip(7).All(row => row.Group == "No Recorded Activity"),
            "Missing, future and invalid timestamps have no recorded activity");
        var crossingMonth = NavigatorProjection.Build(new[] {
            new NavigatorSource { Id = alpha, Name = "previous month", LastActivity = new DateTime(2026, 2, 28) }
        }, 8, icon => icon, new DateTime(2026, 3, 2));
        Check(crossingMonth.Single().Group == "Last 7 Days", "Rolling window takes precedence over month boundary");
        var crossingYear = NavigatorProjection.Build(new[] {
            new NavigatorSource { Id = alpha, Name = "previous year", LastActivity = new DateTime(2025, 12, 31) }
        }, 8, icon => icon, new DateTime(2026, 1, 3));
        Check(crossingYear.Single().Group == "Last 7 Days", "Rolling window crosses year boundary");
        Check(NavigatorProjection.Build(recent.Take(2), 8, icon => icon, now).Count == 2 &&
            NavigatorProjection.Matches(activity[0].Key, NavigatorProjection.SearchKey("newer")),
            "Activity projection retains native filter subset and local search keys");
        var autumn = new DateTime(2026, 10, 20, 12, 0, 0);
        var archive = new[] {
            new NavigatorSource { Id = Guid.NewGuid(), Name = "year 2024", LastActivity = new DateTime(2024, 11, 1) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "older 2025", LastActivity = new DateTime(2025, 1, 10) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "September", LastActivity = new DateTime(2026, 9, 30) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "newer 2025", LastActivity = new DateTime(2025, 12, 31) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "August", LastActivity = new DateTime(2026, 8, 3) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "this month", LastActivity = new DateTime(2026, 10, 1) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "this week", LastActivity = autumn.AddDays(-1) },
            new NavigatorSource { Id = Guid.NewGuid(), Name = "no activity" }
        };
        var hybrid = NavigatorProjection.Build(archive, 8, icon => icon, autumn);
        Check(hybrid.Select(row => row.Group).SequenceEqual(new[] {
            "Last 7 Days", "Earlier This Month", "September 2026",
            "August 2026", "2025", "2025", "2024", "No Recorded Activity"
        }), "Hybrid groups current-year months and previous years chronologically");
        Check(hybrid[4].Name == "newer 2025" && hybrid[5].Name == "older 2025",
            "Hybrid orders games newest first within an annual group");
        var detailed = NavigatorProjection.Build(archive, 9, icon => icon, autumn);
        Check(detailed.Count == archive.Length && detailed[4].Group != detailed[5].Group &&
            detailed[4].Name == "newer 2025" && detailed[5].Name == "older 2025" &&
            detailed[6].Group == "November 2024",
            "Detailed retains individual month-year groups and prior ordering");
        var previousCulture = Thread.CurrentThread.CurrentCulture;
        try {
            foreach (var culture in new[] { "pt-BR", "en-US" }) {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var localizedHybrid = NavigatorProjection.Build(archive, 8, icon => icon, autumn);
                var localizedDetailed = NavigatorProjection.Build(archive, 9, icon => icon, autumn);
                Check(localizedHybrid.Select(row => row.Group).SequenceEqual(new[] {
                    "Last 7 Days", "Earlier This Month", "September 2026", "August 2026",
                    "2025", "2025", "2024", "No Recorded Activity"
                }), "Hybrid English labels under " + culture);
                Check(localizedDetailed.Select(row => row.Group).SequenceEqual(new[] {
                    "Last 7 Days", "Earlier This Month", "September 2026", "August 2026",
                    "December 2025", "January 2025", "November 2024", "No Recorded Activity"
                }), "Detailed English labels across years under " + culture);
                Check(localizedHybrid.Select(row => row.Id).SequenceEqual(hybrid.Select(row => row.Id)) &&
                    localizedDetailed.Select(row => row.Id).SequenceEqual(detailed.Select(row => row.Id)),
                    "Culture does not change chronological order under " + culture);
            }
        } finally { Thread.CurrentThread.CurrentCulture = previousCulture; }
        Check(hybrid.Select(row => row.Id).SequenceEqual(detailed.Select(row => row.Id)),
            "Style switch changes headings without changing chronological game order");
        Console.WriteLine("PASS: " + checks + " NavigatorProjection checks.");
    }
}
