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
        Check(Logic.SearchKey("Pokémon — Edição").Contains(Logic.SearchKey("pokemon")), "Busca com acentos");
        Check(Logic.SearchKey(null) == "", "Título ausente");
        Check(Logic.SearchKey("GOD OF WAR").Contains(Logic.SearchKey("god of war")), "Busca sem distinção de caixa");
        Check(Logic.Groups(null).Single() == "Sem grupo", "Sem série");
        Check(Logic.Groups(new[]{"Série A", "Série A", "", " ", "Série B"}).Count() == 2, "Multigrupo sem duplicação");
        Check(Logic.ClampWidth(-100) == 180 && Logic.ClampWidth(900) == 380 && Logic.ClampWidth(double.NaN) == 230, "Largura válida");
        Check(Logic.Compact(0, false, 850, 420), "Entra em compacto com pouca largura");
        Check(Logic.Compact(0, true, 910, 420), "Histerese impede alternância no limiar");
        Check(!Logic.Compact(0, true, 1030, 420), "Volta ao amplo com folga");
        Check(Logic.Compact(1, false, 3000, 420) && !Logic.Compact(2, true, 400, 420), "Modos manuais");
        // Model a large library: no rows lost when assigning multiple memberships and searching.
        var watch = Stopwatch.StartNew();
        var games = Enumerable.Range(0, 50000).Select(i => new { Id = i, Name = "Jogo " + i,
            Groups = new[] { "Série " + i % 200, "Favoritos", "Favoritos" } }).ToArray();
        var rows = games.SelectMany(g => Logic.Groups(g.Groups).Select(group => new { g.Id, group, Key = Logic.SearchKey(g.Name) })).ToArray();
        Check(rows.Length == 100000, "50 mil jogos / 100 mil participações");
        Check(rows.Select(r=>r.Id).Distinct().Count() == 50000, "Sem perda de jogos");
        Check(rows.Where(r => r.Key.Contains(Logic.SearchKey("Jogo 12345"))).Select(r=>r.Id).Distinct().Single() == 12345, "Filtro de jogo multigrupo");
        Console.WriteLine("PASS: " + assertions + " verificações; cenário 50.000 jogos: " + watch.ElapsedMilliseconds + " ms.");
    }
}
