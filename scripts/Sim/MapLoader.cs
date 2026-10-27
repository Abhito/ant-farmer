using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AntFarmer.Sim;

/// <summary>Reads the text map format. See maps/arena.txt for the legend.</summary>
public static class MapLoader
{
    public const int DefaultFoodAmount = 10;

    public static MapData LoadFile(string path, int foodAmount = DefaultFoodAmount)
    {
        if (!FileAccess.FileExists(path))
            throw new ArgumentException($"Map file not found: {path}");

        return Parse(FileAccess.GetFileAsString(path), foodAmount);
    }

    public static MapData Parse(string text, int foodAmount = DefaultFoodAmount)
    {
        List<string> rows = text
            .Replace("\r", "")
            .Split('\n')
            .Where(line => line.Length > 0 && !line.StartsWith(';'))
            .ToList();

        if (rows.Count == 0)
            throw new FormatException("Map has no rows.");

        int width = rows[0].Length;
        for (int y = 0; y < rows.Count; y++)
        {
            if (rows[y].Length != width)
                throw new FormatException($"Map row {y} has {rows[y].Length} cells, expected {width}.");
        }

        var map = new MapData(width, rows.Count);

        for (int y = 0; y < rows.Count; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char c = rows[y][x];
                var pos = new Vector2I(x, y);
                var cell = new Cell { Terrain = Terrain.Ground, Height = 0 };

                switch (c)
                {
                    case '.':
                        break;
                    case '#':
                        cell.Terrain = Terrain.Rock;
                        break;
                    case >= '1' and <= '9':
                        cell.Height = c - '0';
                        break;
                    case 'F':
                        map.Food.Add(new FoodPile { Id = map.Food.Count, Cell = pos, Amount = foodAmount, StartAmount = foodAmount });
                        break;
                    case 'R':
                        map.Nests[Team.Red].Add(pos);
                        break;
                    case 'B':
                        map.Nests[Team.Blue].Add(pos);
                        break;
                    default:
                        throw new FormatException($"Unknown map character '{c}' at ({x}, {y}).");
                }

                map[pos] = cell;
            }
        }

        foreach (Team team in Enum.GetValues<Team>())
        {
            if (map.Nests[team].Count == 0)
                throw new FormatException($"Map has no {team} nest.");

            SortFromCenterOut(map.Nests[team], map);
        }

        if (map.Food.Count == 0)
            throw new FormatException("Map has no food sources.");

        return map;
    }

    // Center-out order gives mirrored nests mirrored spawn spots.
    private static void SortFromCenterOut(List<Vector2I> cells, MapData map)
    {
        var center = new Vector2((map.Width - 1) / 2f, (map.Height - 1) / 2f);
        cells.Sort((a, b) =>
        {
            int byDistance = center.DistanceSquaredTo(a).CompareTo(center.DistanceSquaredTo(b));
            return byDistance != 0 ? byDistance : a.Y.CompareTo(b.Y);
        });
    }
}
