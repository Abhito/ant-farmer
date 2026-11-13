# Ant Farmer

An ant colony strategy game written in C# for the Godot engine.

**Engine version: Godot 4.7.2-stable, .NET build** (the C# build, sometimes called "mono"), with **.NET SDK 8**. The project was built and tested with exactly these versions (`Ant Farmer.csproj` pins `Godot.NET.Sdk/4.7.2`, target `net8.0`; tested with .NET SDK 8.0.303).

**How to apply the code:** this folder is a complete Godot project. There is nothing to copy into the engine, no engine modifications, and no plugins. Install the two requirements below, then open `project.godot` in Godot.

## Requirements

1. **Godot 4.7.2 .NET**
   - Download **Godot Engine – .NET**, version **4.7.2-stable**, from [https://godotengine.org/download](https://godotengine.org/download) (older versions: [https://godotengine.org/download/archive](https://godotengine.org/download/archive)).
   - The file name includes `mono`: `Godot_v4.7.2-stable_mono_win64.zip` on Windows, `Godot_v4.7.2-stable_mono_macos.universal.zip` on macOS.
   - Do **not** use the standard (non-.NET) download. It can't run C# and won't open this project correctly.
   - Godot is self-contained: extract the zip and run it. No installer.
2. **.NET SDK 8 or later**
   - Godot's .NET build doesn't include the C# compiler. Install the SDK from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download).
   - Use the 64-bit SDK with the 64-bit editor (on Apple Silicon Macs, the Arm64 SDK).

## Import and open the project

1. Unzip the submission (or clone the repository) to a local folder.
2. Launch the **Godot .NET** editor. The Project Manager window opens first.
3. Click **Import**.
4. Browse to this repository and select `project.godot`, then click **Import & Edit** (or **Open**).
5. Godot will import assets and open the **Ant Farmer** editor.

After the first import, the project appears in the Project Manager list. Double-click it (or select it and click **Edit**) to open it again.

If Godot asks you to choose an editor version, pick **4.7 .NET**. Opening the project with a standard (non-.NET) editor will fail or disable C# features.

**If the C# code doesn't build on first open** (errors about missing scripts or assemblies): click **Build** (the hammer icon, top right of the editor), or run `dotnet build` in the project folder, then run the project again.

## Run a match

1. Click **Run Project** (or press **F5**). The main scene is `scenes/Main.tscn`.
2. Two AI colonies, red and blue, play against each other. The match ends when one side delivers 30 food, or the time limit is reached. Food piles start small and regrow after being emptied. The seed sets pile size, regrow time and the size of each side's defender area, plus small random timing differences, so each seed plays a different match and the same seed replays the same one.

Controls:

| Key | Action |
|---|---|
| **R** | Restart the match |
| **Space** | Pause / resume |
| **N** | Generate a new map (next seed) and restart |
| **1-4** | Generate a map with the Open, Caves, Highlands or Contested preset |

## Play on generated maps

The map generator (`tools/MapGenerator.Core`) is plain C# and is built into the game. On the **GameRunner** node, under **Map**, set **Map Source** to **Generated**, then pick a **Map Preset**, **Map Seed**, and whether to **Repair** the map. Or press **N** or **1-4** while the game runs.

Every generated map goes through the validity checker before the match starts. A map that fails is rejected, and the next seed is tried; the console says why. The bottom bar shows which map is playing, e.g. `map Caves 7`.

From the command line, add options after `--`:

```
Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --fixed-fps 60 --quit-after 12000 res://scenes/Main.tscn -- map=Caves:7
```

| Option | Meaning |
|---|---|
| `map=Caves:7` | Generated map: preset and seed |
| `raw` | Skip the generator's repair step |
| `map_file=res://maps/arena_large.txt` | Play a map file |
| `seed=12` | Match seed |
| `shot=out.png shot_at=35` | Save a screenshot at 35 s of game time, then quit (needs a window, so leave out `--headless`) |

## Generate and analyze maps

The generator, its parameters and presets are described in [docs/MAP_GENERATION.md](docs/MAP_GENERATION.md) and [docs/PERLIN_ROCK_GENERATION_GUIDE.md](docs/PERLIN_ROCK_GENERATION_GUIDE.md).

1. **Batch:** generate, check and measure 800 preset maps (4 presets × 100 seeds, raw and repaired) and 570 parameter-sweep maps (about 5 minutes):
   ```
   Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . res://scenes/MapBatch.tscn -- 100 30
   ```
   This writes `results/maps/maps.csv` (one row per map: validity, failed rules, measurements) and the map files to `results/maps/generated/`.
2. **Plots and tables:** needs Python 3 with matplotlib (`pip install matplotlib`):
   ```
   python tools/plot_maps.py
   ```
   This writes `results/maps/summary.md` (validity rates, medians), `distributions.png`, `expressive_range.png` and `sweeps.png`.
3. **Check map files:** `res://scenes/MapCheck.tscn -- <file or folder>` prints each map's problems and measurements. `-- selftest` breaks a good map in 12 ways and confirms the checker catches each one, and confirms its path costs match the game's A*.
4. **Generator on its own (no Godot):** `dotnet run --project tools/MapGenerator.Cli -- Caves 1234` saves `maps/Caves_1234.txt`. See `tools/MapGenerator.Cli/README.md`.

Screenshots of generated maps in play, and two failed maps, are in `results/maps/screenshots/`.

## Change the match settings

Select the **GameRunner** node in `scenes/Main.tscn` and edit its properties in the Inspector:

- **Match**: seed, time limit, deliveries needed to win, ants per side, ant speed, and **Sim Speed** (game seconds per real second, e.g. 2 = double speed).
- **AI**: which controller runs each side (**Baseline**, **Fixed Mix**, or **Strategist**), the role counts used by Fixed Mix, and **Log Role Changes**, which prints every role switch and its reason to the Output panel.

## Run headless (no window)

A full match can be played from the terminal in under a second. Results print to the console. Run this from the repository folder, using your own path to the Godot .NET **console** executable:

```
Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --fixed-fps 60 --quit-after 12000 res://scenes/Main.tscn
```

On macOS the executable is inside the app: `Godot_mono.app/Contents/MacOS/Godot` (use the path of your downloaded app).

`--quit-after 12000` stops after 12,000 frames (200 seconds of game time at 60 frames per second), enough for a full match.

## Run the evaluation (1,000 matches)

Needs Python 3 and the .NET SDK. Close the Godot editor first (or run from a copy of the project folder), then from the repository folder:

```
python tools/run_evaluation.py --godot PATH/TO/Godot_v4.7.2-stable_mono_win64_console.exe
```

It builds the project, plays the matches in parallel headless processes (about 3 minutes with 14 processes), and writes:

- `results/results.csv`: one row per match
- `results/summary.md`: the tables (win rates with 95% confidence intervals, pile-size breakdown, ablations, side check)

What the results show: [docs/EVALUATION.md](docs/EVALUATION.md). To redo the tables from an existing CSV: `python tools/summarize_results.py results/results.csv`. The list of matches is in `scripts/Tools/BatchRunner.cs`.

## Attribution

- **Engine (Godot 4.7.2, MIT license):** the engine itself, its C# bindings (`Godot.NET.Sdk`), and `AStarGrid2D`, which we use for grid pathfinding (`scripts/Sim/Pathfinder.cs` supplies only the step cost, including the climbing penalty). Rendering uses Godot's built-in 2D drawing calls.
- **Our code:** everything in `scenes/`, `scripts/`, `maps/` and `tools/`: the simulation, ant behaviors, team AI interface, Baseline and Fixed Mix opponents, the Strategist, the evaluation runner and scripts, the map generator (Perlin noise, cellular automata, Poisson disk food placement, repair), the validity checker, map measurements and plots. The generator does not use Godot's `FastNoiseLite` or any generation library. Claude Code with Sonnet 5.5 was used to generate scripts for testing, boilerplate code, and to find documentation or generate guides.
- **Others:** no third-party code, assets or libraries.
- **Design references:** Millington, *AI for Games*, 3rd ed. (CRC Press, 2019): multi-tier AI (§6.4.1), rule-based systems with first-applicable arbitration (§5.8, §5.8.5), hysteresis (§6.4.2); Perlin noise (§8.3.3), Poisson disk (§8.1.4), height-maps (§8.3.1), generate and test (§8.4.4).
- **Map generation references:** cellular automata caves from Shaker, Togelius & Nelson, *Procedural Content Generation in Games*, Ch. 3 §3.4 (Springer, 2016), based on Johnson, Yannakakis & Togelius (2010), and the RogueBasin article "Cellular Automata Method for Generating Random Cave-Like Levels".
