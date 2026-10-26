using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AntFarmer.Sim.Ai;

/// <summary>
/// What a team's AI knows about one ant. Every ant's Role is visible (it shows on screen as a
/// ring), but Order, with its target pile, post or ant, is only filled in for the AI's own ants,
/// and so is PendingOrder: a new role the ant is walking home to switch to.
/// A dead ant is waiting to respawn at its nest; its position is where it died.
/// </summary>
public readonly record struct AntInfo(int Id, Vector2 Position, Vector2I Cell, bool Carrying, bool Dead, Role Role,
    RoleOrder? Order, RoleOrder? PendingOrder = null)
{
    /// <summary>The order this ant will be following once any waiting switch has happened (own ants only).</summary>
    public RoleOrder? NextOrder => PendingOrder ?? Order;
}

/// <summary>What a team's AI knows about one food pile.</summary>
public readonly record struct FoodInfo(int Id, Vector2I Cell, int Amount, int StartAmount);

/// <summary>
/// Everything a team's AI is allowed to see when it decides, as of the start of the current tick.
/// The ant and food lists are copies; the map, nests and settings are shared and must not be changed.
/// The only way an AI can affect the match is through the orders it returns from ITeamController.Decide. Both sides' snapshots are taken before
/// either side's orders are applied, so neither side sees the other's newest orders first.
/// </summary>
public class WorldSnapshot
{
    /// <summary>The side this snapshot was taken for.</summary>
    public Team Team { get; private init; }
    public Team Enemy => Team == Team.Red ? Team.Blue : Team.Red;

    public float ElapsedSeconds { get; private init; }
    public float RemainingSeconds { get; private init; }

    public int MyDeliveries { get; private init; }
    public int EnemyDeliveries { get; private init; }

    /// <summary>Deliveries needed for an instant win.</summary>
    public int WinTarget { get; private init; }

    /// <summary>Enemy ants killed by each side so far.</summary>
    public int MyKills { get; private init; }
    public int EnemyKills { get; private init; }

    public IReadOnlyList<AntInfo> MyAnts { get; private init; }
    public IReadOnlyList<AntInfo> EnemyAnts { get; private init; }

    /// <summary>Piles that still have food.</summary>
    public IReadOnlyList<FoodInfo> Food { get; private init; }


    public IReadOnlyList<Vector2I> MyNest { get; private init; }
    public IReadOnlyList<Vector2I> EnemyNest { get; private init; }

    /// <summary>The static map (terrain and heights). It never changes during a match.</summary>
    public MapData Map { get; private init; }

    public MatchSettings Settings { get; private init; }

    private Pathfinder _pathfinder;

    // Path costs already worked out during this decision; rules often ask for the same ones.
    private readonly Dictionary<(Vector2I, Vector2I), float> _pathCosts = new();

    public int ScoreDifference => MyDeliveries - EnemyDeliveries;
    public int MyCarriers => MyAnts.Count(a => a.Carrying);
    public int EnemyCarriers => EnemyAnts.Count(a => a.Carrying);

    /// <summary>
    /// Cost to walk between two cells, the same measure the ants use
    /// (climbing costs extra). Infinity if there is no path.
    /// </summary>
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

    /// <summary>Cost for one of our ants to walk to the nearest cell of our nest.</summary>
    public float CostHome(AntInfo ant) => MyNest.Min(cell => PathCost(ant.Cell, cell));

    /// <summary>Straight-line distance from point to the nearest cell of our nest.</summary>
    public float DistanceFromMyNest(Vector2 point) => MyNest.Min(cell => point.DistanceTo(cell));

    /// <summary>
    /// Cost for one of our ants to reach a target cell in the given role. An ant already
    /// in that role can go straight there; any other ant must first walk home, since roles only change at
    /// the nest. A dead ant will respawn at the nest, so it starts from there. Rules use this to prefer
    /// ants that can start a job soonest.
    /// </summary>
    public float CostToStart(AntInfo ant, Role role, Vector2I target)
    {
        if (ant.Dead)
            return MyNest.Min(cell => PathCost(cell, target));
        if (ant.Order?.Role == role)
            return PathCost(ant.Cell, target);

        return MyNest.Min(cell => PathCost(ant.Cell, cell) + PathCost(cell, target));
    }

    /// <summary>Builds the snapshot one side's AI gets this tick.</summary>
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

    // Uses the start-of-tick fields, the same view of each other the ants themselves get.
    private static AntInfo Describe(Ant ant, bool own) =>
        new(ant.Id, ant.PreviousPosition, ant.PreviousCell, ant.WasCarrying, ant.IsDead, ant.Order.Role,
            own ? ant.Order : null, own ? ant.PendingOrder : null);
}
