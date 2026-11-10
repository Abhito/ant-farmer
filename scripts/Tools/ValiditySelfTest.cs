using System;
using System.Collections.Generic;
using System.Linq;
using AntFarmer.Sim;
using Godot;

namespace AntFarmer.Tools;

/// <summary>Breaks a known-good map in one way at a time and checks that the validator catches each break.</summary>
public static class ValiditySelfTest
{
    private const string GoodMap = "res://maps/arena_large.txt";
    private const string SmallMap = "res://maps/arena.txt";

    private record Case(string Name, string ExpectedRule, Action<char[][]> Break, ValidityRules Rules = null);

    public static bool Run()
    {
        string good = FileAccess.GetFileAsString(GoodMap);
        bool passed = true;

        ValidityReport baseline = MapValidator.Check(good);
        passed &= Expect("good map is valid", baseline.IsValid, baseline);

        passed &= Expect("costs match the game's pathfinder", CostsMatchPathfinder(baseline.Map), baseline);

        ValidityReport small = MapValidator.Check(FileAccess.GetFileAsString(SmallMap));
        passed &= Expect("small arena fails size", Has(small, "size"), small);

        foreach (Case c in Cases())
        {
            char[][] grid = ToGrid(good);
            c.Break(grid);
            ValidityReport report = MapValidator.Check(FromGrid(grid), c.Rules);
            passed &= Expect($"{c.Name} -> {c.ExpectedRule}", Has(report, c.ExpectedRule), report);
        }

        GD.Print(passed ? "Self-test passed." : "Self-test FAILED.");
        return passed;
    }

    private static IEnumerable<Case> Cases()
    {
        yield return new Case("unknown character", "format", g => Set(g, FindOpen(g), 'x'));
        yield return new Case("hole in the border", "border", g => g[g.Length / 2][0] = '.');
        yield return new Case("red nest missing a cell", "nests", g => Set(g, Find(g, 'R').First(), '.'));
        yield return new Case("nests too close", "nest-distance", _ => { }, new ValidityRules { MinNestDistance = 100f });
        yield return new Case("rock next to a nest", "nest-clearance", g => Set(g, Find(g, 'R').First() + new Vector2I(-1, 0), '#'));
        yield return new Case("too few piles", "food-count", g =>
        {
            foreach (Vector2I pile in Find(g, 'F').Skip(11))
                Set(g, pile, '.');
        });
        yield return new Case("pile beside a nest", "food-near-nest", g => Set(g, Find(g, 'R').First() + new Vector2I(3, 0), 'F'));
        yield return new Case("pile walled in", "reach", g =>
        {
            Vector2I pile = Find(g, 'F').First();
            foreach (Vector2I cell in Ring(pile, pile, 1))
                Set(g, cell, '#');
        });
        yield return new Case("red nest sealed in", "reach", g =>
        {
            List<Vector2I> nest = Find(g, 'R').ToList();
            foreach (Vector2I cell in Ring(Min(nest), Max(nest), 3))
                Set(g, cell, '#');
        });
        yield return new Case("extra food near red", "fairness", g =>
        {
            Vector2I nest = Find(g, 'R').First();
            foreach (Vector2I offset in new Vector2I[] { new(8, 0), new(0, 8), new(0, -8), new(7, 7), new(7, -7) })
                Set(g, nest + offset, 'F');
        });
        yield return new Case("small sealed pocket", "connected", g =>
        {
            Vector2I corner = FindOpenBlock(g, 9);
            foreach (Vector2I cell in Ring(corner + new Vector2I(1, 1), corner + new Vector2I(7, 7), 1))
                Set(g, cell, '#');
        }, new ValidityRules { MinConnectedShare = 0.995f });
    }

    // A* returns one cheapest path, so its cost must equal the Dijkstra cost from the same cell.
    private static bool CostsMatchPathfinder(MapData map)
    {
        var settings = new MatchSettings();
        var pathfinder = new Pathfinder(map, settings.ClimbCost);
        bool ok = true;

        foreach (Team team in Enum.GetValues<Team>())
        {
            Vector2I start = map.Nests[team][0];
            var costs = new CostField(map, new[] { start }, settings.ClimbCost);
            foreach (FoodPile pile in map.Food)
            {
                float expected = pathfinder.TravelCost(start, pile.Cell);
                if (Mathf.Abs(expected - costs[pile.Cell]) > 0.01f)
                {
                    GD.Print($"       {team} to pile {pile.Cell}: pathfinder {expected:0.00}, cost field {costs[pile.Cell]:0.00}");
                    ok = false;
                }
            }
        }
        return ok;
    }

    private static bool Expect(string name, bool ok, ValidityReport report)
    {
        GD.Print($"{(ok ? "ok  " : "FAIL")} {name}");
        if (!ok)
            foreach (ValidityProblem problem in report.Problems)
                GD.Print($"       {problem}");
        return ok;
    }

    private static bool Has(ValidityReport report, string rule) => report.Problems.Any(p => p.Rule == rule);

    private static char[][] ToGrid(string text) => text.Replace("\r", "").Split('\n')
        .Where(line => line.Length > 0 && !line.StartsWith(';'))
        .Select(line => line.ToCharArray())
        .ToArray();

    private static string FromGrid(char[][] grid) => string.Join("\n", grid.Select(row => new string(row)));

    private static void Set(char[][] grid, Vector2I cell, char c) => grid[cell.Y][cell.X] = c;

    private static IEnumerable<Vector2I> Find(char[][] grid, char c)
    {
        for (int y = 0; y < grid.Length; y++)
            for (int x = 0; x < grid[y].Length; x++)
                if (grid[y][x] == c)
                    yield return new Vector2I(x, y);
    }

    private static Vector2I FindOpen(char[][] grid) => Find(grid, '.').First();

    // Top-left corner of the first size x size block of plain ground.
    private static Vector2I FindOpenBlock(char[][] grid, int size)
    {
        for (int y = 0; y + size <= grid.Length; y++)
            for (int x = 0; x + size <= grid[y].Length; x++)
                if (Enumerable.Range(0, size).All(dy => Enumerable.Range(0, size).All(dx => grid[y + dy][x + dx] == '.')))
                    return new Vector2I(x, y);

        throw new InvalidOperationException($"No open {size}x{size} block in the test map.");
    }

    // The cells exactly 'distance' outside the rectangle from min to max.
    private static IEnumerable<Vector2I> Ring(Vector2I min, Vector2I max, int distance)
    {
        for (int y = min.Y - distance; y <= max.Y + distance; y++)
            for (int x = min.X - distance; x <= max.X + distance; x++)
                if (x == min.X - distance || x == max.X + distance || y == min.Y - distance || y == max.Y + distance)
                    yield return new Vector2I(x, y);
    }

    private static Vector2I Min(List<Vector2I> cells) => new(cells.Min(c => c.X), cells.Min(c => c.Y));
    private static Vector2I Max(List<Vector2I> cells) => new(cells.Max(c => c.X), cells.Max(c => c.Y));
}
