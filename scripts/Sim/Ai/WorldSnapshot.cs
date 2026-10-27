using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AntFarmer.Sim.Ai;

/// <summary>What an AI knows about one ant. Order and PendingOrder are only filled in for its own ants.</summary>
public readonly record struct AntInfo(int Id, Vector2 Position, Vector2I Cell, bool Carrying, bool Dead, Role Role,
    RoleOrder? Order, RoleOrder? PendingOrder = null)
{
    public RoleOrder? NextOrder => PendingOrder ?? Order;
}

public readonly record struct FoodInfo(int Id, Vector2I Cell, int Amount, int StartAmount);

/// <summary>Everything one side's AI can see, as of the start of the tick. Read-only.</summary>
public class WorldSnapshot
{
    public Team Team { get; private init; }
    public Team Enemy => Team == Team.Red ? Team.Blue : Team.Red;

    public float ElapsedSeconds { get; private init; }
    public float RemainingSeconds { get; private init; }

    public int MyDeliveries { get; private init; }
    public int EnemyDeliveries { get; private init; }
    public int WinTarget { get; private init; }

    public int MyKills { get; private init; }
    public int EnemyKills { get; private init; }

    public IReadOnlyList<AntInfo> MyAnts { get; private init; }
    public IReadOnlyList<AntInfo> EnemyAnts { get; private init; }

    // Only piles that still have food.
    public IReadOnlyList<FoodInfo> Food { get; private init; }

    public IReadOnlyList<Vector2I> MyNest { get; private init; }
    public IReadOnlyList<Vector2I> EnemyNest { get; private init; }

    public MapData Map { get; private init; }
    public MatchSettings Settings { get; private init; }

    private Pathfinder _pathfinder;
    private readonly Dictionary<(Vector2I, Vector2I), float> _pathCosts = new();

    public int ScoreDifference => MyDeliveries - EnemyDeliveries;
    public int MyCarriers => MyAnts.Count(a => a.Carrying);
    public int EnemyCarriers => EnemyAnts.Count(a => a.Carrying);

    /// <summary>Walking cost between two cells, cached. Infinity if there is no path.</summary>
    public float PathCost(Vector2I from, Vector2I to)
    {
        if (from == to)
            return 0f;

        if (!_pathCosts.TryGetValue((from, to), out float cost))
        {
            cost = _pathfinder.TravelCost(from, to);
            _pathCosts[(from, to)] = cost;
        }

        return cost;
    }

    public float CostHome(AntInfo ant) => MyNest.Min(cell => PathCost(ant.Cell, cell));

    public float DistanceFromMyNest(Vector2 point) => MyNest.Min(cell => point.DistanceTo(cell));

    /// <summary>Cost for an ant to reach a target in a role, including the walk home if it must switch.</summary>
    public float CostToStart(AntInfo ant, Role role, Vector2I target)
    {
        if (ant.Dead)
            return MyNest.Min(cell => PathCost(cell, target));
        if (ant.Order?.Role == role)
            return PathCost(ant.Cell, target);

        return MyNest.Min(cell => PathCost(ant.Cell, cell) + PathCost(cell, target));
    }

    public static WorldSnapshot Capture(Simulation sim, Team team)
    {
        Team enemy = team == Team.Red ? Team.Blue : Team.Red;

        return new WorldSnapshot
        {
            Team = team,
            ElapsedSeconds = sim.ElapsedSeconds,
            RemainingSeconds = sim.RemainingSeconds,
            MyDeliveries = sim.Deliveries[team],
            EnemyDeliveries = sim.Deliveries[enemy],
            WinTarget = sim.WinTarget,
            MyKills = sim.Kills[team],
            EnemyKills = sim.Kills[enemy],
            MyAnts = sim.Ants.Where(a => a.Team == team).Select(a => Describe(a, true)).ToList(),
            EnemyAnts = sim.Ants.Where(a => a.Team == enemy).Select(a => Describe(a, false)).ToList(),
            Food = sim.Food
                .Where(f => f.Amount > 0)
                .Select(f => new FoodInfo(f.Id, f.Cell, f.Amount, f.StartAmount))
                .ToList(),
            MyNest = sim.Map.Nests[team],
            EnemyNest = sim.Map.Nests[enemy],
            Map = sim.Map,
            Settings = sim.Settings,
            _pathfinder = sim.Pathfinder,
        };
    }

    private static AntInfo Describe(Ant ant, bool own) =>
        new(ant.Id, ant.PreviousPosition, ant.PreviousCell, ant.WasCarrying, ant.IsDead, ant.Order.Role,
            own ? ant.Order : null, own ? ant.PendingOrder : null);
}
