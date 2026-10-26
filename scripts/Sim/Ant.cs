using System.Collections.Generic;
using Godot;

namespace AntFarmer.Sim;

/// <summary>
/// One ant: where it is, where it is walking, and what it is carrying.
/// Plain data plus movement; deciding *what* to do is up to the behavior for its Order.
/// </summary>
public class Ant
{
    public int Id { get; }
    public Team Team { get; }

    public bool CarryingFood { get; set; }
    public FoodPile TargetFood { get; set; }

    /// <summary>The pile the carried food came from. If the ant is killed, the food goes back there.</summary>
    public FoodPile CarriedFrom { get; set; }

    /// <summary>The role this ant is carrying out. Every ant starts as a gatherer.</summary>
    public RoleOrder Order { get; private set; } = RoleOrder.Gather();

    /// <summary>
    /// A new role waiting to take effect. Ants can only change role at their nest, so until it gets
    /// there the ant walks home and keeps Order. Null if no switch is waiting.
    /// </summary>
    public RoleOrder? PendingOrder { get; set; }

    /// <summary>Why the waiting order was given, for the log once the switch happens.</summary>
    public string PendingReason { get; set; }

    /// <summary>The enemy this ant is currently chasing (Defend or Intercept), if any.</summary>
    public Ant ChaseTarget { get; set; }

    /// <summary>Ticks until this ant respawns at its nest after being killed; 0 while alive.</summary>
    public int RespawnTicksLeft { get; set; }

    /// <summary>Ticks until this ant can bite again.</summary>
    public int BiteCooldownLeft { get; set; }

    public bool IsDead => RespawnTicksLeft > 0;

    /// <summary>The last cell the ant fully reached.</summary>
    public Vector2I Cell { get; private set; }

    /// <summary>The cell the ant is walking to, or null if it has no destination.</summary>
    public Vector2I? Destination { get; private set; }

    /// <summary>Exact position in grid units, partway between cells while walking.</summary>
    public Vector2 Position => IsMidStep ? ((Vector2)Cell).Lerp(_path[_pathIndex], _stepProgress) : Cell;

    /// <summary>
    /// Position at the start of the current tick. Other ants sense this ant here, so what they see doesn't
    /// depend on update order; the view also blends from it to smooth movement between ticks.
    /// </summary>
    public Vector2 PreviousPosition { get; set; }

    /// <summary>The cell the ant had reached at the start of the current tick (see PreviousPosition).</summary>
    public Vector2I PreviousCell { get; set; }

    /// <summary>Whether the ant was carrying food at the start of the current tick (see PreviousPosition).</summary>
    public bool WasCarrying { get; set; }

    /// <summary>Direction of the last step, for drawing which way the ant faces.</summary>
    public Vector2 Heading { get; private set; } = Vector2.Right;

    /// <summary>True when the ant is standing on its destination (or has none).</summary>
    public bool Arrived => _pathIndex >= _path.Count;

    private List<Vector2I> _path = new();
    private int _pathIndex;

    /// <summary>How far along the step from Cell to the next path cell, 0 to 1.</summary>
    private float _stepProgress;

    private bool IsMidStep => _pathIndex < _path.Count && _stepProgress > 0f;

    public Ant(int id, Team team, Vector2I cell)
    {
        Id = id;
        Team = team;
        Cell = cell;
        PreviousPosition = cell;
        PreviousCell = cell;
    }

    /// <summary>
    /// Puts a new order into effect right now and clears any waiting one. Does nothing if the order is
    /// unchanged, so an AI can re-send the same orders every decision without resetting what the ant is doing.
    /// The rule that role changes only happen at the nest lives in Simulation.AssignRole.
    /// Returns true if the order changed.
    /// </summary>
    public bool SetOrder(RoleOrder order)
    {
        PendingOrder = null;
        PendingReason = null;
        if (order == Order)
            return false;

        Order = order;
        ChaseTarget = null;

        // Keep any food already picked up; the new behavior will bring it home first.
        if (!CarryingFood)
            TargetFood = null;

        return true;
    }

    /// <summary>
    /// Plans a path to the goal cell. Does nothing if already heading there.
    /// If the ant is between two cells it finishes that step first, so it never jumps backwards.
    /// </summary>
    public void GoTo(Vector2I goal, Pathfinder pathfinder)
    {
        if (Destination == goal)
            return;

        Destination = goal;

        if (IsMidStep)
        {
            Vector2I next = _path[_pathIndex];
            _path = new List<Vector2I> { next };
            _path.AddRange(pathfinder.FindPath(next, goal));
        }
        else
        {
            _path = pathfinder.FindPath(Cell, goal);
            _stepProgress = 0f;
        }

        _pathIndex = 0;
    }

    /// <summary>
    /// Kills the ant: it drops out of the match for the given number of ticks (any food it carried is
    /// handled by the caller). It keeps its order, so it goes back to the same job.
    /// </summary>
    public void Kill(int respawnTicks)
    {
        RespawnTicksLeft = System.Math.Max(1, respawnTicks);
        CarryingFood = false;
        CarriedFrom = null;
        TargetFood = null;
        ChaseTarget = null;
        Stop();
    }

    /// <summary>Brings a dead ant back, standing on the given cell with nothing planned.</summary>
    public void Respawn(Vector2I cell)
    {
        RespawnTicksLeft = 0;
        BiteCooldownLeft = 0;
        Cell = cell;
        PreviousPosition = cell;
        PreviousCell = cell;
        Destination = null;
        _path = new List<Vector2I>();
        _pathIndex = 0;
        _stepProgress = 0f;
    }

    /// <summary>Clears the current destination; the ant stops at the end of its current step.</summary>
    public void Stop()
    {
        Destination = null;
        _path = IsMidStep ? new List<Vector2I> { _path[_pathIndex] } : new List<Vector2I>();
        _pathIndex = 0;
    }

    /// <summary>
    /// Walks along the path, spending the given distance in movement points.
    /// Each step costs Pathfinder.MoveCost, so climbing is slower than flat ground.
    /// </summary>
    public void Advance(float distance, Pathfinder pathfinder)
    {
        while (distance > 0f && _pathIndex < _path.Count)
        {
            Vector2I next = _path[_pathIndex];
            float stepCost = pathfinder.MoveCost(Cell, next);
            float costLeft = (1f - _stepProgress) * stepCost;

            Heading = ((Vector2)(next - Cell)).Normalized();

            if (distance >= costLeft)
            {
                distance -= costLeft;
                Cell = next;
                _pathIndex++;
                _stepProgress = 0f;
            }
            else
            {
                _stepProgress += distance / stepCost;
                distance = 0f;
            }
        }
    }
}
