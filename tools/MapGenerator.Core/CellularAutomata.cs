using System;
using System.Collections.Generic;

namespace AntFarmer.Gen
{
    // Cave rock from cellular automata, adapted from the C# example code in RogueBasin, "Cellular Automata Method for Generating Random Cave-Like Levels"
    // (https://www.roguebasin.com/index.php/Cellular_Automata_Method_for_Generating_Random_Cave-Like_Levels).
    public static class CellularAutomata
    {
        public static bool[] Generate(int width, int height, Random rand, int iterations = 4, int percentAreWalls = 40)
        {
            var map = new bool[width * height];
            RandomFill(map, width, height, rand, percentAreWalls);

            for (var i = 0; i < iterations; i++)
                map = Step(map, width, height);

            AdjustDensity(map, width, height, percentAreWalls);
            return map;
        }

        private static void AdjustDensity(bool[] map, int width, int height, int percentAreWalls)
        {
            int interiorCount = (width - 2) * (height - 2);
            int targetWalls = (int)Math.Round(interiorCount * Math.Clamp(percentAreWalls, 0, 100) / 100f);
            int currentWalls = 0;
            var candidates = new List<(int Index, int NeighborWalls)>();

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = x + y * width;
                    if (map[index])
                        currentWalls++;
                }
            }

            if (currentWalls == targetWalls)
                return;

            bool removeWalls = currentWalls > targetWalls;
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = x + y * width;
                    if (map[index] != removeWalls)
                        continue;

                    int neighborWalls = CountAdjacentWalls(map, width, x, y) - (map[index] ? 1 : 0);
                    candidates.Add((index, neighborWalls));
                }
            }

            candidates.Sort((a, b) =>
            {
                int byNeighbors = removeWalls
                    ? a.NeighborWalls.CompareTo(b.NeighborWalls)
                    : b.NeighborWalls.CompareTo(a.NeighborWalls);
                return byNeighbors != 0 ? byNeighbors : a.Index.CompareTo(b.Index);
            });

            int adjustmentCount = Math.Abs(currentWalls - targetWalls);
            for (int i = 0; i < adjustmentCount; i++)
                map[candidates[i].Index] = !removeWalls;
        }

        private static void RandomFill(bool[] map, int width, int height, Random rand, int percentAreWalls = 40)
        {
            var randomColumn = rand.Next(4, width - 4);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                        map[x + y * width] = true;
                    else if (x != randomColumn && rand.Next(100) < percentAreWalls)
                        map[x + y * width] = true;
                }
            }
        }

        private static bool[] Step(bool[] map, int width, int height)
        {
            var newMap = new bool[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                        newMap[x + y * width] = true;
                    else
                        newMap[x + y * width] = PlaceWallLogic(map, width, height, x, y);
                }
            }
            return newMap;
        }

        private static bool PlaceWallLogic(bool[] map, int width, int height, int x, int y) =>
            CountAdjacentWalls(map, width, x, y) >= 5 ||
            CountNearbyWalls(map, width, height, x, y) <= 2;

        private static int CountAdjacentWalls(bool[] map, int width, int x, int y)
        {
            var walls = 0;
            for (var mapX = x - 1; mapX <= x + 1; mapX++)
            {
                for (var mapY = y - 1; mapY <= y + 1; mapY++)
                {
                    if (map[mapX + mapY * width]) walls++;
                }
            }
            return walls;
        }

        private static int CountNearbyWalls(bool[] map, int width, int height, int x, int y)
        {
            var walls = 0;
            for (var mapX = x - 2; mapX <= x + 2; mapX++)
            {
                for (var mapY = y - 2; mapY <= y + 2; mapY++)
                {
                    if (Math.Abs(mapX - x) == 2 && Math.Abs(mapY - y) == 2) continue;
                    if (mapX < 0 || mapY < 0 || mapX >= width || mapY >= height) continue;
                    if (map[mapX + mapY * width]) walls++;
                }
            }
            return walls;
        }
    }
}
