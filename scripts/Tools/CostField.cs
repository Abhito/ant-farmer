using System;
using System.Collections.Generic;
using AntFarmer.Sim;
using Godot;

namespace AntFarmer.Tools;

/// <summary>Cheapest travel cost from a set of start cells to every cell, with the same moves and costs as the ants.</summary>
public class CostField
{
    private static readonly Vector2I[] Directions =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
    };

    private readonly MapData _map;
    private readonly float[] _cost;

    public CostField(MapData map, IEnumerable<Vector2I> starts, float climbCost)
    {
        _map = map;
        _cost = new float[map.Width * map.Height];
        Array.Fill(_cost, float.PositiveInfinity);

        // Dijkstra from all start cells at once.
        var queue = new PriorityQueue<Vector2I, float>();
        foreach (Vector2I start in starts)
        {
            if (map.InBounds(start) && map[start].IsWalkable)
            {
                _cost[Index(start)] = 0f;
                queue.Enqueue(start, 0f);
            }
        }

        while (queue.TryDequeue(out Vector2I cell, out float cost))
        {
            if (cost > _cost[Index(cell)])
                continue;

            foreach (Vector2I next in Neighbors(map, cell))
            {
                float nextCost = cost + Pathfinder.StepCost(map, cell, next, climbCost);
                if (nextCost < _cost[Index(next)])
                {
                    _cost[Index(next)] = nextCost;
                    queue.Enqueue(next, nextCost);
                }
            }
        }
    }

    public float this[Vector2I cell] => _cost[Index(cell)];

    public bool Reaches(Vector2I cell) => !float.IsPositiveInfinity(this[cell]);

    // Same moves as the pathfinder: 8 directions, diagonals only when both side cells are open.
    public static IEnumerable<Vector2I> Neighbors(MapData map, Vector2I cell)
    {
        foreach (Vector2I d in Directions)
        {
            Vector2I next = cell + d;
            if (!map.InBounds(next) || !map[next].IsWalkable)
                continue;

            if (d.X != 0 && d.Y != 0 &&
                (!map[new Vector2I(cell.X + d.X, cell.Y)].IsWalkable || !map[new Vector2I(cell.X, cell.Y + d.Y)].IsWalkable))
                continue;

            yield return next;
        }
    }

    private int Index(Vector2I p) => p.Y * _map.Width + p.X;
}
