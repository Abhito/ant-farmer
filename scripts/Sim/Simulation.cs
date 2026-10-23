using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AntFarmer.Sim;

/// <summary>Food left in one pile during a match.</summary>
public class FoodPile
{
    public int Id;
    public Vector2I Cell;
    public int Amount;
    public int StartAmount;
}

/// <summary>
/// The whole game state for one match, advanced one fixed tick at a time by <see cref="Step"/>.
/// It has no nodes and never reads frame time, so a match plays out the same way whether it is
/// drawn at 60 FPS, run at 10x speed, or run headless; only the seed and settings matter.
/// </summary>
public class Simulation
{
    public MapData Map { get; }
    public MatchSettings Settings { get; }
    public Pathfinder Pathfinder { get; }

    /// <summary>The match's only source of randomness. Use this, never Godot's or System's global RNG.</summary>
    public Random Rng { get; }

    public int Tick { get; private set; }
    public float ElapsedSeconds => Tick / (float)Settings.TicksPerSecond;
    public float RemainingSeconds => Mathf.Max(0f, Settings.MatchSeconds - ElapsedSeconds);

    public IReadOnlyList<FoodPile> Food => _food;
    private readonly List<FoodPile> _food;

    public IReadOnlyList<Ant> Ants => _ants;
    private readonly List<Ant> _ants = new();

    public IReadOnlyDictionary<Team, int> Deliveries => _deliveries;
    private readonly Dictionary<Team, int> _deliveries = new() { [Team.Red] = 0, [Team.Blue] = 0 };

    /// <summary>Successful bites landed by each side (each one stole a unit of food from an enemy).</summary>
    public IReadOnlyDictionary<Team, int> Bites => _bites;
    private readonly Dictionary<Team, int> _bites = new() { [Team.Red] = 0, [Team.Blue] = 0 };

    private readonly List<(Ant Biter, Ant Target)> _pendingBites = new();

    /// <summary>Deliveries needed for an instant win (see <see cref="MatchSettings.WinDeliveries"/>).</summary>
    public int WinTarget { get; }

    public bool IsOver { get; private set; }

    /// <summary>The winning side once the match is over, or null for a draw (or while it is still running).</summary>
    public Team? Winner { get; private set; }

    /// <summary>Raised once, on the tick the match ends.</summary>
    public event Action<Simulation> MatchEnded;

    public Simulation(MapData map, MatchSettings settings)
    {
        Map = map;
        Settings = settings;
        Rng = new Random(settings.Seed);
        Pathfinder = new Pathfinder(map, settings.ClimbCost);

        // Copy the food so the map keeps its starting layout and can be reused for the next match.
        _food = map.Food
            .Select(f => new FoodPile { Id = f.Id, Cell = f.Cell, Amount = f.Amount, StartAmount = f.Amount })
            .ToList();

        int totalFood = _food.Sum(f => f.Amount);
        WinTarget = settings.WinDeliveries > 0 ? settings.WinDeliveries : totalFood / 2 + 1;

        SpawnAnts();
    }

    /// <summary>Food lying in piles on the map (not counting food being carried).</summary>
    public int FoodRemaining => _food.Sum(f => f.Amount);

    public Ant AntById(int id) => id >= 0 && id < _ants.Count ? _ants[id] : null;

    public FoodPile FoodById(int id) => _food.FirstOrDefault(f => f.Id == id);

    private int Ticks(float seconds) => Mathf.RoundToInt(seconds * Settings.TicksPerSecond);

    /// <summary>Places each side's ants on its nest cells, taking turns through the cells.</summary>
    private void SpawnAnts()
    {
        foreach (Team team in Enum.GetValues<Team>())
        {
            List<Vector2I> nest = Map.Nests[team];
            for (int i = 0; i < Settings.AntsPerTeam; i++)
                _ants.Add(new Ant(_ants.Count, team, nest[i % nest.Count]));
        }
    }

    /// <summary>Advances the match by one tick.</summary>
    public void Step()
    {
        if (IsOver)
            return;

        Tick++;

        float distancePerTick = Settings.AntSpeed / Settings.TicksPerSecond;

        // Record where everyone starts the tick and who is carrying. Bites are judged on this
        // snapshot, not on whoever happened to move first this tick.
        foreach (Ant ant in _ants)
        {
            ant.PreviousPosition = ant.Position;
            ant.PreviousCell = ant.Cell;
            ant.WasCarrying = ant.CarryingFood;
        }

        // Ants earlier in the update order win ties, like two ants reaching the last unit of food on
        // the same tick. Flip the order every tick so neither side always goes first. The order is
        // still fixed by the tick number, so matches with the same seed replay exactly.
        bool reverse = Tick % 2 == 1;
        for (int i = 0; i < _ants.Count; i++)
        {
            Ant ant = _ants[reverse ? _ants.Count - 1 - i : i];

            if (ant.BiteCooldownLeft > 0)
                ant.BiteCooldownLeft--;

            if (ant.IsStunned)
            {
                ant.StunTicksLeft--;
                continue;
            }

            float speed = ant.CarryingFood ? distancePerTick * Settings.CarrySpeedFactor : distancePerTick;
            ant.Advance(speed, Pathfinder);
            RunBehavior(ant);
        }

        ResolveBites();

        // Only check for a winner once every ant has acted, so two sides that reach the target on
        // the same tick are treated the same no matter who happened to update first.
        CheckForEnd();
    }

    private void RunBehavior(Ant ant)
    {
        switch (ant.Order.Role)
        {
            case Role.Gather:
                GatherBehavior.Update(ant, this);
                break;
            case Role.Defend:
                DefendBehavior.Update(ant, this);
                break;
            case Role.Intercept:
                InterceptBehavior.Update(ant, this);
                break;
        }
    }

    /// <summary>
    /// Gives one of <paramref name="team"/>'s ants a new role. Orders for the other side's ants
    /// (or unknown ids) are ignored, so an AI can only command its own colony.
    /// Returns true if the order was accepted.
    /// </summary>
    public bool AssignRole(Team team, int antId, RoleOrder order)
    {
        Ant ant = AntById(antId);
        if (ant == null || ant.Team != team)
            return false;

        ant.SetOrder(order);
        return true;
    }

    /// <summary>
    /// <paramref name="biter"/> tries to bite <paramref name="target"/>. Allowed if, at the start of this tick,
    /// the target was an enemy carrying food, the biter's hands were free, and they were within
    /// <see cref="MatchSettings.BiteRange"/>, and the biter's cooldown is over and it isn't stunned.
    /// Returns true if the bite was declared. All bites resolve together at the end of the tick
    /// (see <see cref="ResolveBites"/>), so the result doesn't depend on which ant updated first.
    /// </summary>
    public bool TryBite(Ant biter, Ant target)
    {
        if (IsOver || target == null || biter.Team == target.Team)
            return false;
        if (biter.IsStunned || biter.BiteCooldownLeft > 0 || biter.CarryingFood || biter.WasCarrying)
            return false;
        if (!target.WasCarrying)
            return false;
        if (biter.PreviousPosition.DistanceTo(target.PreviousPosition) > Settings.BiteRange)
            return false;

        _pendingBites.Add((biter, target));
        return true;
    }

    /// <summary>
    /// Applies this tick's bites: the biter steals the target's food (and will carry it to its own nest),
    /// and the target is stunned for <see cref="MatchSettings.StunSeconds"/>. If several ants bit the same
    /// target, the first in the list gets the food; they are all on the same side, so it doesn't change the score.
    /// A bite misses if the target delivered its food this tick.
    /// </summary>
    private void ResolveBites()
    {
        foreach ((Ant biter, Ant target) in _pendingBites)
        {
            if (!target.CarryingFood)
                continue;

            biter.BiteCooldownLeft = Ticks(Settings.BiteCooldownSeconds);
            _bites[biter.Team]++;

            target.CarryingFood = false;
            target.TargetFood = null;
            target.StunTicksLeft = Ticks(Settings.StunSeconds);
            target.State = AntState.Stunned;
            target.Stop();

            biter.CarryingFood = true;
            biter.TargetFood = null;
            biter.ChaseTarget = null;
        }

        _pendingBites.Clear();
    }

    /// <summary>Takes one unit of food from a pile. Returns false if the pile is empty.</summary>
    public bool TakeFood(FoodPile pile)
    {
        if (IsOver || pile.Amount <= 0)
            return false;

        pile.Amount--;
        return true;
    }

    /// <summary>Records one unit of food delivered to a team's nest.</summary>
    public void Deliver(Team team)
    {
        if (IsOver)
            return;

        _deliveries[team]++;
    }

    private void CheckForEnd()
    {
        // If both sides reached the target this tick, the higher count wins; equal counts are a draw.
        if (_deliveries.Values.Any(count => count >= WinTarget))
        {
            End(LeadingTeam());
            return;
        }

        // Once the map is empty and nobody is carrying food, the score can't change any more.
        bool noFoodLeft = FoodRemaining == 0 && !_ants.Any(a => a.CarryingFood);

        if (Tick >= Settings.MatchTicks || noFoodLeft)
            End(LeadingTeam());
    }

    private Team? LeadingTeam()
    {
        int red = _deliveries[Team.Red];
        int blue = _deliveries[Team.Blue];
        if (red == blue) return null;
        return red > blue ? Team.Red : Team.Blue;
    }

    private void End(Team? winner)
    {
        IsOver = true;
        Winner = winner;
        MatchEnded?.Invoke(this);
    }
}
