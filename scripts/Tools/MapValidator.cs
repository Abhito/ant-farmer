using System;
using System.Collections.Generic;
using System.Linq;
using AntFarmer.Sim;
using Godot;

namespace AntFarmer.Tools;

/// <summary>The limits a playable map must meet.</summary>
public class ValidityRules
{
    public int MinWidth = 71;
    public int MaxWidth = 121;
    public int MinHeight = 45;
    public int MaxHeight = 75;

    // Between nest centers, in a straight line. The closest preset places nests 30 apart.
    public float MinNestDistance = 30f;

    // No rock within this many cells of a nest.
    public int NestClearance = 2;

    public int MinFoodPiles = 12;
    public float MinFoodNestDistance = 6f;

    // Food access = average path cost from a nest to its nearest few piles.
    public int FoodAccessPiles = 5;
    public float MaxNestImbalance = 0.2f;

    public float MinConnectedShare = 0.9f;

    public float ClimbCost = 1f;
}

public readonly record struct ValidityProblem(string Rule, string Detail)
{
    public override string ToString() => $"{Rule}: {Detail}";
}

public class ValidityReport
{
    public List<ValidityProblem> Problems { get; } = new();
    public bool IsValid => Problems.Count == 0;

    // Null when the text couldn't be read as a map.
    public MapData Map { get; set; }
    public CostField RedCosts { get; set; }
    public CostField BlueCosts { get; set; }

    public float RedFoodAccess { get; set; } = float.NaN;
    public float BlueFoodAccess { get; set; } = float.NaN;
    public float ConnectedShare { get; set; } = float.NaN;

    public void Add(string rule, string detail) => Problems.Add(new ValidityProblem(rule, detail));
}

/// <summary>Checks a map against the playability rules in docs/MAP_GENERATION.md.</summary>
public static class MapValidator
{
    public static ValidityReport Check(string text, ValidityRules rules = null)
    {
        rules ??= new ValidityRules();
        var report = new ValidityReport();

        MapData map;
        try
        {
            map = MapLoader.Parse(text);
        }
        catch (FormatException e)
        {
            report.Add("format", e.Message);
            return report;
        }

        report.Map = map;

        CheckSize(map, rules, report);
        CheckBorder(map, report);
        bool nestsOk = CheckNestShape(map, report);
        if (nestsOk)
        {
            CheckNestDistance(map, rules, report);
            CheckNestClearance(map, rules, report);
        }
        CheckFoodPlacement(map, rules, report);

        report.RedCosts = new CostField(map, map.Nests[Team.Red], rules.ClimbCost);
        report.BlueCosts = new CostField(map, map.Nests[Team.Blue], rules.ClimbCost);
        bool allReached = CheckReach(map, report);
        if (allReached)
            CheckFairness(map, rules, report);

        CheckConnected(map, rules, report);
        return report;
    }

    private static void CheckSize(MapData map, ValidityRules rules, ValidityReport report)
    {
        if (map.Width < rules.MinWidth || map.Width > rules.MaxWidth ||
            map.Height < rules.MinHeight || map.Height > rules.MaxHeight)
            report.Add("size", $"{map.Width}x{map.Height} is outside {rules.MinWidth}-{rules.MaxWidth} x {rules.MinHeight}-{rules.MaxHeight}");
    }

    private static void CheckBorder(MapData map, ValidityReport report)
    {
        int open = 0;
        for (int x = 0; x < map.Width; x++)
        {
            if (map[x, 0].IsWalkable) open++;
            if (map[x, map.Height - 1].IsWalkable) open++;
        }
        for (int y = 1; y < map.Height - 1; y++)
        {
            if (map[0, y].IsWalkable) open++;
            if (map[map.Width - 1, y].IsWalkable) open++;
        }

        if (open > 0)
            report.Add("border", $"{open} border cells are not rock");
    }

    private static bool CheckNestShape(MapData map, ValidityReport report)
    {
        bool ok = true;
        foreach (Team team in Enum.GetValues<Team>())
        {
            List<Vector2I> nest = map.Nests[team];
            int width = nest.Max(c => c.X) - nest.Min(c => c.X) + 1;
            int height = nest.Max(c => c.Y) - nest.Min(c => c.Y) + 1;
            if (nest.Count != 4 || width != 2 || height != 2)
            {
                report.Add("nests", $"{team} nest is {nest.Count} cells in a {width}x{height} area, expected a 2x2 block");
                ok = false;
            }
        }
        return ok;
    }

    private static void CheckNestDistance(MapData map, ValidityRules rules, ValidityReport report)
    {
        float distance = NestCenter(map, Team.Red).DistanceTo(NestCenter(map, Team.Blue));
        if (distance < rules.MinNestDistance)
            report.Add("nest-distance", $"nests are {distance:0.#} cells apart, need {rules.MinNestDistance:0.#}");
    }

    private static void CheckNestClearance(MapData map, ValidityRules rules, ValidityReport report)
    {
        foreach (Team team in Enum.GetValues<Team>())
        {
            int rock = 0;
            foreach (Vector2I cell in CellsAround(map, map.Nests[team], rules.NestClearance))
                if (!map[cell].IsWalkable)
                    rock++;

            if (rock > 0)
                report.Add("nest-clearance", $"{rock} rock cells within {rules.NestClearance} of the {team} nest");
        }
    }

    private static void CheckFoodPlacement(MapData map, ValidityRules rules, ValidityReport report)
    {
        if (map.Food.Count < rules.MinFoodPiles)
            report.Add("food-count", $"{map.Food.Count} piles, need {rules.MinFoodPiles}");

        foreach (FoodPile pile in map.Food)
        {
            foreach (Team team in Enum.GetValues<Team>())
            {
                float distance = map.Nests[team].Min(c => ((Vector2)c).DistanceTo(pile.Cell));
                if (distance < rules.MinFoodNestDistance)
                    report.Add("food-near-nest", $"pile at {pile.Cell} is {distance:0.#} cells from the {team} nest");
            }
        }
    }

    private static bool CheckReach(MapData map, ValidityReport report)
    {
        bool ok = true;
        if (!map.Nests[Team.Blue].Any(report.RedCosts.Reaches))
        {
            report.Add("reach", "the nests can't reach each other");
            ok = false;
        }

        foreach (Team team in Enum.GetValues<Team>())
        {
            CostField costs = team == Team.Red ? report.RedCosts : report.BlueCosts;
            List<FoodPile> cutOff = map.Food.Where(p => !costs.Reaches(p.Cell)).ToList();
            if (cutOff.Count > 0)
            {
                string cells = string.Join(", ", cutOff.Take(3).Select(p => p.Cell.ToString()));
                report.Add("reach", $"{cutOff.Count} piles can't be reached from the {team} nest ({cells}{(cutOff.Count > 3 ? ", ..." : "")})");
                ok = false;
            }
        }
        return ok;
    }

    private static void CheckFairness(MapData map, ValidityRules rules, ValidityReport report)
    {
        report.RedFoodAccess = FoodAccess(map, report.RedCosts, rules.FoodAccessPiles);
        report.BlueFoodAccess = FoodAccess(map, report.BlueCosts, rules.FoodAccessPiles);

        float better = Math.Min(report.RedFoodAccess, report.BlueFoodAccess);
        float worse = Math.Max(report.RedFoodAccess, report.BlueFoodAccess);
        float gap = better > 0f ? worse / better - 1f : 0f;
        if (gap > rules.MaxNestImbalance)
        {
            Team favored = report.RedFoodAccess < report.BlueFoodAccess ? Team.Red : Team.Blue;
            report.Add("fairness", $"{favored} has {gap:P0} better food access (red {report.RedFoodAccess:0.0}, blue {report.BlueFoodAccess:0.0}), limit {rules.MaxNestImbalance:P0}");
        }
    }

    private static void CheckConnected(MapData map, ValidityRules rules, ValidityReport report)
    {
        var seen = new bool[map.Width * map.Height];
        int total = 0;
        int largest = 0;

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (!map[x, y].IsWalkable)
                    continue;

                total++;
                if (seen[y * map.Width + x])
                    continue;

                largest = Math.Max(largest, FloodSize(map, new Vector2I(x, y), seen));
            }
        }

        report.ConnectedShare = total > 0 ? (float)largest / total : 0f;
        if (report.ConnectedShare < rules.MinConnectedShare)
            report.Add("connected", $"largest open region is {report.ConnectedShare:P0} of open ground, need {rules.MinConnectedShare:P0}");
    }

    public static float FoodAccess(MapData map, CostField costs, int piles) =>
        map.Food.Select(p => costs[p.Cell]).OrderBy(c => c).Take(piles).Average();

    public static Vector2 NestCenter(MapData map, Team team)
    {
        List<Vector2I> nest = map.Nests[team];
        return new Vector2((float)nest.Average(c => c.X), (float)nest.Average(c => c.Y));
    }

    // In-bounds cells within the given square distance of any of the cells, not counting the cells themselves.
    private static IEnumerable<Vector2I> CellsAround(MapData map, List<Vector2I> cells, int distance)
    {
        int minX = cells.Min(c => c.X) - distance, maxX = cells.Max(c => c.X) + distance;
        int minY = cells.Min(c => c.Y) - distance, maxY = cells.Max(c => c.Y) + distance;
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2I(x, y);
                if (map.InBounds(p) && !cells.Contains(p))
                    yield return p;
            }
    }

    private static int FloodSize(MapData map, Vector2I start, bool[] seen)
    {
        var stack = new Stack<Vector2I>();
        stack.Push(start);
        seen[start.Y * map.Width + start.X] = true;
        int size = 0;

        while (stack.Count > 0)
        {
            Vector2I cell = stack.Pop();
            size++;
            foreach (Vector2I next in CostField.Neighbors(map, cell))
            {
                int i = next.Y * map.Width + next.X;
                if (!seen[i])
                {
                    seen[i] = true;
                    stack.Push(next);
                }
            }
        }
        return size;
    }
}
