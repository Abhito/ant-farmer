# Ant Farmer

A Godot 4.7 project. This repo uses C#, so you must use the **.NET** build of Godot, not the standard editor.

## Requirements

1. **Godot 4.7 .NET** (C# support)
   - Download **Godot Engine – .NET** from [https://godotengine.org/download](https://godotengine.org/download).
   - The file name includes `mono` (for example, `Godot_v4.7.2-stable_mono_win64.exe` on Windows).
   - Do **not** use the standard Godot download. That build does not include C# / .NET support and cannot open this project correctly.
2. **.NET SDK 8 or later**
   - Godot's .NET editor does not include the C# compiler. Install the latest stable SDK from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download).
   - Use the 64-bit SDK if you are using the 64-bit Godot editor.

Godot is self-contained: extract the zip and run the executable. No installer is required.

## Import and open the project

1. Clone this repository (or download and unzip it) to a local folder.
2. Launch the **Godot .NET** editor. The Project Manager window opens first.
3. Click **Import**.
4. Browse to this repository and select `project.godot`, then click **Import & Edit** (or **Open**).
5. Godot will import assets and open the **Ant Farmer** editor.

After the first import, the project appears in the Project Manager list. Double-click it (or select it and click **Edit**) to open it again.

If Godot asks you to choose an editor version, pick **4.7 .NET**. Opening the project with a standard (non-.NET) editor will fail or disable C# features.

## Run a match

1. Click **Run Project** (or press **F5**). The main scene is `scenes/Main.tscn`.
2. Two AI colonies, red and blue, play against each other. The match ends when one side delivers 30 food, or the time limit is reached. Food piles start small and regrow after being emptied; pile size and regrow time come from the seed.

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

`--quit-after 12000` stops after 12,000 frames (200 seconds of game time at 60 frames per second), enough for a full match.

See [docs/OVERVIEW.md](docs/OVERVIEW.md) for how the code is organized, and [docs/EXTENDING_AI.md](docs/EXTENDING_AI.md) for how to add AI rules, roles, and controllers.
