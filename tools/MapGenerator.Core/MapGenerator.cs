using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AntFarmer.Gen
{
    public class GenParams
    {
        public string Name = "Custom";

        public int Width = 91;
        public int Height = 57;

        public float RockDensity = 0.08f;   // share of cells that are rock
        public int RockFeatureSize = 10;    // rough size of rock clusters

        public int HillCount = 30;
        public int MaxHillHeight = 3;       // 0-9
        public int HillRadius = 6;

        public int FoodPiles = 31;
        public int MinFoodSpacing = 8;      // cells between piles
        public int MinFoodNestDistance = 6; // cells between a pile and a nest
        public float FoodBias = 0f;         // -1 near nests, 0 even, +1 far from both

        public int MinNestDistance = 45;    // cells between the two nests
        public int NestEdgeMargin = 3;      // cells between a nest and the border
        public float MaxNestImbalance = 0.2f; // how much better one nest's food access may be

        public static GenParams Open()
        {
            return new GenParams
            {
                Name = "Open",
                RockDensity = 0.04f,
                MaxHillHeight = 2,
                MinNestDistance = 60
            };
        }

        public static GenParams Caves()
        {
            return new GenParams
            {
                Name = "Caves",
                RockDensity = 0.3f,
                RockFeatureSize = 5
            };
        }

        public static GenParams Highlands()
        {
            return new GenParams
            {
                Name = "Highlands",
                HillCount = 40,
                MaxHillHeight = 6,
                HillRadius = 8
            };
        }

        public static GenParams Contested()
        {
            return new GenParams
            {
                Name = "Contested",
                MinNestDistance = 30,
                FoodBias = 0.8f
            };
        }
    }

    public class MapGenerator
    {
        private readonly record struct NestPair(int RedX, int RedY, int BlueX, int BlueY);

        public static string Generate(GenParams p, int seed, bool repair = true)
        {
            var rand = new Random(seed);
            StringBuilder map = new();

            map.AppendLine($"; generator: {p.Name}\tseed: {seed}\trepaired: {repair}");
            map.AppendLine($"; Width={p.Width}\tHeight={p.Height}\tRockDensity={p.RockDensity}\tRockFeatureSize={p.RockFeatureSize}");
            map.AppendLine($"; HillCount={p.HillCount}\tMaxHillHeight={p.MaxHillHeight}\tHillRadius={p.HillRadius}\tFoodPiles={p.FoodPiles}");
            map.AppendLine($"; MinFoodSpacing={p.MinFoodSpacing}\tMinFoodNestDistance={p.MinFoodNestDistance}\tFoodBias={p.FoodBias}");
            map.AppendLine($"; MinNestDistance={p.MinNestDistance}\tNestEdgeMargin={p.NestEdgeMargin}\tMaxNestImbalance={p.MaxNestImbalance}");

            var cells = new char[p.Width * p.Height];
            if (p.Name == "Caves")
            {
                // Cellular automata produces more natural-looking caves than Perlin noise, so we use it for the "Caves" preset.
                var caveMap = CellularAutomata.Generate(p.Width, p.Height, rand, 4, (int)(p.RockDensity * 100));
                for (int y = 0; y < p.Height; y++)
                {
                    for (int x = 0; x < p.Width; x++)
                    {
                        cells[x + y * p.Width] = caveMap[x + y * p.Width] ? '#' : '.';
                    }
                }
            }
            else
            {
                var rocks = GenerateRocks(p.Width, p.Height, p, rand);
                for (int y = 0; y < p.Height; y++)
                {
                    for (int x = 0; x < p.Width; x++)
                    {
                        cells[x + y * p.Width] = rocks[x + y * p.Width] ? '#' : '.';
                    }
                }
            }

            if (repair) RepairConnectivity(cells, p.Width, p.Height);

            AddHills(cells, p, rand);
            PlaceNestsAndFood(cells, p, rand);
            BlendTerrainAroundFeatures(cells, p.Width, p.Height);
            for (int y = 0; y < p.Height; y++)
            {
                for (int x = 0; x < p.Width; x++)
                    map.Append(cells[x + y * p.Width]);

                map.AppendLine();
            }

            string mapText = map.ToString();
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string suffix = repair ? string.Empty : "_raw";
            string fileName = $"{p.Name}_{timestamp}{suffix}.txt";
            string mapsDirectory = Path.Combine(FindRepositoryRoot(), "maps");
            Directory.CreateDirectory(mapsDirectory);
            File.WriteAllText(Path.Combine(mapsDirectory, fileName), mapText);

            return mapText;
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Ant Farmer.csproj")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find the repository root containing 'Ant Farmer.csproj'.");
        }

        private static void BlendTerrainAroundFeatures(char[] cells, int width, int height)
        {
            var features = new List<(int X, int Y)>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char cell = cells[x + y * width];
                    if (cell == 'F' || cell == 'R' || cell == 'B') features.Add((x, y));
                }
            }

            foreach (var (X, Y) in features)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = x + y * width;
                        if (cells[index] < '1' || cells[index] > '9') continue;

                        int distance = Math.Max(Math.Abs(x - X), Math.Abs(y - Y));
                        int heightLimit = Math.Min(9, distance);
                        if (cells[index] - '0' > heightLimit)
                            cells[index] = (char)('0' + heightLimit);
                    }
                }
            }
        }

        // Close any pockets smaller than 10 cells, then carve tunnels between the remaining pockets
        private static void RepairConnectivity(char[] cells, int width, int height)
        {
            var components = FindOpenComponents(cells, width, height);
            foreach (var component in components)
            {
                if (component.Count >= 10) continue;

                foreach (int index in component)
                    cells[index] = '#';
            }

            components = FindOpenComponents(cells, width, height);
            while (components.Count > 1)
            {
                int mainIndex = 0;
                for (int i = 1; i < components.Count; i++)
                {
                    if (components[i].Count > components[mainIndex].Count) mainIndex = i;
                }

                var componentIds = new int[cells.Length];
                Array.Fill(componentIds, -1);
                for (int i = 0; i < components.Count; i++)
                {
                    foreach (int index in components[i])
                        componentIds[index] = i;
                }

                if (!CarveTunnel(cells, width, height, components[mainIndex], componentIds, mainIndex)) break;

                components = FindOpenComponents(cells, width, height);
            }
        }

        private static List<List<int>> FindOpenComponents(char[] cells, int width, int height)
        {
            var components = new List<List<int>>();
            var visited = new bool[cells.Length];
            var queue = new Queue<int>();

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int start = x + y * width;
                    if (visited[start] || cells[start] == '#') continue;

                    var component = new List<int>();
                    visited[start] = true;
                    queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        int index = queue.Dequeue();
                        component.Add(index);
                        int currentX = index % width;
                        int currentY = index / width;

                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;

                                int nextX = currentX + dx;
                                int nextY = currentY + dy;
                                if (nextX < 1 || nextY < 1 || nextX >= width - 1 || nextY >= height - 1) continue;
                                int next = nextX + nextY * width;
                                if (visited[next] || cells[next] == '#') continue;
                                if (dx != 0 && dy != 0 &&
                                    (cells[currentX + dx + currentY * width] == '#' ||
                                     cells[currentX + (currentY + dy) * width] == '#'))
                                    continue;

                                visited[next] = true;
                                queue.Enqueue(next);
                            }
                        }
                    }
                    components.Add(component);
                }
            }

            return components;
        }

        private static bool CarveTunnel(char[] cells, int width, int height, List<int> mainComponent, int[] componentIds, int mainIndex)
        {
            var costs = new int[cells.Length];
            var previous = new int[cells.Length];
            Array.Fill(costs, int.MaxValue);
            Array.Fill(previous, -1);
            var queue = new PriorityQueue<int, int>();

            foreach (int index in mainComponent)
            {
                costs[index] = 0;
                queue.Enqueue(index, 0);
            }

            int target = -1;
            int[] dxs = [0, -1, 1, 0];
            int[] dys = [-1, 0, 0, 1];
            while (queue.TryDequeue(out int index, out int cost))
            {
                if (cost != costs[index])
                    continue;
                if (componentIds[index] >= 0 && componentIds[index] != mainIndex)
                {
                    target = index;
                    break;
                }

                int x = index % width;
                int y = index / width;
                for (int i = 0; i < dxs.Length; i++)
                {
                    int nextX = x + dxs[i];
                    int nextY = y + dys[i];
                    if (nextX < 1 || nextY < 1 || nextX >= width - 1 || nextY >= height - 1) continue;

                    int next = nextX + nextY * width;
                    int nextCost = cost + (cells[next] == '#' ? 1 : 0);
                    if (nextCost >= costs[next]) continue;

                    costs[next] = nextCost;
                    previous[next] = index;
                    queue.Enqueue(next, nextCost);
                }
            }

            if (target < 0) return false;

            for (int index = target; index >= 0; index = previous[index])
                cells[index] = '.';

            return true;
        }

        // Add hills to the map using Perlin noise for natural variation
        private static void AddHills(char[] cells, GenParams p, Random rand)
        {
            int maxHeight = Math.Clamp(p.MaxHillHeight, 0, 9);
            int radius = Math.Max(1, p.HillRadius);

            var candidates = new List<int>();
            for (int y = 1; y < p.Height - 1; y++)
            {
                for (int x = 1; x < p.Width - 1; x++)
                {
                    int index = x + y * p.Width;
                    if (cells[index] == '.') candidates.Add(index);
                }
            }

            int hillCount = Math.Min(p.HillCount, candidates.Count);
            for (int i = 0; i < hillCount; i++)
            {
                int swapIndex = rand.Next(i, candidates.Count);
                (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
            }

            var noise = new PerlinNoise([1f, 0.5f, 0.25f], rand);
            for (int i = 0; i < hillCount; i++)
            {
                int centerX = candidates[i] % p.Width;
                int centerY = candidates[i] / p.Width;
                int minX = Math.Max(1, centerX - radius);
                int maxX = Math.Min(p.Width - 2, centerX + radius);
                int minY = Math.Max(1, centerY - radius);
                int maxY = Math.Min(p.Height - 2, centerY + radius);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        int dx = x - centerX;
                        int dy = y - centerY;
                        float distance = MathF.Sqrt(dx * dx + dy * dy);
                        if (distance > radius || cells[x + y * p.Width] == '#') continue;

                        float falloff = 1f - distance / (radius + 1f);
                        float noiseValue = noise.Get((float)x / radius, (float)y / radius);
                        float noiseFactor = 0.75f + 0.25f * Math.Clamp(noiseValue, -1f, 1f);
                        int height = Math.Clamp((int)MathF.Round(maxHeight * falloff * noiseFactor), 1, maxHeight);
                        int index = x + y * p.Width;
                        cells[index] = (char)('0' + Math.Max(cells[index] - '0', height));
                    }
                }
            }
        }

        // Food piles and nests are placed together to allow for better balancing of food access between the two nests.
        private static void PlaceNestsAndFood(char[] cells, GenParams p, Random rand)
        {
            int margin = Math.Max(0, p.NestEdgeMargin);
            var nestSpots = new List<(int X, int Y)>();
            for (int y = margin + 1; y + 1 < p.Height - margin - 1; y++)
            {
                for (int x = margin + 1; x + 1 < p.Width - margin - 1; x++)
                {
                    if (CanPlaceNest(cells, p.Width, p.Height, x, y)) nestSpots.Add((x, y));
                }
            }

            if (nestSpots.Count < 2) return;

            int requestedFood = Math.Max(0, p.FoodPiles);
            float allowedImbalance = Math.Clamp(p.MaxNestImbalance, 0f, 1f);
            float biasTarget = (Math.Clamp(p.FoodBias, -1f, 1f) + 1f) * 0.5f;
            float mapDiagonal = MathF.Sqrt(p.Width * p.Width + p.Height * p.Height);
            float bestScore = float.PositiveInfinity;
            NestPair bestPair = default;
            List<Disk> bestFood = [];

            int pairAttempts = Math.Min(256, nestSpots.Count * 4);
            for (int attempt = 0; attempt < pairAttempts; attempt++)
            {
                var red = nestSpots[rand.Next(nestSpots.Count)];
                var blue = nestSpots[rand.Next(nestSpots.Count)];
                if (red == blue) continue;

                int nestDx = red.X - blue.X;
                int nestDy = red.Y - blue.Y;
                if (nestDx * nestDx + nestDy * nestDy < p.MinNestDistance * p.MinNestDistance) continue;

                var pair = new NestPair(red.X, red.Y, blue.X, blue.Y);
                var pathCells = (char[])cells.Clone();
                SetNest(pathCells, p.Width, pair.RedX, pair.RedY, 'R');
                SetNest(pathCells, p.Width, pair.BlueX, pair.BlueY, 'B');
                float[] redCosts = GetTravelCosts(pathCells, p.Width, p.Height, pair.RedX, pair.RedY);
                float[] blueCosts = GetTravelCosts(pathCells, p.Width, p.Height, pair.BlueX, pair.BlueY);
                if (float.IsPositiveInfinity(redCosts[pair.BlueX + pair.BlueY * p.Width])) continue;

                var foodCandidates = new List<Disk>();
                int minFoodNestDistance = Math.Max(0, p.MinFoodNestDistance);
                for (int y = 1; y < p.Height - 1; y++)
                {
                    for (int x = 1; x < p.Width - 1; x++)
                    {
                        int index = x + y * p.Width;
                        if (cells[index] == '#' ||
                            IsNestCell(x, y, pair.RedX, pair.RedY) ||
                            IsNestCell(x, y, pair.BlueX, pair.BlueY) ||
                            !FarFromNest(x, y, pair.RedX, pair.RedY, minFoodNestDistance) ||
                            !FarFromNest(x, y, pair.BlueX, pair.BlueY, minFoodNestDistance) ||
                            float.IsPositiveInfinity(redCosts[index]) ||
                            float.IsPositiveInfinity(blueCosts[index]))
                            continue;

                        foodCandidates.Add(new Disk(x, y));
                    }
                }

                for (int layoutAttempt = 0; layoutAttempt < 4; layoutAttempt++)
                {
                    var food = PoissonDisk.Generate(foodCandidates, requestedFood, p.MinFoodSpacing, rand);
                    if (requestedFood > 0 && food.Count == 0) continue;

                    float redAccess = FoodAccess(redCosts, food, p.Width);
                    float blueAccess = FoodAccess(blueCosts, food, p.Width);
                    float imbalance = requestedFood == 0 ? 0f :
                        MathF.Abs(redAccess - blueAccess) / MathF.Max(redAccess, blueAccess);
                    float biasError = FoodBiasError(food, pair, biasTarget, mapDiagonal);
                    float missingFoodPenalty = (requestedFood - food.Count) * 10000f;
                    float fairnessPenalty = MathF.Max(0f, imbalance - allowedImbalance) * 100f;
                    float score = missingFoodPenalty + fairnessPenalty + imbalance + biasError * 0.1f;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestPair = pair;
                        bestFood = food;
                    }
                }
            }

            if (float.IsPositiveInfinity(bestScore)) return;

            foreach (var food in bestFood)
                cells[food.X + food.Y * p.Width] = 'F';

            SetNest(cells, p.Width, bestPair.RedX, bestPair.RedY, 'R');
            SetNest(cells, p.Width, bestPair.BlueX, bestPair.BlueY, 'B');
        }

        private static bool CanPlaceNest(char[] cells, int width, int height, int x, int y)
        {
            for (int checkY = y - 2; checkY <= y + 3; checkY++)
            {
                for (int checkX = x - 2; checkX <= x + 3; checkX++)
                {
                    if (checkX < 0 || checkY < 0 || checkX >= width || checkY >= height ||
                        cells[checkX + checkY * width] == '#')
                        return false;
                }
            }
            return true;
        }

        private static bool IsNestCell(int x, int y, int nestX, int nestY) =>
            x >= nestX && x <= nestX + 1 && y >= nestY && y <= nestY + 1;

        private static bool FarFromNest(int x, int y, int nestX, int nestY, int minDistance)
        {
            int minDistanceSquared = minDistance * minDistance;
            for (int dy = 0; dy <= 1; dy++)
            {
                for (int dx = 0; dx <= 1; dx++)
                {
                    int offsetX = x - (nestX + dx);
                    int offsetY = y - (nestY + dy);
                    if (offsetX * offsetX + offsetY * offsetY < minDistanceSquared) return false;
                }
            }
            return true;
        }

        private static void SetNest(char[] cells, int width, int x, int y, char team)
        {
            for (int dy = 0; dy <= 1; dy++)
                for (int dx = 0; dx <= 1; dx++)
                    cells[x + dx + (y + dy) * width] = team;
        }

        private static float[] GetTravelCosts(char[] cells, int width, int height, int nestX, int nestY)
        {
            var costs = new float[cells.Length];
            Array.Fill(costs, float.PositiveInfinity);
            var queue = new PriorityQueue<int, float>();

            for (int dy = 0; dy <= 1; dy++)
            {
                for (int dx = 0; dx <= 1; dx++)
                {
                    int index = nestX + dx + (nestY + dy) * width;
                    costs[index] = 0f;
                    queue.Enqueue(index, 0f);
                }
            }

            while (queue.TryDequeue(out int index, out float cost))
            {
                if (cost > costs[index]) continue;

                int x = index % width;
                int y = index / width;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nextX = x + dx;
                        int nextY = y + dy;
                        if (nextX < 0 || nextY < 0 || nextX >= width || nextY >= height) continue;
                        int nextIndex = nextX + nextY * width;
                        if (cells[nextIndex] == '#') continue;
                        if (dx != 0 && dy != 0 &&
                            (cells[x + dx + y * width] == '#' || cells[x + (y + dy) * width] == '#')) continue;

                        int currentHeight = CellHeight(cells[index]);
                        int nextHeight = CellHeight(cells[nextIndex]);
                        float step = dx != 0 && dy != 0 ? 1.41421356f : 1f;
                        step *= 1 + Math.Max(0, nextHeight - currentHeight);
                        float nextCost = cost + step;
                        if (nextCost >= costs[nextIndex]) continue;

                        costs[nextIndex] = nextCost;
                        queue.Enqueue(nextIndex, nextCost);
                    }
                }
            }

            return costs;
        }

        private static int CellHeight(char cell) => cell is >= '1' and <= '9' ? cell - '0' : 0;

        private static float FoodAccess(float[] costs, List<Disk> food, int width)
        {
            if (food.Count == 0) return 0f;

            var nearestCosts = new List<float>(food.Count);
            foreach (var pile in food)
                nearestCosts.Add(costs[pile.X + pile.Y * width]);
            nearestCosts.Sort();

            int count = Math.Min(5, nearestCosts.Count);
            float total = 0f;
            for (int i = 0; i < count; i++)
                total += nearestCosts[i];
            return total / count;
        }

        private static float FoodBiasError(List<Disk> food, NestPair pair, float target, float mapDiagonal)
        {
            if (food.Count == 0) return 0f;

            float total = 0f;
            foreach (var pile in food)
            {
                float redDistance = Distance(pile.X, pile.Y, pair.RedX + 0.5f, pair.RedY + 0.5f);
                float blueDistance = Distance(pile.X, pile.Y, pair.BlueX + 0.5f, pair.BlueY + 0.5f);
                total += MathF.Min(redDistance, blueDistance) / mapDiagonal;
            }
            return MathF.Abs(total / food.Count - target);
        }

        private static float Distance(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        // Generate rocks using Perlin noise
        private static bool[] GenerateRocks(int width, int height, GenParams p, Random rand)
        {
            var rocks = new bool[width * height];

            // Create Perlin noise with multiple octaves for natural clustering
            float[] weights = [1f, 0.5f, 0.25f];
            var perlin = new PerlinNoise(weights, rand);

            // Rank interior cells by their noise so density does not depend on
            // the non-uniform distribution of Perlin values.
            var noiseValues = new List<(float Value, int Index)>();

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float nx = (float)x / (p.RockFeatureSize / 2);
                    float ny = (float)y / (p.RockFeatureSize / 2);
                    float value = perlin.Get(nx, ny);
                    noiseValues.Add((value, x + y * width));
                }
            }

            noiseValues.Sort((a, b) =>
            {
                int byNoise = b.Value.CompareTo(a.Value);
                return byNoise != 0 ? byNoise : a.Index.CompareTo(b.Index);
            });

            int interiorRockCount = (int)MathF.Round(Math.Clamp(p.RockDensity, 0f, 1f) * noiseValues.Count);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                        rocks[x + y * width] = true;
                }
            }

            for (int i = 0; i < interiorRockCount; i++)
                rocks[noiseValues[i].Index] = true;

            return rocks;
        }
    }
}
