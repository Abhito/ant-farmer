using System.Collections.Generic;
using Godot;

namespace AntFarmer.Sim;

public enum Terrain
{
    Ground,
    Rock,
}

public enum Team
{
    Red,
    Blue,
}

/// <summary>One grid square of the map.</summary>
public struct Cell
{
    public Terrain Terrain;

    /// <summary>Elevation, 0 = flat. Higher cells cost more to climb.</summary>
    public int Height;

    public readonly bool IsWalkable => Terrain != Terrain.Rock;
}

/// <summary>A pile of food that ants can carry home one unit at a time.</summary>
public class FoodPile
{
    public int Id;
    public Vector2I Cell;
    public int Amount;

    /// <summary>How much the pile held at the start of the match, so the view can shrink it as it empties.</summary>
    public int StartAmount;
}

/// <summary>
/// The static layout of a map: terrain, heights, nests, and the starting food.
/// Pure data with no nodes or drawing, so the simulation, AI, and a map
/// generator can all use it without depending on how it is rendered.
/// </summary>
public class MapData
{
    public int Width { get; }
    public int Height { get; }

    private readonly Cell[] _cells;

    public Dictionary<Team, List<Vector2I>> Nests { get; } = new()
    {
        [Team.Red] = new List<Vector2I>(),
        [Team.Blue] = new List<Vector2I>(),
    };

    /// <summary>The starting piles. Each match works on its own copy, so the map can be reused.</summary>
    public List<FoodPile> Food { get; } = new();

    public MapData(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new Cell[width * height];
    }

    public bool InBounds(Vector2I p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

    public Cell this[Vector2I p]
    {
        get => _cells[p.Y * Width + p.X];
        set => _cells[p.Y * Width + p.X] = value;
    }

    public Cell this[int x, int y]
    {
        get => _cells[y * Width + x];
        set => _cells[y * Width + x] = value;
    }
}
