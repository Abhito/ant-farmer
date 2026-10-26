using System;
using System.Collections.Generic;
using System.Linq;
using AntFarmer.Sim.Ai;
using Godot;

namespace AntFarmer.Sim;

/// <summary>
/// The whole game state for one match, advanced one fixed tick at a time by Step.
/// It has no nodes and never reads frame time, so a match plays out the same way whether it is
/// drawn at 60 FPS, run at 10x speed, or run headless; only the seed and settings matter.
/// </summary>
public class Simulation
{
    public MapData Map { get; }
    public MatchSettings Settings { get; }
    public Pathfinder Pathfinder { get; }

    /// <summary>
    /// Path costs or distances closer than this count as a tie. Mirrored routes can add up their steps in a
    /// different order and differ in the last decimal places, so ties are broken some other fair way
    /// (straight-line distance, then ant id) instead of by which one happens to come first in a list.
    /// </summary>
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

    /// <summary>Enemy ants killed by each side's bites.</summary>
    public IReadOnlyDictionary<Team, int> Kills => _kills;
    private readonly Dictionary<Team, int> _kills = new() { [Team.Red] = 0, [Team.Blue] = 0 };

    // How many ants each side has respawned, used to rotate through the nest cells.
    private readonly Dictionary<Team, int> _respawns = new() { [Team.Red] = 0, [Team.Blue] = 0 };

    private readonly List<(Ant Biter, Ant Target)> _pendingBites = new();

    /// <summary>Times each side's AI switched an ant to a different order.</summary>
    public IReadOnlyDictionary<Team, int> RoleChanges => _roleChanges;
    private readonly Dictionary<Team, int> _roleChanges = new() { [Team.Red] = 0, [Team.Blue] = 0 };

    private readonly Dictionary<Team, ITeamController> _controllers = new();

    /// <summary>
    /// Raised whenever an ant's order actually changes (not when the same order is re-sent), with the
    /// reason the AI gave, if any. For a new role this is when the ant reaches its nest, not when it was ordered.
    /// </summary>
    public event Action<Simulation, Ant, string> RoleChanged;

    /// <summary>Raised when an ant is ordered into a new role away from its nest and starts walking home to switch.</summary>
    public event Action<Simulation, Ant, string> RoleQueued;

    /// <summary>Deliveries needed for an instant win (see MatchSettings.WinDeliveries).</summary>
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

        // Bring back ants whose respawn time is up, before anyone looks at the board. Ants only die
        // (end of tick) and respawn (start of tick) between ticks, so who is alive never changes mid-tick.
        foreach (Ant ant in _ants)
        {
            if (ant.IsDead && --ant.RespawnTicksLeft == 0)
                ant.Respawn(NextSpawnCell(ant.Team));
        }

        // Record where everyone starts the tick and who is carrying. Bites are judged on this
        // snapshot, not on whoever happened to move first this tick.
        foreach (Ant ant in _ants)
        {
            ant.PreviousPosition = ant.Position;
            ant.PreviousCell = ant.Cell;
            ant.WasCarrying = ant.CarryingFood;
        }

        // The AIs decide on the first tick and then every DecisionTicks, before any ant moves.
        if ((Tick - 1) % Settings.DecisionTicks == 0)
            RunControllers();

        // Ants earlier in the update order win ties, like two ants reaching the last unit of food on
        // the same tick. Flip the order every tick so neither side always goes first. The order is
        // still fixed by the tick number, so matches with the same seed replay exactly.
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

            // A waiting role switch happens once the ant reaches its nest; until then it walks home.
            if (ant.PendingOrder != null && IsAtNest(ant))
                ApplyPendingOrder(ant);

            if (ant.PendingOrder != null)
                GatherBehavior.ReturnHome(ant, this);
            else
                RunBehavior(ant);
        }

        ResolveBites();

        // Only check for a winner once every ant has acted, so two sides that reach the target on
        // the same tick are treated the same no matter who happened to update first.
        CheckForEnd();
    }

    /// <summary>
    /// The nest cell for a side's next respawn. Cycles through the nest cells in the same center-out
    /// order as the starting spawn, so mirrored sides respawn on mirrored cells.
    /// </summary>
    private Vector2I NextSpawnCell(Team team)
    {
        List<Vector2I> nest = Map.Nests[team];
        return nest[_respawns[team]++ % nest.Count];
    }

    private void RunBehavior(Ant ant)
    {
        // Attackers never hold food: ants hand it in before switching away from gathering. This is only a
        // safety net, so no attacker (including any role added later) ever wanders around with food.
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

    /// <summary>Puts a controller in charge of one side. Null leaves its ants on their current orders.</summary>
    public void SetController(Team team, ITeamController controller)
    {
        if (controller == null)
            _controllers.Remove(team);
        else
            _controllers[team] = controller;
    }

    public ITeamController Controller(Team team) => _controllers.GetValueOrDefault(team);

    /// <summary>
    /// Asks each side's AI for orders. Both snapshots are taken before either side's orders are
    /// applied, so the side that happens to be asked first gains nothing.
    /// </summary>
    private void RunControllers()
    {
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
    /// Gives one of a side's ants a new order. Orders for the other side's ants
    /// (or unknown ids) are ignored, so an AI can only command its own colony.
    ///
    /// Ants can only change role at their nest. A new target for the same role (another pile, post or
    /// enemy) takes effect at once, and so does any order for an ant that is at its nest or dead (it respawns
    /// there). Otherwise the order waits in Ant.PendingOrder while the ant walks home; a later
    /// order replaces it, and re-sending the ant's current order cancels the switch.
    ///
    /// Returns true if the order was accepted. The reason is only passed on to the events.
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

    /// <summary>True if the last cell the ant reached is one of its own nest cells.</summary>
    public bool IsAtNest(Ant ant) => Map.Nests[ant.Team].Contains(ant.Cell);

    /// <summary>Straight-line distance from a point to the nearest cell of a side's nest.</summary>
    public float DistanceFromNest(Team team, Vector2 point) => Map.Nests[team].Min(cell => point.DistanceTo(cell));

    private void ApplyPendingOrder(Ant ant)
    {
        if (ant.PendingOrder is not RoleOrder order)
            return;

        ApplyOrder(ant, order, ant.PendingReason);
    }

    private void ApplyOrder(Ant ant, RoleOrder order, string reason)
    {
        // Attackers never hold food. A role change away from gathering only happens at the nest (or while
        // dead, carrying nothing), so a carrying ant hands its food in before it switches.
        if (order.Role != Role.Gather && ant.CarryingFood)
            DeliverCarried(ant);

        if (ant.SetOrder(order))
        {
            _roleChanges[ant.Team]++;
            RoleChanged?.Invoke(this, ant, reason);
        }
    }

    /// <summary>
    /// Whether the biter may attack the target at all, going by their roles:
    /// interceptors attack enemy carriers; defenders attack enemy carriers and enemy interceptors;
    /// gatherers never attack. Carrying is judged at the start of the tick.
    /// </summary>
    public static bool CanAttack(Ant biter, Ant target) => biter.Order.Role switch
    {
        Role.Intercept => target.WasCarrying,
        Role.Defend => target.WasCarrying || target.Order.Role == Role.Intercept,
        _ => false,
    };

    /// <summary>
    /// The biter tries to bite the target. Allowed if both are alive, the biter's
    /// role lets it attack the target (CanAttack), the biter's hands were free, they were within bite range
    /// (MatchSettings.BiteRange) at the start of the tick, and the biter's cooldown is over.
    /// Returns true if the bite was declared. All bites resolve together at the end of the tick
    /// (see ResolveBites), so the result doesn't depend on which ant updated first.
    /// </summary>
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

    /// <summary>
    /// Applies this tick's bites: each target is killed and respawns at its nest a few seconds later
    /// (MatchSettings.RespawnSeconds). Food it was carrying goes back to the pile it came from;
    /// attackers never pick it up. A biter killed this same tick still lands its bite, since every bite
    /// was declared from the same start-of-tick positions.
    /// </summary>
    private void ResolveBites()
    {
        foreach ((Ant biter, Ant target) in _pendingBites)
        {
            // Another ant already killed this target this tick.
            if (target.IsDead)
                continue;

            biter.BiteCooldownLeft = Ticks(Settings.BiteCooldownSeconds);
            _kills[biter.Team]++;

            if (target.CarryingFood && target.CarriedFrom != null)
                target.CarriedFrom.Amount++;

            target.Kill(Ticks(Settings.RespawnSeconds));

            // It respawns at the nest, so a waiting role switch can happen now.
            ApplyPendingOrder(target);
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

    /// <summary>The ant hands in the food it is carrying: one delivery for its team.</summary>
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
