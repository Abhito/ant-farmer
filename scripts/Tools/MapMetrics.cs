using System;
using System.Collections.Generic;
using System.Linq;
using AntFarmer.Sim;
using Godot;

namespace AntFarmer.Tools;

/// <summary>Measurable properties of a map, for comparing what different generator settings produce.</summary>
public static class MapMetrics
{
    // Exposure and entrances look at the area this many cells around a nest.
    public const int NestArea = 6;

    // Middle of the defender range (MatchSettings MinDefendLeash to MaxDefendLeash).
    public const float DefenseRadius = 14f;

    // Values are NaN when they can't be measured, e.g. a pile no nest can reach.
    public static List<(string Name, double Value)> Measure(ValidityReport report, ValidityRules rules = null)
    {
        rules ??= new ValidityRules();
        MapData map = report.Map;
        var values = new List<(string, double)>();
        void Add(string name, double value) => values.Add((name, value));

        List<Vector2I> open = AllCells(map).Where(c => map[c].IsWalkable).ToList();
        int inside = (map.Width - 2) * (map.Height - 2);
        int insideRock = AllCells(map).Count(c => !map[c].IsWalkable && !OnBorder(map, c));

        Add("width", map.Width);
        Add("height", map.Height);
        Add("rock_density", (double)insideRock / inside);
        Add("hill_share", (double)open.Count(c => map[c].Height > 0) / open.Count);
        Add("max_height", open.Max(c => map[c].Height));
        Add("food_piles", map.Food.Count);

        Vector2 red = MapValidator.NestCenter(map, Team.Red);
        Vector2 blue = MapValidator.NestCenter(map, Team.Blue);
        Add("nest_distance", red.DistanceTo(blue));

        float nestPath = map.Nests[Team.Blue].Min(c => report.RedCosts[c]);
        Add("nest_path_cost", Finite(nestPath));
        var flatCosts = new CostField(map, map.Nests[Team.Red], 0f);
        float flatPath = map.Nests[Team.Blue].Min(c => flatCosts[c]);
        Add("hill_penalty", Finite(nestPath / flatPath - 1f));

        Add("food_cost_mean", Finite(map.Food.Average(p => (report.RedCosts[p.Cell] + report.BlueCosts[p.Cell]) / 2f)));

        float redAccess = MapValidator.FoodAccess(map, report.RedCosts, rules.FoodAccessPiles);
        float blueAccess = MapValidator.FoodAccess(map, report.BlueCosts, rules.FoodAccessPiles);
        Add("food_access_red", Finite(redAccess));
        Add("food_access_blue", Finite(blueAccess));
        Add("fairness_gap", Finite(Gap(redAccess, blueAccess)));

        int redExposure = Exposure(map, Team.Red);
        int blueExposure = Exposure(map, Team.Blue);
        Add("exposure_red", redExposure);
        Add("exposure_blue", blueExposure);
        Add("exposure_gap", Gap(redExposure, blueExposure));
        Add("entrances_red", Entrances(map, Team.Red));
        Add("entrances_blue", Entrances(map, Team.Blue));

        Add("narrow_cells", (double)open.Count(c => IsNarrow(map, c)) / open.Count);
        Add("dead_ends", open.Count(c => OpenSides(map, c) == 1));

        List<int> regions = MapValidator.RegionSizes(map);
        Add("regions", regions.Count);
        Add("connected_share", (double)regions.Max() / open.Count);

        int outside = open.Count(c => DistanceToNest(map, Team.Red, c) > DefenseRadius && DistanceToNest(map, Team.Blue, c) > DefenseRadius);
        Add("outside_defense", (double)outside / open.Count);

        return values;
    }

    // How much bigger the larger value is than the smaller, e.g. 0.25 = 25% bigger.
    private static double Gap(double a, double b)
    {
        double low = Math.Min(a, b);
        return low > 0 ? Math.Max(a, b) / low - 1 : double.NaN;
    }

    private static double Finite(double value) => double.IsFinite(value) ? value : double.NaN;

    private static int Exposure(MapData map, Team team) =>
        CellsNear(map, team, NestArea).Count(c => map[c].IsWalkable && !map.Nests[team].Contains(c));

    // Separate open stretches along the square ring NestArea cells out from the nest.
    private static int Entrances(MapData map, Team team)
    {
        List<Vector2I> ring = Ring(map.Nests[team], NestArea);
        bool Open(Vector2I c) => map.InBounds(c) && map[c].IsWalkable;

        int stretches = 0;
        for (int i = 0; i < ring.Count; i++)
            if (Open(ring[i]) && !Open(ring[(i + ring.Count - 1) % ring.Count]))
                stretches++;

        // A ring with no rock at all is one wide-open way in.
        return stretches == 0 && ring.All(Open) ? 1 : stretches;
    }

    // Open cell squeezed between rock on opposite sides, at most 2 cells apart.
    private static bool IsNarrow(MapData map, Vector2I c)
    {
        bool Rock(int x, int y) => !map.InBounds(new Vector2I(x, y)) || !map[x, y].IsWalkable;

        bool acrossX = Rock(c.X - 1, c.Y) && (Rock(c.X + 1, c.Y) || Rock(c.X + 2, c.Y))
                       || Rock(c.X + 1, c.Y) && Rock(c.X - 2, c.Y);
        bool acrossY = Rock(c.X, c.Y - 1) && (Rock(c.X, c.Y + 1) || Rock(c.X, c.Y + 2))
                       || Rock(c.X, c.Y + 1) && Rock(c.X, c.Y - 2);
        return acrossX || acrossY;
    }

    private static int OpenSides(MapData map, Vector2I c)
    {
        int count = 0;
        foreach (Vector2I d in new Vector2I[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) })
            if (map.InBounds(c + d) && map[c + d].IsWalkable)
                count++;
        return count;
    }

    private static float DistanceToNest(MapData map, Team team, Vector2I cell) =>
        map.Nests[team].Min(n => ((Vector2)n).DistanceTo(cell));

    private static IEnumerable<Vector2I> CellsNear(MapData map, Team team, int distance)
    {
        List<Vector2I> nest = map.Nests[team];
        for (int y = nest.Min(c => c.Y) - distance; y <= nest.Max(c => c.Y) + distance; y++)
            for (int x = nest.Min(c => c.X) - distance; x <= nest.Max(c => c.X) + distance; x++)
                if (map.InBounds(new Vector2I(x, y)))
                    yield return new Vector2I(x, y);
    }

    // The ring in order around the rectangle, so neighbors in the list are neighbors on the map.
    private static List<Vector2I> Ring(List<Vector2I> cells, int distance)
    {
        int minX = cells.Min(c => c.X) - distance, maxX = cells.Max(c => c.X) + distance;
        int minY = cells.Min(c => c.Y) - distance, maxY = cells.Max(c => c.Y) + distance;
        var ring = new List<Vector2I>();
        for (int x = minX; x < maxX; x++) ring.Add(new Vector2I(x, minY));
        for (int y = minY; y < maxY; y++) ring.Add(new Vector2I(maxX, y));
        for (int x = maxX; x > minX; x--) ring.Add(new Vector2I(x, maxY));
        for (int y = maxY; y > minY; y--) ring.Add(new Vector2I(minX, y));
        return ring;
    }

    private static bool OnBorder(MapData map, Vector2I c) =>
        c.X == 0 || c.Y == 0 || c.X == map.Width - 1 || c.Y == map.Height - 1;

    private static IEnumerable<Vector2I> AllCells(MapData map)
    {
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                yield return new Vector2I(x, y);
    }
}
