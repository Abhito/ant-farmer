using System.Collections.Generic;
using Godot;

namespace AntFarmer.Sim;

public enum AntState
{
    /// <summary>Nothing to do (e.g. no food left on the map).</summary>
    Idle,
    GoingToFood,
    GoingHome,

    /// <summary>Defending: standing at (or walking back to) its post.</summary>
    Guarding,

    /// <summary>Defending or intercepting: running after an enemy ant.</summary>
    Chasing,

    /// <summary>Bitten recently and can't move or act until the stun wears off.</summary>
    Stunned,
}

/// <summary>
/// One ant: where it is, where it is walking, and what it is carrying.
/// Plain data plus movement; deciding *what* to do is up to the behavior for its <see cref="Order"/>.
/// </summary>
public class Ant
{
    public int Id { get; }
    public Team Team { get; }

    public AntState State { get; set; } = AntState.Idle;
    public bool CarryingFood { get; set; }
    public FoodPile TargetFood { get; set; }

    /// <summary>The role this ant is carrying out. Every ant starts as a gatherer.</summary>
    public RoleOrder Order { get; private set; } = RoleOrder.Gather();

    /// <summary>The enemy this ant is currently chasing (Defend or Intercept), if any.</summary>
    public Ant ChaseTarget { get; set; }

    /// <summary>Ticks until this ant can move again after being bitten.</summary>
    public int StunTicksLeft { get; set; }

    /// <summary>Ticks until this ant can bite again.</summary>
    public int BiteCooldownLeft { get; set; }

    public bool IsStunned => StunTicksLeft > 0;

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

    /// <summary>The cell the ant had reached at the start of the current tick (see <see cref="PreviousPosition"/>).</summary>
    public Vector2I PreviousCell { get; set; }

    /// <summary>Whether the ant was carrying food at the start of the current tick (see <see cref="PreviousPosition"/>).</summary>
    public bool WasCarrying { get; set; }

    /// <summary>Direction of the last step, for drawing which way the ant faces.</summary>
    public Vector2 Heading { get; private set; } = Vector2.Right;

    /// <summary>True when the ant is standing on its destination (or has none).</summary>
    public bool Arrived => _pathIndex >= _path.Count;

    private List<Vector2I> _path = new();
    private int _pathIndex;

    /// <summary>How far along the step from <see cref="Cell"/> to the next path cell, 0 to 1.</summary>
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
    /// Switches to a new role. Does nothing if the order is unchanged, so an AI can re-send the
    /// same orders every decision without resetting what the ant is doing.
    /// </summary>
    public void SetOrder(RoleOrder order)
    {
        if (order == Order)
            return;

        Order = order;
        ChaseTarget = null;

        // Keep any food already picked up; the new behavior will bring it home first.
        if (!CarryingFood)
            TargetFood = null;
    }

    /// <summary>
    /// Plans a path to <paramref name="goal"/>. Does nothing if already heading there.
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

    /// <summary>Clears the current destination; the ant stops at the end of its current step.</summary>
    public void Stop()
    {
        Destination = null;
        _path = IsMidStep ? new List<Vector2I> { _path[_pathIndex] } : new List<Vector2I>();
        _pathIndex = 0;
    }

    /// <summary>
    /// Walks along the path, spending <paramref name="distance"/> movement points.
    /// Each step costs <see cref="Pathfinder.MoveCost"/>, so climbing is slower than flat ground.
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
