# Map Generator

The map generator is split into two .NET 8 projects:

- `MapGenerator.Core` contains the reusable generator implementation in the `AntFarmer.Gen` namespace.
- `MapGenerator.Cli` is the console application that runs it.

## Run

From the repository root, run:

```powershell
dotnet run --project tools/MapGenerator.Cli -- <preset> [seed] [--no-repair]
```

Available presets are `Open`, `Caves`, `Highlands`, and `Contested`. The seed is optional; when omitted, the current UTC time is used. Repair is enabled by default. Pass `--no-repair` to disable it and generate a raw map.

Examples:

```powershell
dotnet run --project tools/MapGenerator.Cli -- Open
dotnet run --project tools/MapGenerator.Cli -- Caves 1234
dotnet run --project tools/MapGenerator.Cli -- Highlands 1234 --no-repair
```

Generated text files are written to the repository root's `maps/` directory, regardless of the current working directory. Filenames contain the preset and a sortable UTC timestamp; raw maps end with `_raw.txt`.

## Validate maps

To analyze every `.txt` map in the repository root's `maps/` directory and write a CSV report, run:

```powershell
dotnet run --project tools/MapGenerator.Cli -- --validate
```

The report is saved as `maps/map_validation.csv`. It includes each map's preset and generation parameters, measured map values and parameter comparisons, repair status, and a `PASS`/`FAIL` result for each rule in `docs/MAP_GENERATION.md`.

## Build

```powershell
dotnet build tools/MapGenerator.Cli/MapGenerator.Cli.csproj
dotnet build tools/MapGenerator.Core/MapGenerator.Core.csproj
```
