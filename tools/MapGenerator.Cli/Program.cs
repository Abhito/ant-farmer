using AntFarmer.Gen;

if (args.Length > 0 && args[0] == "--validate")
{
    if (args.Length != 1)
    {
        PrintUsage();
        return 1;
    }

    return MapValidation.Run();
}

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

GenParams parameters;
try
{
    parameters = args[0].ToLowerInvariant() switch
    {
        "open" => GenParams.Open(),
        "caves" => GenParams.Caves(),
        "highlands" => GenParams.Highlands(),
        "contested" => GenParams.Contested(),
        _ => throw new ArgumentException($"Unknown preset '{args[0]}'."),
    };
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    PrintUsage();
    return 1;
}

int? seed = null;
bool repair = true;
for (int i = 1; i < args.Length; i++)
{
    if (args[i] == "--no-repair")
    {
        repair = false;
    }
    else if (int.TryParse(args[i], out int parsedSeed) && seed is null)
    {
        seed = parsedSeed;
    }
    else
    {
        Console.Error.WriteLine($"Unexpected argument '{args[i]}'.");
        PrintUsage();
        return 1;
    }
}

int actualSeed = seed ?? unchecked((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
var map = MapGenerator.Generate(parameters, actualSeed, repair);
Console.WriteLine(map);

string mapsDirectory = Path.Combine(FindRepositoryRoot(), "maps");
Directory.CreateDirectory(mapsDirectory);
string path = Path.Combine(mapsDirectory, $"{parameters.Name}_{actualSeed}{(repair ? string.Empty : "_raw")}.txt");
File.WriteAllText(path, map);
Console.WriteLine($"Generated {parameters.Name} map with seed {actualSeed}{(repair ? string.Empty : " (raw)")}, saved to {path}.");
return 0;

static string FindRepositoryRoot()
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

static void PrintUsage() =>
    Console.Error.WriteLine("Usage: dotnet run --project tools/MapGenerator.Cli -- <Open|Caves|Highlands|Contested> [seed] [--no-repair] | --validate");
