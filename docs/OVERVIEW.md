# Ant Farmer: Developer Overview

A top-down 2D ant colony game in Godot 4.7.2 .NET (C#). Two AI-controlled colonies, red and blue, collect food from a grid map and carry it to their nests. Ants can also steal food from enemy carriers. See the [README](../README.md) for installing Godot .NET and opening the project.

## Running it

- Open the project in **Godot 4.7.2 .NET** and press **F5**. The main scene is `scenes/Main.tscn`.
- **R** restarts the match. **Space** pauses.
- Match settings (seed, time limit, ants per side, speed, sim speed) and each side's AI (**AI** group) are in the Inspector on the `GameRunner` node.
- Headless run, which plays a full match in under a second:
  ```
  Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --fixed-fps 60 --quit-after 12000 res://scenes/Main.tscn
  ```

## Layout

```
maps/arena.txt          Hand-built, left/right mirror-symmetric arena (text format, legend inside)
scenes/Main.tscn        Main scene: GameRunner, MapView, AntView, Hud, Camera2D
scripts/GameRunner.cs   Loads the map, creates the Simulation, steps it on a fixed clock, wires up the views
scripts/Sim/            Game logic: plain C#, no nodes, no drawing
scripts/Sim/Ai/         Team AIs and the interface they use to see and command the game
scripts/View/           Drawing only: reads Sim state, never changes it
```

## Architecture

The main rule: **game logic (`Sim/`) is separate from rendering (`View/`).**

- `Simulation` holds the whole match state and advances it with `Step()` at a fixed **10 ticks/s**. It never reads frame time. A match depends only on its seed and `MatchSettings`, so it plays out the same in the editor, at 10x speed, or headless.
- All game logic works in **grid coordinates**. Grid-to-screen conversion happens in exactly one place, `MapView.GridToWorld` / `WorldToGrid`. A different renderer could replace the views without touching `Sim/`.
- Game rules don't use physics bodies or `Area2D`. Distances and arrival checks are computed on the grid.
- Maps are loaded from data at runtime (`MapLoader.Parse(text)`), so generated maps can be passed in as text.

### Sim/

| File | Purpose |
|---|---|
| `MapData.cs` | Static map: grid of `Cell { Terrain, Height }`, nest cells per team, starting food |
| `MapLoader.cs` | Parses the text map format; sorts nest cells from the map center outward so mirrored nests get mirrored spawn order |
| `MatchSettings.cs` | All tunable rules (time limit, ants per side, speeds, bite range/cooldown, stun, defend radius, ...) |
| `Simulation.cs` | Match state and tick loop: ants, food piles, scores, bites, win/end checks, `AssignRole` |
| `Pathfinder.cs` | `AStarGrid2D` wrapper. Rock is solid, diagonal moves allowed (no corner cutting), uphill steps cost extra (`MoveCost`) |
| `Ant.cs` | One ant: position, path following (speed depends on `MoveCost`), carried food, role order, stun/cooldown timers |
| `Role.cs` | `Role` enum and `RoleOrder` (role plus optional target pile / post / enemy ant) |
| `GatherBehavior.cs` | Go to the ordered (or cheapest-to-reach) pile, take 1 food, carry it to the nearest nest cell |
| `DefendBehavior.cs` | Guard a post; chase and bite enemy carriers that come within `DefendRadius` |
| `InterceptBehavior.cs` | Hunt a specific enemy or the nearest enemy carrier anywhere on the map |
| `Combat.cs` | Shared chase-and-bite and nearest-enemy helpers |

### Sim/Ai/

The strategy layer only picks **roles**; the role behaviors above do the pathfinding, picking up food, and biting.

| File | Purpose |
|---|---|
| `WorldSnapshot.cs` | Read-only copy of what an AI may see: time, scores, win target, bites, its own ants (with orders), enemy ants (position/carrying/stunned, **not** their orders), food piles, nests, map, and a `PathCost` helper |
| `ITeamController.cs` | `Decide(WorldSnapshot) → AntCommand(antId, RoleOrder)` list. Called every `DecisionSeconds` (0.5 s); orders go through `Simulation.AssignRole`, which ignores orders for enemy ants |
| `BaselineController.cs` | The opponent to beat: every ant gathers from the cheapest pile, forever |
| `FixedMixController.cs` | Fixed roles from the start (N interceptors, M nest defenders, rest gather); never adapts |
| `Controllers.cs` | `ControllerKind` enum and factory used by the Inspector |

Both sides' snapshots are taken before either side's orders are applied. `Simulation.RoleChanges` counts real role switches per side, and the `RoleChanged` event is printed to the output when **Log Role Changes** is on.

### View/

| File | Purpose |
|---|---|
| `MapView.cs` | Draws terrain with shapes (lighter = higher ground, contour lines), nests, and food piles that shrink as they empty |
| `AntView.cs` | Draws ants (team color, head shows direction, green dot = carrying, white ring = defender, yellow ring = interceptor, grey = stunned); smooths motion between ticks |
| `Hud.cs` | Score with each side's AI name, clock, info line, end-of-match banner |

## Game rules (defaults)

- **3 ants per side**, spawned on their nest. Every ant starts in the **Gather** role; each side's AI can change roles every 0.5 s.
- Ants walk **3 cells/s** on flat ground. Each level climbed adds one normal step's cost to that step (climbing one level is twice as slow). Going downhill costs nothing extra. Carrying food slows an ant to **75%**.
- **Win:** first side to deliver **more than half the map's food** (61 of 120 on the arena), or more deliveries when the **180 s** limit runs out. The match also ends once the map is empty and nobody is carrying food. Equal scores are a draw.
- **Bite:** an empty-handed ant within **1.2 cells** of an enemy carrier steals its food. The victim is stunned for **1.5 s**. Each ant can bite once every **2 s**. Only Defend and Intercept ants bite.

### Fairness

The arena is mirror-symmetric, and the simulation is built so that identical AIs play exactly mirrored matches and draw:

- Spawn cells are ordered from the map center outward, not in reading order.
- Ant update order flips every tick.
- Ants see other ants (position, cell, carrying) as they were at the **start of the tick**.
- Bites are declared during the tick and all **resolve together** at the end.
- Winners are checked only after every ant has acted.
- Equal path costs (within 0.001) are decided by straight-line distance, not list order.

## Status

**Done:** map format and loader, shape rendering, fixed-tick simulation, grid pathfinding with climb costs, ants with gathering, two colonies with HUD and win condition, bite/steal combat, the Gather / Defend / Intercept roles, and the team AI interface with Baseline and Fixed Mix controllers.

**Not done yet:**
- **Strategist.** An adaptive controller that reassigns roles as the match changes.
- **Batch evaluation.** A headless runner for many seeded matches with side swapping and CSV output.
