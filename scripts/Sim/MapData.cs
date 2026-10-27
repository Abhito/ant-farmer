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

public struct Cell
{
    public Terrain Terrain;

    public int Height; // 0 = flat

    public readonly bool IsWalkable => Terrain != Terrain.Rock;
}

public class FoodPile
{
    public int Id;
    public Vector2I Cell;
    public int Amount;
    public int StartAmount;
}

/// <summary>The static map layout: terrain, heights, nests, and starting food.</summary>
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
