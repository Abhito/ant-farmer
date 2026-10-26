using System.Collections.Generic;
using Godot;

namespace AntFarmer.Sim;

/// <summary>
/// Shortest paths on the map grid, using Godot's AStarGrid2D.
/// Rock blocks movement, ants may move diagonally (but not squeeze between two blocked corners),
/// and moving uphill costs extra. Downhill and flat moves cost only the distance.
/// </summary>
public partial class Pathfinder
{
    public MapData Map { get; }

    /// <summary>Extra cost per height level climbed, as a fraction of the move's distance.</summary>
    public float ClimbCost { get; }

    private readonly GridAStar _astar;

    public Pathfinder(MapData map, float climbCost)
    {
        Map = map;
        ClimbCost = climbCost;

        _astar = new GridAStar(this)
        {
            Region = new Rect2I(0, 0, map.Width, map.Height),
            CellSize = Vector2.One,
            DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles,
            // Octile distance is the exact cost of a path with no climbing, so it never
            // overestimates and A* still finds the cheapest path.
            DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Octile,
        };
        _astar.Update();

        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (!map[x, y].IsWalkable)
                    _astar.SetPointSolid(new Vector2I(x, y));
    }

    public bool IsWalkable(Vector2I cell) => Map.InBounds(cell) && Map[cell].IsWalkable;

    /// <summary>
    /// Cost of stepping between two neighboring cells: the distance (1 straight, about 1.41 diagonal),
    /// times (1 + ClimbCost × levels climbed). Ants will also use this to set how long a step takes,
    /// so slow routes on the planner are slow in the game too.
    /// </summary>
    public float MoveCost(Vector2I from, Vector2I to)
    {
        float distance = ((Vector2)(to - from)).Length();
        int climb = Mathf.Max(0, Map[to].Height - Map[from].Height);
        return distance * (1f + ClimbCost * climb);
    }

    /// <summary>
    /// Cells to walk through to get from one cell to another, not including the start cell. Empty if already there or if there is no route.
    /// </summary>
    public List<Vector2I> FindPath(Vector2I from, Vector2I to)
    {
        var path = new List<Vector2I>();
        if (from == to || !IsWalkable(from) || !IsWalkable(to))
            return path;

        var cells = _astar.GetIdPath(from, to);
        for (int i = 1; i < cells.Count; i++)
            path.Add(cells[i]);

        return path;
    }

    /// <summary>
    /// Cost of the cheapest route between two cells: 0 if they are the same cell, infinity if there is
    /// no route. This is the number gatherers and the strategist compare when choosing where to go.
    /// </summary>
    public float TravelCost(Vector2I from, Vector2I to)
    {
        if (from == to)
            return 0f;

        List<Vector2I> path = FindPath(from, to);
        return path.Count == 0 ? float.PositiveInfinity : PathCost(from, path);
    }

    /// <summary>Total MoveCost of walking the given path from a starting cell.</summary>
    public float PathCost(Vector2I from, IReadOnlyList<Vector2I> path)
    {
        float cost = 0f;
        Vector2I current = from;
        foreach (Vector2I next in path)
        {
            cost += MoveCost(current, next);
            current = next;
        }
        return cost;
    }

    /// <summary>AStarGrid2D with its step cost replaced by MoveCost.</summary>
    private partial class GridAStar : AStarGrid2D
    {
        private readonly Pathfinder _owner;

        public GridAStar(Pathfinder owner) => _owner = owner;

        // Godot needs a parameterless constructor for classes that derive from its objects.
        public GridAStar() { }

        public override float _ComputeCost(Vector2I fromId, Vector2I toId) => _owner.MoveCost(fromId, toId);
    }
}
