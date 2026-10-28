using System.Collections.Generic;
using Godot;

namespace AntFarmer.Sim;

/// <summary>One ant: position, path, and what it carries. Its behavior decides what it does.</summary>
public class Ant
{
    public int Id { get; }
    public Team Team { get; }

    public bool CarryingFood { get; set; }
    public FoodPile TargetFood { get; set; }

    // Where the carried food goes back to if the ant is killed.
    public FoodPile CarriedFrom { get; set; }

    public RoleOrder Order { get; private set; } = RoleOrder.Gather();

    // A role switch waiting until the ant reaches its nest.
    public RoleOrder? PendingOrder { get; set; }
    public string PendingReason { get; set; }

    public Ant ChaseTarget { get; set; }

    public int RespawnTicksLeft { get; set; }
    public int BiteCooldownLeft { get; set; }
    public int Kills { get; set; }

    public bool IsDead => RespawnTicksLeft > 0;

    // The last cell the ant fully reached.
    public Vector2I Cell { get; private set; }
    public Vector2I? Destination { get; private set; }

    public Vector2 Position => IsMidStep ? ((Vector2)Cell).Lerp(_path[_pathIndex], _stepProgress) : Cell;

    // State at the start of the tick. Other ants sense these, so update order doesn't matter.
    public Vector2 PreviousPosition { get; set; }
    public Vector2I PreviousCell { get; set; }
    public bool WasCarrying { get; set; }

    public Vector2 Heading { get; private set; } = Vector2.Right;

    public bool Arrived => _pathIndex >= _path.Count;

    private List<Vector2I> _path = new();
    private int _pathIndex;
    private float _stepProgress; // 0 to 1 along the step from Cell to the next path cell

    private bool IsMidStep => _pathIndex < _path.Count && _stepProgress > 0f;

    public Ant(int id, Team team, Vector2I cell)
    {
        Id = id;
        Team = team;
        Cell = cell;
        PreviousPosition = cell;
        PreviousCell = cell;
    }

    /// <summary>Applies an order now and clears any waiting one. Returns true if the order changed.</summary>
    public bool SetOrder(RoleOrder order)
    {
        PendingOrder = null;
        PendingReason = null;
        if (order == Order)
            return false;

        Order = order;
        ChaseTarget = null;

        if (!CarryingFood)
            TargetFood = null;

        return true;
    }

    /// <summary>Plans a path to the goal, finishing any half-taken step first.</summary>
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

    /// <summary>Removes the ant for a while. It keeps its order; the caller handles carried food.</summary>
    public void Kill(int respawnTicks)
    {
        RespawnTicksLeft = System.Math.Max(1, respawnTicks);
        CarryingFood = false;
        CarriedFrom = null;
        TargetFood = null;
        ChaseTarget = null;
        Stop();
    }

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

    // The ant stops at the end of its current step.
    public void Stop()
    {
        Destination = null;
        _path = IsMidStep ? new List<Vector2I> { _path[_pathIndex] } : new List<Vector2I>();
        _pathIndex = 0;
    }

    /// <summary>Walks along the path. Climbing steps cost more, so the ant covers less ground.</summary>
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
