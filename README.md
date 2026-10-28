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

## Documentation

- [docs/OVERVIEW.md](docs/OVERVIEW.md): how the code is organized, game rules, the Strategist
- [docs/EXTENDING_AI.md](docs/EXTENDING_AI.md): how the AI works and how to add rules, roles, and controllers
- [docs/EVALUATION.md](docs/EVALUATION.md): evaluation results

## Attribution

- **Engine (Godot 4.7.2, MIT license):** the engine itself, its C# bindings (`Godot.NET.Sdk`), and `AStarGrid2D`, which we use for grid pathfinding (`scripts/Sim/Pathfinder.cs` supplies only the step cost, including the climbing penalty). Rendering uses Godot's built-in 2D drawing calls.
- **Our code:** everything in `scenes/`, `maps/` and `tools/`: the simulation, ant behaviors, team AI interface, Baseline and Fixed Mix opponents, the Strategist, the evaluation runner and scripts. Claude Code with Sonnet 5.5 was used to generate scripts for testing, boilerplate code, and to find documentation or generate guides.
- **Others:** no third-party code, assets or libraries.
- **Design references:** Millington, *AI for Games*, 3rd ed. (CRC Press, 2019): multi-tier AI (§6.4.1), rule-based systems with first-applicable arbitration (§5.8, §5.8.5), hysteresis (§6.4.2).
