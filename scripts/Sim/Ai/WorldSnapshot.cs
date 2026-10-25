using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AntFarmer.Sim.Ai;

/// <summary>
/// What a team's AI knows about one ant. <see cref="Order"/> is only filled in for the AI's own ants:
/// an enemy's position and load can be seen, but not the job it was given.
/// </summary>
public readonly record struct AntInfo(int Id, Vector2 Position, Vector2I Cell, bool Carrying, bool Stunned, RoleOrder? Order);

/// <summary>What a team's AI knows about one food pile.</summary>
public readonly record struct FoodInfo(int Id, Vector2I Cell, int Amount, int StartAmount);

/// <summary>
/// Everything a team's AI is allowed to see when it decides, as of the start of the current tick.
/// It is a copy: changing it does nothing, and the only way an AI can affect the match is through the
/// orders it returns from <see cref="ITeamController.Decide"/>. Both sides' snapshots are taken before
/// either side's orders are applied, so neither side sees the other's newest orders first.
/// </summary>
public class WorldSnapshot
{
    /// <summary>The side this snapshot was taken for.</summary>
    public Team Team { get; private init; }
    public Team Enemy => Team == Team.Red ? Team.Blue : Team.Red;

    public int Tick { get; private init; }
    public float ElapsedSeconds { get; private init; }
    public float RemainingSeconds { get; private init; }

    public int MyDeliveries { get; private init; }
    public int EnemyDeliveries { get; private init; }

    /// <summary>Deliveries needed for an instant win.</summary>
    public int WinTarget { get; private init; }

    /// <summary>Bites landed by each side so far (each one stole a unit of food).</summary>
    public int MyBites { get; private init; }
    public int EnemyBites { get; private init; }

    public IReadOnlyList<AntInfo> MyAnts { get; private init; }
    public IReadOnlyList<AntInfo> EnemyAnts { get; private init; }

    /// <summary>Piles that still have food.</summary>
    public IReadOnlyList<FoodInfo> Food { get; private init; }

    /// <summary>Food lying in piles (not counting food being carried).</summary>
    public int FoodRemaining { get; private init; }

    public IReadOnlyList<Vector2I> MyNest { get; private init; }
    public IReadOnlyList<Vector2I> EnemyNest { get; private init; }

    /// <summary>The static map (terrain and heights). It never changes during a match.</summary>
    public MapData Map { get; private init; }

    public MatchSettings Settings { get; private init; }

    private Pathfinder _pathfinder;

    public int ScoreDifference => MyDeliveries - EnemyDeliveries;
    public int MyCarriers => MyAnts.Count(a => a.Carrying);
    public int EnemyCarriers => EnemyAnts.Count(a => a.Carrying);

    /// <summary>
    /// Cost to walk from <paramref name="from"/> to <paramref name="to"/>, the same measure the ants use
    /// (climbing costs extra). Infinity if there is no path.
    /// </summary>
    public float PathCost(Vector2I from, Vector2I to)
    {
        if (from == to)
            return 0f;

        List<Vector2I> path = _pathfinder.FindPath(from, to);
        return path.Count == 0 ? float.PositiveInfinity : _pathfinder.PathCost(from, path);
    }

    /// <summary>Builds the snapshot <paramref name="team"/>'s AI gets this tick.</summary>
    public static WorldSnapshot Capture(Simulation sim, Team team)
    {
        Team enemy = team == Team.Red ? Team.Blue : Team.Red;

        return new WorldSnapshot
        {
            Team = team,
            Tick = sim.Tick,
            ElapsedSeconds = sim.ElapsedSeconds,
            RemainingSeconds = sim.RemainingSeconds,
            MyDeliveries = sim.Deliveries[team],
            EnemyDeliveries = sim.Deliveries[enemy],
            WinTarget = sim.WinTarget,
            MyBites = sim.Bites[team],
            EnemyBites = sim.Bites[enemy],
            MyAnts = sim.Ants.Where(a => a.Team == team).Select(a => Describe(a, true)).ToList(),
            EnemyAnts = sim.Ants.Where(a => a.Team == enemy).Select(a => Describe(a, false)).ToList(),
            Food = sim.Food
                .Where(f => f.Amount > 0)
                .Select(f => new FoodInfo(f.Id, f.Cell, f.Amount, f.StartAmount))
                .ToList(),
            FoodRemaining = sim.FoodRemaining,
            MyNest = sim.Map.Nests[team],
            EnemyNest = sim.Map.Nests[enemy],
            Map = sim.Map,
            Settings = sim.Settings,
            _pathfinder = sim.Pathfinder,
        };
    }

    // Uses the start-of-tick fields, the same view of each other the ants themselves get.
    private static AntInfo Describe(Ant ant, bool own) =>
        new(ant.Id, ant.PreviousPosition, ant.PreviousCell, ant.WasCarrying, ant.IsStunned, own ? ant.Order : null);
}
