using System.Collections.Generic;
using Godot;

namespace AntFarmer.Sim;

/// <summary>Shortest paths using Godot's AStarGrid2D. Rock blocks movement and climbing costs extra.</summary>
public partial class Pathfinder
{
    public MapData Map { get; }
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
            // Octile distance never overestimates, so A* still finds the cheapest path.
            DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Octile,
        };
        _astar.Update();

        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (!map[x, y].IsWalkable)
                    _astar.SetPointSolid(new Vector2I(x, y));
    }

    public bool IsWalkable(Vector2I cell) => Map.InBounds(cell) && Map[cell].IsWalkable;

    // Step distance times (1 + ClimbCost per level climbed). Ants use this for movement too.
    public float MoveCost(Vector2I from, Vector2I to)
    {
        float distance = ((Vector2)(to - from)).Length();
        int climb = Mathf.Max(0, Map[to].Height - Map[from].Height);
        return distance * (1f + ClimbCost * climb);
    }

    /// <summary>Cells to walk through, not including the start. Empty if there is no route.</summary>
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

    /// <summary>Cost of the cheapest route. Infinity if there is none.</summary>
    public float TravelCost(Vector2I from, Vector2I to)
    {
        if (from == to)
            return 0f;

        List<Vector2I> path = FindPath(from, to);
        return path.Count == 0 ? float.PositiveInfinity : PathCost(from, path);
    }

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

    private partial class GridAStar : AStarGrid2D
    {
        private readonly Pathfinder _owner;

        public GridAStar(Pathfinder owner) => _owner = owner;

        // Godot needs a parameterless constructor.
        public GridAStar() { }

        public override float _ComputeCost(Vector2I fromId, Vector2I toId) => _owner.MoveCost(fromId, toId);
    }
}
