using System.Globalization;
using System.Text;
using AntFarmer.Gen;

internal static class MapValidation
{
    private static readonly string[] RuleColumns =
    [
        "valid_characters_and_rows", "rock_border", "nests_are_2x2_and_far_enough", "no_rock_near_nests",
        "nests_reach_each_other_and_food", "food_access_fair_within_20_percent", "food_count_and_nest_spacing",
        "open_ground_connected_90_percent", "dimensions_in_range",
    ];

    private static readonly string[] Columns =
    [
        "file", "preset", "seed", "repaired",
        "param_width", "param_height", "param_rock_density", "param_rock_feature_size", "param_hill_count",
        "param_max_hill_height", "param_hill_radius", "param_food_piles", "param_min_food_spacing",
        "param_min_food_nest_distance", "param_food_bias", "param_min_nest_distance", "param_nest_edge_margin",
        "param_max_nest_imbalance", "actual_width", "actual_height", "actual_interior_rock_density",
        "width_delta", "height_delta", "rock_density_delta", "actual_hill_cells", "actual_max_hill_height",
        "max_hill_height_delta", "actual_food_piles",
        "food_piles_delta", "actual_min_food_spacing", "min_food_spacing_delta", "actual_min_food_nest_distance",
        "min_food_nest_distance_delta", "actual_nest_distance", "min_nest_distance_delta",
        "actual_nest_edge_gap", "nest_edge_margin_delta", "actual_red_food_access", "actual_blue_food_access",
        "actual_food_access_imbalance", "max_nest_imbalance_delta", "actual_food_bias_score", "food_bias_delta",
        "actual_open_ground_connected_percent", "food_pile_count_matches_parameter",
        "min_food_spacing_meets_parameter", "max_hill_height_not_exceeded", "nest_edge_margin_met",
        .. RuleColumns, "error",
    ];

    public static int Run()
    {
        string? root = FindRepositoryRoot();
        if (root is null)
        {
            Console.Error.WriteLine("Could not find the repository root containing 'Ant Farmer.csproj'.");
            return 1;
        }

        string mapsDirectory = Path.Combine(root, "maps");
        if (!Directory.Exists(mapsDirectory))
        {
            Console.Error.WriteLine($"Map directory not found: {mapsDirectory}");
            return 1;
        }

        string csvPath = Path.Combine(mapsDirectory, "map_validation.csv");
        string[] files = [.. Directory.EnumerateFiles(mapsDirectory, "*.txt").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)];

        using (var writer = new StreamWriter(csvPath, false, new UTF8Encoding(true)))
        {
            writer.WriteLine(CsvRow(Columns));
            foreach (string file in files)
            {
                Report report;
                try
                {
                    report = Analyze(file);
                }
                catch (Exception exception)
                {
                    report = new Report(Path.GetFileName(file));
                    report.Error = exception.Message;
                }

                writer.WriteLine(CsvRow(Columns.Select(column => report.Values.GetValueOrDefault(column) ?? string.Empty)));
            }
        }

        Console.WriteLine($"Validated {files.Length} map(s). Report: {csvPath}");
        return 0;
    }

    private static Report Analyze(string path)
    {
        string[] lines = File.ReadAllLines(path);
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string preset = string.Empty;
        string seed = string.Empty;
        string repaired = string.Empty;

        foreach (string line in lines.Where(line => line.StartsWith(';')))
        {
            string header = line.TrimStart(';', ' ');
            if (header.StartsWith("generator:", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string part in header.Split('\t'))
                {
                    string value = part.Trim();
                    if (value.StartsWith("generator:", StringComparison.OrdinalIgnoreCase))
                        preset = value["generator:".Length..].Trim();
                    else if (value.StartsWith("seed:", StringComparison.OrdinalIgnoreCase))
                        seed = value["seed:".Length..].Trim();
                    else if (value.StartsWith("repaired:", StringComparison.OrdinalIgnoreCase))
                        repaired = value["repaired:".Length..].Trim();
                }
            }
            else
            {
                foreach (string part in header.Split('\t'))
                {
                    int separator = part.IndexOf('=');
                    if (separator > 0)
                        metadata[part[..separator].Trim()] = part[(separator + 1)..].Trim();
                }
            }
        }

        var report = new Report(Path.GetFileName(path));
        report.Values["preset"] = preset;
        report.Values["seed"] = seed;
        report.Values["repaired"] = repaired;
        GenParams parameters = PresetParameters(preset);
        ApplyMetadata(parameters, metadata);
        WriteParameters(report, parameters);

        string[] rows = [.. lines.Where(line => line.Length > 0 && !line.StartsWith(';'))];
        if (rows.Length == 0)
        {
            report.Error = "Map has no rows.";
            return report;
        }

        int width = rows[0].Length;
        int height = rows.Length;
        report.Set("actual_width", width);
        report.Set("actual_height", height);
        report.Set("width_delta", width - parameters.Width);
        report.Set("height_delta", height - parameters.Height);
        bool rectangular = width > 0 && rows.All(row => row.Length == width);
        bool validCharacters = rectangular && rows.SelectMany(row => row).All(IsMapCharacter);
        report.Rule("valid_characters_and_rows", validCharacters);
        report.Rule("dimensions_in_range", width is >= 71 and <= 121 && height is >= 45 and <= 75);
        if (!rectangular)
        {
            report.Error = "Map rows do not have a consistent non-zero width.";
            return report;
        }

        var cells = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                cells[y, x] = rows[y][x];

        bool rockBorder = true;
        for (int x = 0; x < width; x++)
            rockBorder &= cells[0, x] == '#' && cells[height - 1, x] == '#';
        for (int y = 0; y < height; y++)
            rockBorder &= cells[y, 0] == '#' && cells[y, width - 1] == '#';
        report.Rule("rock_border", rockBorder);

        List<Point> red = FindCells(cells, 'R');
        List<Point> blue = FindCells(cells, 'B');
        List<Point> food = FindCells(cells, 'F');
        bool redNestValid = TryGetNest(red, out Point redOrigin);
        bool blueNestValid = TryGetNest(blue, out Point blueOrigin);
        double nestDistance = redNestValid && blueNestValid
            ? Distance(redOrigin, blueOrigin)
            : double.NaN;
        report.Set("actual_nest_distance", nestDistance);
        report.Set("min_nest_distance_delta", double.IsNaN(nestDistance) ? null : nestDistance - parameters.MinNestDistance);
        report.Rule("nests_are_2x2_and_far_enough",
            redNestValid && blueNestValid && nestDistance >= parameters.MinNestDistance);

        bool noRockNearNests = redNestValid && blueNestValid &&
            NoRockNearNest(cells, red) && NoRockNearNest(cells, blue);
        report.Rule("no_rock_near_nests", noRockNearNests);

        int interiorCells = Math.Max(0, width - 2) * Math.Max(0, height - 2);
        int interiorRocks = 0;
        int hillCells = 0;
        int maxHillHeight = 0;
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                char cell = cells[y, x];
                if (cell == '#') interiorRocks++;
                if (cell is >= '1' and <= '9')
                {
                    hillCells++;
                    maxHillHeight = Math.Max(maxHillHeight, cell - '0');
                }
            }
        }
        double actualRockDensity = interiorCells == 0 ? 0 : (double)interiorRocks / interiorCells;
        report.Set("actual_interior_rock_density", actualRockDensity);
        report.Set("rock_density_delta", actualRockDensity - parameters.RockDensity);
        report.Set("actual_hill_cells", hillCells);
        report.Set("actual_max_hill_height", maxHillHeight);
        report.Set("max_hill_height_delta", maxHillHeight - parameters.MaxHillHeight);
        report.Set("actual_food_piles", food.Count);
        report.Set("food_piles_delta", food.Count - parameters.FoodPiles);

        double? minFoodSpacing = MinimumPairDistance(food);
        report.Set("actual_min_food_spacing", minFoodSpacing);
        report.Set("min_food_spacing_delta", minFoodSpacing - parameters.MinFoodSpacing);
        double? minFoodNestDistance = MinimumFoodNestDistance(food, red, blue);
        report.Set("actual_min_food_nest_distance", minFoodNestDistance);
        report.Set("min_food_nest_distance_delta", minFoodNestDistance - parameters.MinFoodNestDistance);

        double? edgeGap = redNestValid && blueNestValid
            ? MinimumNestEdgeGap(red.Concat(blue), width, height)
            : null;
        report.Set("actual_nest_edge_gap", edgeGap);
        report.Set("nest_edge_margin_delta", edgeGap - parameters.NestEdgeMargin);
        report.Rule("food_pile_count_matches_parameter", food.Count == parameters.FoodPiles);
        report.Rule("min_food_spacing_meets_parameter", minFoodSpacing.HasValue && minFoodSpacing.Value >= parameters.MinFoodSpacing);
        report.Rule("max_hill_height_not_exceeded", maxHillHeight <= parameters.MaxHillHeight);
        report.Rule("nest_edge_margin_met", edgeGap.HasValue && edgeGap.Value >= parameters.NestEdgeMargin);

        int[,] components = FindComponents(cells, out int largestComponent, out int openCells);
        double connectedPercent = openCells == 0 ? 0 : (double)largestComponent / openCells * 100;
        report.Set("actual_open_ground_connected_percent", connectedPercent);
        report.Rule("open_ground_connected_90_percent", connectedPercent >= 90);

        bool allReachable = redNestValid && blueNestValid && food.Count > 0 &&
            SameComponent(components, red.Concat(blue).Concat(food));
        report.Rule("nests_reach_each_other_and_food", allReachable);

        double? redAccess = redNestValid ? FoodAccess(cells, red, food) : null;
        double? blueAccess = blueNestValid ? FoodAccess(cells, blue, food) : null;
        double? imbalance = redAccess.HasValue && blueAccess.HasValue
            ? Math.Abs(redAccess.Value - blueAccess.Value) / Math.Max(redAccess.Value, blueAccess.Value)
            : null;
        report.Set("actual_red_food_access", redAccess);
        report.Set("actual_blue_food_access", blueAccess);
        report.Set("actual_food_access_imbalance", imbalance);
        report.Set("max_nest_imbalance_delta", imbalance - parameters.MaxNestImbalance);
        report.Rule("food_access_fair_within_20_percent", imbalance.HasValue && imbalance.Value <= 0.2);

        double? foodBiasScore = ActualFoodBias(food, red, blue, width, height);
        double expectedFoodBias = (Math.Clamp(parameters.FoodBias, -1f, 1f) + 1f) * 0.5f;
        report.Set("actual_food_bias_score", foodBiasScore);
        report.Set("food_bias_delta", foodBiasScore - expectedFoodBias);

        bool foodCountAndSpacing = food.Count >= 12 && minFoodNestDistance.HasValue &&
            minFoodNestDistance.Value >= parameters.MinFoodNestDistance;
        report.Rule("food_count_and_nest_spacing", foodCountAndSpacing);

        return report;
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ant Farmer.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }
        return null;
    }

    private static GenParams PresetParameters(string preset) => preset.ToLowerInvariant() switch
    {
        "open" => GenParams.Open(),
        "caves" => GenParams.Caves(),
        "highlands" => GenParams.Highlands(),
        "contested" => GenParams.Contested(),
        _ => new GenParams { Name = string.IsNullOrEmpty(preset) ? "Custom" : preset },
    };

    private static void ApplyMetadata(GenParams p, Dictionary<string, string> metadata)
    {
        p.Width = Int(metadata, "Width", p.Width);
        p.Height = Int(metadata, "Height", p.Height);
        p.RockDensity = Float(metadata, "RockDensity", p.RockDensity);
        p.RockFeatureSize = Int(metadata, "RockFeatureSize", p.RockFeatureSize);
        p.HillCount = Int(metadata, "HillCount", p.HillCount);
        p.MaxHillHeight = Int(metadata, "MaxHillHeight", p.MaxHillHeight);
        p.HillRadius = Int(metadata, "HillRadius", p.HillRadius);
        p.FoodPiles = Int(metadata, "FoodPiles", p.FoodPiles);
        p.MinFoodSpacing = Int(metadata, "MinFoodSpacing", p.MinFoodSpacing);
        p.MinFoodNestDistance = Int(metadata, "MinFoodNestDistance", p.MinFoodNestDistance);
        p.FoodBias = Float(metadata, "FoodBias", p.FoodBias);
        p.MinNestDistance = Int(metadata, "MinNestDistance", p.MinNestDistance);
        p.NestEdgeMargin = Int(metadata, "NestEdgeMargin", p.NestEdgeMargin);
        p.MaxNestImbalance = Float(metadata, "MaxNestImbalance", p.MaxNestImbalance);
    }

    private static int Int(Dictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out string? value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : fallback;

    private static float Float(Dictionary<string, string> values, string key, float fallback) =>
        values.TryGetValue(key, out string? value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
            ? result
            : fallback;

    private static void WriteParameters(Report report, GenParams p)
    {
        report.Values["param_width"] = p.Width.ToString(CultureInfo.InvariantCulture);
        report.Values["param_height"] = p.Height.ToString(CultureInfo.InvariantCulture);
        report.Values["param_rock_density"] = Format(p.RockDensity);
        report.Values["param_rock_feature_size"] = p.RockFeatureSize.ToString(CultureInfo.InvariantCulture);
        report.Values["param_hill_count"] = p.HillCount.ToString(CultureInfo.InvariantCulture);
        report.Values["param_max_hill_height"] = p.MaxHillHeight.ToString(CultureInfo.InvariantCulture);
        report.Values["param_hill_radius"] = p.HillRadius.ToString(CultureInfo.InvariantCulture);
        report.Values["param_food_piles"] = p.FoodPiles.ToString(CultureInfo.InvariantCulture);
        report.Values["param_min_food_spacing"] = p.MinFoodSpacing.ToString(CultureInfo.InvariantCulture);
        report.Values["param_min_food_nest_distance"] = p.MinFoodNestDistance.ToString(CultureInfo.InvariantCulture);
        report.Values["param_food_bias"] = Format(p.FoodBias);
        report.Values["param_min_nest_distance"] = p.MinNestDistance.ToString(CultureInfo.InvariantCulture);
        report.Values["param_nest_edge_margin"] = p.NestEdgeMargin.ToString(CultureInfo.InvariantCulture);
        report.Values["param_max_nest_imbalance"] = Format(p.MaxNestImbalance);
    }

    private static bool IsMapCharacter(char cell) =>
        cell is '#' or '.' or 'F' or 'R' or 'B' or >= '1' and <= '9';

    private static List<Point> FindCells(char[,] cells, char value)
    {
        var found = new List<Point>();
        for (int y = 0; y < cells.GetLength(0); y++)
            for (int x = 0; x < cells.GetLength(1); x++)
                if (cells[y, x] == value) found.Add(new Point(x, y));
        return found;
    }

    private static bool TryGetNest(List<Point> nest, out Point origin)
    {
        origin = default;
        if (nest.Count != 4) return false;
        int x = nest.Min(point => point.X);
        int y = nest.Min(point => point.Y);
        if (nest.Any(point => point.X < x || point.X > x + 1 || point.Y < y || point.Y > y + 1))
            return false;
        origin = new Point(x, y);
        return true;
    }

    private static bool NoRockNearNest(char[,] cells, List<Point> nest)
    {
        foreach (Point point in nest)
        {
            for (int y = Math.Max(0, point.Y - 2); y <= Math.Min(cells.GetLength(0) - 1, point.Y + 2); y++)
            {
                for (int x = Math.Max(0, point.X - 2); x <= Math.Min(cells.GetLength(1) - 1, point.X + 2); x++)
                    if (cells[y, x] == '#') return false;
            }
        }
        return true;
    }

    private static double? MinimumPairDistance(List<Point> points)
    {
        if (points.Count < 2) return null;
        double minimum = double.PositiveInfinity;
        for (int i = 0; i < points.Count; i++)
            for (int j = i + 1; j < points.Count; j++)
                minimum = Math.Min(minimum, Distance(points[i], points[j]));
        return minimum;
    }

    private static double? MinimumFoodNestDistance(List<Point> food, List<Point> red, List<Point> blue)
    {
        if (food.Count == 0 || red.Count != 4 || blue.Count != 4) return null;
        double minimum = double.PositiveInfinity;
        foreach (Point pile in food)
            foreach (Point nestCell in red.Concat(blue))
                minimum = Math.Min(minimum, Distance(pile, nestCell));
        return minimum;
    }

    private static double MinimumNestEdgeGap(IEnumerable<Point> nest, int width, int height) => nest
        .Select(point => Math.Min(Math.Min(point.X, width - 1 - point.X), Math.Min(point.Y, height - 1 - point.Y)) - 1)
        .Min();

    private static int[,] FindComponents(char[,] cells, out int largest, out int openCount)
    {
        int height = cells.GetLength(0);
        int width = cells.GetLength(1);
        var ids = new int[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) ids[y, x] = -1;

        largest = 0;
        openCount = 0;
        int componentId = 0;
        var queue = new Queue<Point>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (cells[y, x] == '#' || ids[y, x] >= 0) continue;
                int size = 0;
                ids[y, x] = componentId;
                queue.Enqueue(new Point(x, y));
                while (queue.Count > 0)
                {
                    Point current = queue.Dequeue();
                    size++;
                    openCount++;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nextX = current.X + dx;
                            int nextY = current.Y + dy;
                            if (nextX < 0 || nextY < 0 || nextX >= width || nextY >= height ||
                                cells[nextY, nextX] == '#' || ids[nextY, nextX] >= 0)
                                continue;
                            if (dx != 0 && dy != 0 &&
                                (cells[current.Y, current.X + dx] == '#' || cells[current.Y + dy, current.X] == '#'))
                                continue;
                            ids[nextY, nextX] = componentId;
                            queue.Enqueue(new Point(nextX, nextY));
                        }
                    }
                }
                largest = Math.Max(largest, size);
                componentId++;
            }
        }
        return ids;
    }

    private static bool SameComponent(int[,] components, IEnumerable<Point> points)
    {
        using IEnumerator<Point> iterator = points.GetEnumerator();
        if (!iterator.MoveNext()) return false;
        Point first = iterator.Current;
        int component = components[first.Y, first.X];
        if (component < 0) return false;
        while (iterator.MoveNext())
        {
            Point point = iterator.Current;
            if (components[point.Y, point.X] != component) return false;
        }
        return true;
    }

    private static double? FoodAccess(char[,] cells, List<Point> nest, List<Point> food)
    {
        if (food.Count == 0) return null;
        int height = cells.GetLength(0);
        int width = cells.GetLength(1);
        var costs = new double[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) costs[y, x] = double.PositiveInfinity;

        var queue = new PriorityQueue<Point, double>();
        foreach (Point point in nest)
        {
            costs[point.Y, point.X] = 0;
            queue.Enqueue(point, 0);
        }

        while (queue.TryDequeue(out Point current, out double cost))
        {
            if (cost > costs[current.Y, current.X]) continue;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int x = current.X + dx;
                    int y = current.Y + dy;
                    if (x < 0 || y < 0 || x >= width || y >= height || cells[y, x] == '#') continue;
                    if (dx != 0 && dy != 0 && (cells[current.Y, x] == '#' || cells[y, current.X] == '#')) continue;
                    double step = dx != 0 && dy != 0 ? 1.41421356 : 1;
                    step *= 1 + Math.Max(0, CellHeight(cells[y, x]) - CellHeight(cells[current.Y, current.X]));
                    double nextCost = cost + step;
                    if (nextCost >= costs[y, x]) continue;
                    costs[y, x] = nextCost;
                    queue.Enqueue(new Point(x, y), nextCost);
                }
            }
        }

        double[] nearest = [.. food.Select(point => costs[point.Y, point.X]).OrderBy(value => value).Take(5)];
        if (nearest.Any(double.IsPositiveInfinity)) return null;
        return nearest.Average();
    }

    private static int CellHeight(char cell) => cell is >= '1' and <= '9' ? cell - '0' : 0;

    private static double? ActualFoodBias(List<Point> food, List<Point> red, List<Point> blue, int width, int height)
    {
        if (food.Count == 0 || red.Count != 4 || blue.Count != 4) return null;
        Point redOrigin = new(red.Min(point => point.X), red.Min(point => point.Y));
        Point blueOrigin = new(blue.Min(point => point.X), blue.Min(point => point.Y));
        double diagonal = Math.Sqrt((double)width * width + (double)height * height);
        return food.Average(pile => Math.Min(DistanceToCenter(pile, redOrigin), DistanceToCenter(pile, blueOrigin)) / diagonal);
    }

    private static double DistanceToCenter(Point point, Point origin) =>
        Math.Sqrt(Math.Pow(point.X - (origin.X + 0.5), 2) + Math.Pow(point.Y - (origin.Y + 0.5), 2));

    private static double Distance(Point first, Point second) => Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));
    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string CsvRow(IEnumerable<string> values) => string.Join(",", values.Select(Csv));

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private readonly record struct Point(int X, int Y);

    private sealed class Report
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
        public string Error
        {
            set => Values["error"] = value;
        }

        public Report(string file)
        {
            Values["file"] = file;
            foreach (string rule in RuleColumns) Values[rule] = "FAIL";
        }

        public void Set(string column, object? value)
        {
            Values[column] = value switch
            {
                null => string.Empty,
                double number when double.IsNaN(number) || double.IsInfinity(number) => string.Empty,
                double number => Format(number),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => value.ToString() ?? string.Empty,
            };
        }

        public void Rule(string column, bool passed) => Values[column] = passed ? "PASS" : "FAIL";
    }
}
