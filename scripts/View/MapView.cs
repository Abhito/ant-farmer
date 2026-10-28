using System;
using System.Collections.Generic;
using AntFarmer.Sim;
using Godot;

namespace AntFarmer.View;

/// <summary>Draws the map. All grid-to-screen conversion goes through GridToWorld.</summary>
public partial class MapView : Node2D
{
    [Export] public int TileSize { get; set; } = 32;

    private static readonly Color LowGround = new(0.36f, 0.25f, 0.16f);
    private static readonly Color HighGround = new(0.80f, 0.68f, 0.46f);
    private static readonly Color RockColor = new(0.42f, 0.42f, 0.45f);
    private static readonly Color RockEdge = new(0.28f, 0.28f, 0.31f);
    private static readonly Color ContourColor = new(0.15f, 0.10f, 0.06f, 0.6f);
    private static readonly Color GridLineColor = new(0f, 0f, 0f, 0.08f);
    private static readonly Color FoodColor = new(0.35f, 0.85f, 0.30f);
    private static readonly Color FoodEdge = new(0.12f, 0.40f, 0.10f);

    public static Color TeamColor(Team team) => team switch
    {
        Team.Red => new Color(0.90f, 0.25f, 0.20f),
        Team.Blue => new Color(0.25f, 0.50f, 0.95f),
        _ => Colors.White,
    };

    public MapData Map { get; private set; }

    public Vector2 WorldSize => Map == null ? Vector2.Zero : new Vector2(Map.Width, Map.Height) * TileSize;

    private int _maxHeight = 1;
    private IReadOnlyList<FoodPile> _food = Array.Empty<FoodPile>();

    // Drawn as a faint circle around each nest; 0 hides it.
    public float DefendLeash { get; set; }

    public void SetMap(MapData map, IReadOnlyList<FoodPile> food)
    {
        Map = map;
        _food = food;

        _maxHeight = 1;
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                _maxHeight = Mathf.Max(_maxHeight, map[x, y].Height);

        QueueRedraw();
    }

    public Vector2 GridToWorld(Vector2I cell) => (cell + new Vector2(0.5f, 0.5f)) * TileSize;

    public Vector2 GridToWorld(Vector2 gridPos) => (gridPos + new Vector2(0.5f, 0.5f)) * TileSize;

    public override void _Draw()
    {
        if (Map == null)
            return;

        DrawTerrain();
        DrawContours();
        DrawLeash();
        DrawNests();
        DrawFood();
    }

    private Rect2 CellRect(int x, int y) => new(x * TileSize, y * TileSize, TileSize, TileSize);

    private void DrawTerrain()
    {
        for (int y = 0; y < Map.Height; y++)
        {
            for (int x = 0; x < Map.Width; x++)
            {
                Cell cell = Map[x, y];
                Rect2 rect = CellRect(x, y);

                if (cell.Terrain == Terrain.Rock)
                {
                    DrawRect(rect, RockColor);
                    DrawRect(rect.Grow(-1), RockEdge, filled: false, width: 2);
                }
                else
                {
                    // Higher ground is drawn lighter, like a relief map.
                    float t = (float)cell.Height / _maxHeight;
                    DrawRect(rect, LowGround.Lerp(HighGround, t));
                    DrawRect(rect, GridLineColor, filled: false, width: 1);
                }
            }
        }
    }

    // Dark lines where the height changes.
    private void DrawContours()
    {
        for (int y = 0; y < Map.Height; y++)
        {
            for (int x = 0; x < Map.Width; x++)
            {
                Cell cell = Map[x, y];
                if (!cell.IsWalkable)
                    continue;

                float left = x * TileSize;
                float top = y * TileSize;

                if (x + 1 < Map.Width && Map[x + 1, y].IsWalkable && Map[x + 1, y].Height != cell.Height)
                    DrawLine(new Vector2(left + TileSize, top), new Vector2(left + TileSize, top + TileSize), ContourColor, 2);

                if (y + 1 < Map.Height && Map[x, y + 1].IsWalkable && Map[x, y + 1].Height != cell.Height)
                    DrawLine(new Vector2(left, top + TileSize), new Vector2(left + TileSize, top + TileSize), ContourColor, 2);
            }
        }
    }

    private void DrawNests()
    {
        foreach ((Team team, var cells) in Map.Nests)
        {
            Color color = TeamColor(team);
            foreach (Vector2I c in cells)
            {
                Rect2 rect = CellRect(c.X, c.Y);
                DrawRect(rect, color with { A = 0.55f });
                DrawRect(rect.Grow(-2), color, filled: false, width: 2);
            }
        }
    }

    private void DrawLeash()
    {
        if (DefendLeash <= 0f)
            return;

        foreach ((Team team, var cells) in Map.Nests)
        {
            Vector2 center = Vector2.Zero;
            foreach (Vector2I c in cells)
                center += GridToWorld(c);
            center /= cells.Count;

            float radius = (DefendLeash + 0.5f) * TileSize;
            DrawCircle(center, radius, TeamColor(team) with { A = 0.06f });
            DrawArc(center, radius, 0, Mathf.Tau, 96, TeamColor(team) with { A = 0.35f }, 2f);
        }
    }

    private void DrawFood()
    {
        float maxRadius = TileSize * 0.38f;
        foreach (FoodPile food in _food)
        {
            Vector2 center = GridToWorld(food.Cell);

            // An empty pile shows as a faint ring until it regrows.
            if (food.Amount <= 0)
            {
                DrawArc(center, maxRadius, 0, Mathf.Tau, 24, FoodEdge, 2);
                continue;
            }

            // The circle shrinks as the pile is used up.
            float t = Mathf.Clamp(food.Amount / (float)food.StartAmount, 0.25f, 1f);
            DrawCircle(center, maxRadius * t, FoodColor);
            DrawArc(center, maxRadius * t, 0, Mathf.Tau, 24, FoodEdge, 2);
        }
    }
}
