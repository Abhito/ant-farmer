using System;
using System.Collections.Generic;
using System.Linq;
using AntFarmer.Sim.Ai;
using Godot;

namespace AntFarmer.Sim;

/// <summary>
/// The full game state for one match, advanced one fixed tick at a time. It never reads frame time,
/// so a match plays out the same whether drawn, sped up, or run headless.
/// </summary>
public class Simulation
{
    public MapData Map { get; }
    public MatchSettings Settings { get; }
    public Pathfinder Pathfinder { get; }

    // Mirrored routes can differ in the last decimal places, so costs this close count as a tie.
    public const float TieTolerance = 0.001f;

    public int Tick { get; private set; }
    public float ElapsedSeconds => Tick / (float)Settings.TicksPerSecond;
    public float RemainingSeconds => Mathf.Max(0f, Settings.MatchSeconds - ElapsedSeconds);

    public IReadOnlyList<FoodPile> Food => _food;
    private readonly List<FoodPile> _food;

    public IReadOnlyList<Ant> Ants => _ants;
    private readonly List<Ant> _ants = new();

    public IReadOnlyDictionary<Team, int> Deliveries => _deliveries;
    private readonly Dictionary<Team, int> _deliveries = new() { [Team.Red] = 0, [Team.Blue] = 0 };

    public IReadOnlyDictionary<Team, int> Kills => _kills;
    private readonly Dictionary<Team, int> _kills = new() { [Team.Red] = 0, [Team.Blue] = 0 };

    // Used to rotate respawns through the nest cells.
    private readonly Dictionary<Team, int> _respawns = new() { [Team.Red] = 0, [Team.Blue] = 0 };

    private readonly List<(Ant Biter, Ant Target)> _pendingBites = new();

    public IReadOnlyDictionary<Team, int> RoleChanges => _roleChanges;
    private readonly Dictionary<Team, int> _roleChanges = new() { [Team.Red] = 0, [Team.Blue] = 0 };

    private readonly Dictionary<Team, ITeamController> _controllers = new();

    // Fires when an ant's order actually changes, with the AI's reason.
    public event Action<Simulation, Ant, string> RoleChanged;

    // Fires when an ant is given a new role away from its nest and starts walking home.
    public event Action<Simulation, Ant, string> RoleQueued;

    public int WinTarget => Settings.WinDeliveries;

    // Rolled from the seed. Piles near a nest regrow slower than RegrowSeconds.
    public int PileSize { get; }
    public float RegrowSeconds { get; }

    public bool IsOver { get; private set; }

    // Null for a draw or while the match is running.
    public Team? Winner { get; private set; }

    public event Action<Simulation> MatchEnded;

    public Simulation(MapData map, MatchSettings settings)
    {
        Map = map;
        Settings = settings;
        Pathfinder = new Pathfinder(map, settings.ClimbCost);

        var rng = new Random(settings.Seed);
        PileSize = rng.Next(settings.MinPileSize, settings.MaxPileSize + 1);
        RegrowSeconds = Mathf.Lerp(settings.MinRegrowSeconds, settings.MaxRegrowSeconds, (float)rng.NextDouble());

        // Copy the food so the map can be reused for the next match.
        _food = map.Food
            .Select(f => new FoodPile { Id = f.Id, Cell = f.Cell, Amount = PileSize, StartAmount = PileSize })
            .ToList();
        SetRegrowTimes();

        SpawnAnts();
    }

    // Scales each pile's regrow time from the base (farthest from any nest) up to NearNestRegrowFactor (closest).
    private void SetRegrowTimes()
    {
        float NestDistance(FoodPile pile) => Enum.GetValues<Team>().Min(team => DistanceFromNest(team, pile.Cell));
        float nearest = _food.Min(NestDistance);
        float farthest = _food.Max(NestDistance);

        foreach (FoodPile pile in _food)
        {
            float closeness = farthest > nearest ? (farthest - NestDistance(pile)) / (farthest - nearest) : 0f;
            pile.RegrowSeconds = RegrowSeconds * Mathf.Lerp(1f, Settings.NearNestRegrowFactor, closeness);
        }
    }

    // Food still in piles, not counting food being carried.
    public int FoodRemaining => _food.Sum(f => f.Amount);

    public Ant AntById(int id) => id >= 0 && id < _ants.Count ? _ants[id] : null;

    public FoodPile FoodById(int id) => _food.FirstOrDefault(f => f.Id == id);

    private int Ticks(float seconds) => Mathf.RoundToInt(seconds * Settings.TicksPerSecond);

    private void SpawnAnts()
    {
        foreach (Team team in Enum.GetValues<Team>())
        {
            List<Vector2I> nest = Map.Nests[team];
            for (int i = 0; i < Settings.AntsPerTeam; i++)
                _ants.Add(new Ant(_ants.Count, team, nest[i % nest.Count]));
        }
    }

    public void Step()
    {
        if (IsOver)
            return;

        Tick++;

        float distancePerTick = Settings.AntSpeed / Settings.TicksPerSecond;

        foreach (FoodPile pile in _food)
        {
            if (pile.IsRegrowing && --pile.RegrowTicksLeft == 0)
                pile.Amount = Math.Max(pile.Amount, pile.StartAmount);
        }

        foreach (Ant ant in _ants)
        {
            if (ant.IsDead && --ant.RespawnTicksLeft == 0)
                ant.Respawn(NextSpawnCell(ant.Team));
        }

        // Ants sense each other from these start-of-tick values, so update order doesn't matter.
        foreach (Ant ant in _ants)
        {
            ant.PreviousPosition = ant.Position;
            ant.PreviousCell = ant.Cell;
            ant.WasCarrying = ant.CarryingFood;
        }

        if ((Tick - 1) % Settings.DecisionTicks == 0)
            RunControllers();

        // Earlier ants win ties (like the last unit of food), so flip the order each tick.
        bool reverse = Tick % 2 == 1;
        for (int i = 0; i < _ants.Count; i++)
        {
            Ant ant = _ants[reverse ? _ants.Count - 1 - i : i];

            if (ant.IsDead)
                continue;

            if (ant.BiteCooldownLeft > 0)
                ant.BiteCooldownLeft--;

            float speed = distancePerTick * Settings.SpeedFactor(ant.Order.Role)
                          * (ant.CarryingFood ? Settings.CarrySpeedFactor : 1f);
            ant.Advance(speed, Pathfinder);

            if (ant.PendingOrder != null && IsAtNest(ant))
                ApplyPendingOrder(ant);

            if (ant.PendingOrder != null)
                GatherBehavior.ReturnHome(ant, this);
            else
                RunBehavior(ant);
        }

        ResolveBites();
        CheckForEnd();
    }

    // Same center-out order as the starting spawn, so mirrored sides respawn on mirrored cells.
    private Vector2I NextSpawnCell(Team team)
    {
        List<Vector2I> nest = Map.Nests[team];
        return nest[_respawns[team]++ % nest.Count];
    }

    private void RunBehavior(Ant ant)
    {
        // Safety net: attackers should never be holding food.
        if (ant.CarryingFood && ant.Order.Role != Role.Gather)
        {
            GatherBehavior.CarryHome(ant, this);
            return;
        }

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

    // Null leaves the side's ants on their current orders.
    public void SetController(Team team, ITeamController controller)
    {
        if (controller == null)
            _controllers.Remove(team);
        else
            _controllers[team] = controller;
    }

    public ITeamController Controller(Team team) => _controllers.GetValueOrDefault(team);

    private void RunControllers()
    {
        // Take both snapshots first so neither side sees the other's new orders.
        var snapshots = _controllers.Keys.ToDictionary(team => team, team => WorldSnapshot.Capture(this, team));

        foreach (Team team in Enum.GetValues<Team>())
        {
            if (!_controllers.TryGetValue(team, out ITeamController controller))
                continue;

            foreach (AntCommand command in controller.Decide(snapshots[team]))
                AssignRole(team, command.AntId, command.Order, command.Reason);
        }
    }

    /// <summary>
    /// Gives an ant a new order. Roles only change at the nest: a new role for an ant away from home
    /// is queued while it walks back. Same-role orders, and orders for dead ants, apply now.
    /// </summary>
    public bool AssignRole(Team team, int antId, RoleOrder order, string reason = null)
    {
        Ant ant = AntById(antId);
        if (ant == null || ant.Team != team)
            return false;

        if (order.Role == ant.Order.Role || ant.IsDead || IsAtNest(ant))
        {
            ApplyOrder(ant, order, reason);
        }
        else if (ant.PendingOrder != order)
        {
            ant.PendingOrder = order;
            ant.PendingReason = reason;
            RoleQueued?.Invoke(this, ant, reason);
        }

        return true;
    }

    public bool IsAtNest(Ant ant) => Map.Nests[ant.Team].Contains(ant.Cell);

    public float DistanceFromNest(Team team, Vector2 point) => Map.Nests[team].Min(cell => point.DistanceTo(cell));

    private void ApplyPendingOrder(Ant ant)
    {
        if (ant.PendingOrder is not RoleOrder order)
            return;

        ApplyOrder(ant, order, ant.PendingReason);
    }

    private void ApplyOrder(Ant ant, RoleOrder order, string reason)
    {
        // Hand in any food before becoming an attacker.
        if (order.Role != Role.Gather && ant.CarryingFood)
            DeliverCarried(ant);

        if (ant.SetOrder(order))
        {
            _roleChanges[ant.Team]++;
            RoleChanged?.Invoke(this, ant, reason);
        }
    }

    // Interceptors attack carriers. Defenders attack carriers and interceptors. Gatherers never attack.
    public static bool CanAttack(Ant biter, Ant target) => biter.Order.Role switch
    {
        Role.Intercept => target.WasCarrying,
        Role.Defend => target.WasCarrying || target.Order.Role == Role.Intercept,
        _ => false,
    };

    /// <summary>Declares a bite if it's allowed. All bites resolve together at the end of the tick.</summary>
    public bool TryBite(Ant biter, Ant target)
    {
        if (IsOver || target == null || biter.Team == target.Team)
            return false;
        if (biter.IsDead || target.IsDead || biter.BiteCooldownLeft > 0 || biter.CarryingFood || biter.WasCarrying)
            return false;
        if (!CanAttack(biter, target))
            return false;
        if (biter.PreviousPosition.DistanceTo(target.PreviousPosition) > Settings.BiteRange)
            return false;

        _pendingBites.Add((biter, target));
        return true;
    }

    // Each bitten ant dies and its food goes back to its pile. A biter killed this tick still lands its bite.
    private void ResolveBites()
    {
        foreach ((Ant biter, Ant target) in _pendingBites)
        {
            if (target.IsDead)
                continue;

            biter.BiteCooldownLeft = Ticks(Settings.BiteCooldownSeconds);
            biter.Kills++;
            _kills[biter.Team]++;

            if (target.CarryingFood && target.CarriedFrom != null)
                target.CarriedFrom.Amount++;

            target.Kill(Ticks(Settings.RespawnSeconds));

            // It respawns at the nest, so a waiting switch can happen now.
            ApplyPendingOrder(target);
        }

        _pendingBites.Clear();
    }

    public bool TakeFood(FoodPile pile)
    {
        if (IsOver || pile.Amount <= 0)
            return false;

        pile.Amount--;
        if (pile.Amount == 0 && !pile.IsRegrowing)
            pile.RegrowTicksLeft = Ticks(pile.RegrowSeconds);

        return true;
    }

    public void DeliverCarried(Ant ant)
    {
        if (!ant.CarryingFood)
            return;

        ant.CarryingFood = false;
        ant.CarriedFrom = null;
        ant.TargetFood = null;
        if (!IsOver)
            _deliveries[ant.Team]++;
    }

    private void CheckForEnd()
    {
        if (_deliveries.Values.Any(count => count >= WinTarget) || Tick >= Settings.MatchTicks)
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
