# Ant Farmer: Developer Overview

A top-down 2D ant colony game in Godot 4.7.2 .NET (C#). Two AI-controlled colonies, red and blue, collect food from a grid map and carry it to their nests. Attacker ants can kill enemy carriers and interceptors; killed ants respawn at their nest. See the [README](../README.md) for installing Godot .NET and opening the project.

## Layout

```
maps/arena.txt          Hand-built 41x25 left/right mirror-symmetric arena, 13 piles of 10 (text format, legend inside)
scenes/Main.tscn        Main scene: GameRunner, MapView, AntView, Hud, Camera2D
scripts/GameRunner.cs   Loads the map, creates the Simulation, steps it on a fixed clock, wires up the views
scripts/Sim/            Game logic: plain C#, no nodes, no drawing
scripts/Sim/Ai/         Team AIs and the interface they use to see and command the game
scripts/View/           Drawing only: reads Sim state, never changes it
```

## Architecture

The main rule: **game logic (`Sim/`) is separate from rendering (`View/`).**

- `Simulation` holds the whole match state and advances it with `Step()` at a fixed **10 ticks/s**. It never reads frame time. A match depends only on its seed and `MatchSettings`, so it plays out the same in the editor, at 10x speed, or headless.
- All game logic works in **grid coordinates**. Grid-to-screen conversion happens in exactly one place, `MapView.GridToWorld`. A different renderer could replace the views without touching `Sim/`.
- Game rules don't use physics bodies or `Area2D`. Distances and arrival checks are computed on the grid.
- Maps are loaded from data at runtime (`MapLoader.Parse(text)`), so generated maps can be passed in as text.

### Sim/

| File | Purpose |
|---|---|
| `MapData.cs` | Static map: grid of `Cell { Terrain, Height }`, nest cells per team, starting food |
| `MapLoader.cs` | Parses the text map format; sorts nest cells from the map center outward so mirrored nests get mirrored spawn order |
| `MatchSettings.cs` | All tunable rules (time limit, ants per side, speeds, bite range/cooldown, respawn time, defend radius and leash, ...) |
| `Simulation.cs` | Match state and tick loop: ants, food piles, scores, kills, respawns, attack rules (`CanAttack`), win/end checks, `AssignRole` (role changes wait until the ant is at its nest) |
| `Pathfinder.cs` | `AStarGrid2D` wrapper. Rock is solid, diagonal moves allowed (no corner cutting), uphill steps cost extra (`MoveCost`) |
| `Ant.cs` | One ant: position, path following (speed depends on `MoveCost`), carried food (and the pile it came from), role order and any waiting role switch (`PendingOrder`), respawn/cooldown timers |
| `Role.cs` | `Role` enum and `RoleOrder` (role plus optional target pile / post / enemy ant) |
| `GatherBehavior.cs` | Go to the ordered (or cheapest-to-reach) pile, take 1 food, carry it to the nearest nest cell. Also `ReturnHome`, used by any ant walking home to switch roles |
| `DefendBehavior.cs` | Guard a post; chase and kill enemy carriers **and enemy interceptors** that come within `DefendRadius`. Stays within `DefendLeash` (14 cells) of its nest. Never picks up food |
| `InterceptBehavior.cs` | Hunt a specific enemy or the nearest enemy carrier anywhere on the map; with no carriers, shadow the nearest enemy gatherer. Never picks up food |
| `Combat.cs` | Shared chase-and-bite and nearest-target helpers (skip dead ants) |

### Sim/Ai/

The strategy layer only picks **roles**; the role behaviors above do the pathfinding, picking up food, and biting.

| File | Purpose |
|---|---|
| `WorldSnapshot.cs` | Read-only copy of what an AI may see: time, scores, win target, kills, its own ants (with full orders), enemy ants (position, carrying, dead, and **role**, but not their order targets), food piles, nests, map, plus `PathCost` (cached per decision) and `CostToStart` (cost for an ant to begin a job, including the walk home if it needs to switch roles) |
| `ITeamController.cs` | `Decide(WorldSnapshot) → AntCommand(antId, RoleOrder)` list. Called every `DecisionSeconds` (0.5 s); orders go through `Simulation.AssignRole`, which ignores orders for enemy ants and queues role changes until the ant reaches its nest |
| `BaselineController.cs` | The opponent to beat: every ant gathers from the cheapest pile, forever |
| `FixedMixController.cs` | Fixed roles from the start (N interceptors, M nest defenders, rest gather); never adapts. The comparison opponent for showing that adapting matters |
| `Controllers.cs` | `ControllerKind` enum and factory used by the Inspector |
| `Strategy/StrategistController.cs` | The adaptive AI. Runs its rules in priority order every decision; unclaimed ants gather. Compares against each ant's next order (including a queued switch). An ant keeps a new order at least `HoldSeconds` (2 s) |
| `Strategy/IStrategyRule.cs` | One rule: `Apply(world, plan)` |
| `Strategy/StrategyPlan.cs` | The orders being built: `FreeAnts`, `Assign(antId, order, reason)` (first rule to claim an ant wins), `SkipRemainingRules` |
| `Strategy/GuardRule.cs` | One defender per enemy interceptor (max 1 by default), picked by who can start soonest (ants already defending first). Posts next to the interceptor when it is inside our defender area (otherwise waits at its post or the nest), and keeps the post while the interceptor stays within reach. A defender stays in role while its interceptor is dead (it respawns in 3 s) |
| `Strategy/CloseOutRule.cs` | Ahead and within 3 of the win target, or ahead with < 20 s left: every remaining ant gathers and later rules are skipped. **Not in the default rule list**: with role changes only at the nest it cost more than it gained in testing. If added back, put it after Guard |
| `Strategy/RaidRule.cs` | Keep a standing number of interceptors (2 while the enemy has no defenders, 1 once it has one, since defenders kill interceptors). Ants already intercepting keep the job; new ones are taken from whoever is closest to the nest |
| `Strategy/SpreadGatherRule.cs` | Pick each gatherer's pile so no pile gets more ants than it has food left (attackers moved back to gathering pay for the walk home). Optional `SafetyWeight` (default 0) makes it take contested piles first |

Both sides' snapshots are taken before either side's orders are applied. `Simulation.RoleChanges` counts real role switches per side, and the `RoleChanged` event is printed to the output when **Log Role Changes** is on.

### Extending the AI

See [EXTENDING_AI.md](EXTENDING_AI.md) for how to add strategist rules, new roles, new controllers, and how to test them.

Default rule order: **Guard, Raid, Spread gather**.

Design references (Millington, *AI for Games*, 3rd ed.): strategist on top of role behaviors on top of pathfinding is multi-tier AI (§6.4.1); the rule list is a rule-based system (§5.8) with first-applicable arbitration (§5.8.5); the hold time is hysteresis (§6.4.2).

### View/

| File | Purpose |
|---|---|
| `MapView.cs` | Draws terrain with shapes (lighter = higher ground, contour lines), nests, each nest's defender area (faint circle), and food piles that shrink as they empty |
| `AntView.cs` | Draws ants (team color, head shows direction, green dot = carrying, white ring = defender, yellow ring = interceptor, faded X = killed, waiting to respawn); smooths motion between ticks |
| `Hud.cs` | Score with each side's AI name, clock, info line, end-of-match banner |

## Game rules (defaults)

- **5 ants per side**, spawned on their nest. Every ant starts in the **Gather** role; each side's AI can change roles every 0.5 s.
- Gatherers walk **5 cells/s** on flat ground, interceptors **1.1x** that, defenders **0.8x**. Each level climbed adds one normal step's cost to that step (climbing one level is twice as slow). Going downhill costs nothing extra. Carrying food slows an ant to **70%**.
- **Win:** first side to deliver **more than half the map's food** (66 of 130 on the arena), or more deliveries when the **180 s** limit runs out. The match also ends once the map is empty and nobody is carrying food. Equal scores are a draw.
- **Roles:** Gatherers collect food and never attack. **Interceptors** attack enemy carriers anywhere. **Defenders** attack enemy carriers and enemy interceptors near their post, and never go more than **14 cells** from their nest (the faint circle). Attackers never pick up food.
- **Bite:** an attacker within **1.2 cells** of a valid target kills it. Each ant can bite once every **2 s**. Food the victim was carrying goes **back to the pile it came from**.
- **Respawn:** a killed ant respawns at its nest after **3 s**, keeping its role.
- **Switching roles happens at the nest.** An ant given a new role walks home (delivering any food on the way), then switches; a thin arc over it shows the role it is heading home for. A new target within the same role (another pile, post or enemy) applies immediately, and dead ants switch at once since they respawn at the nest.

### Fairness

The arena is mirror-symmetric, and the simulation is built so that identical AIs play exactly mirrored matches and draw:

- Spawn cells are ordered from the map center outward, not in reading order.
- Ant update order flips every tick.
- Ants see other ants (position, cell, carrying) as they were at the **start of the tick**.
- Bites are declared during the tick and all **resolve together** at the end, so an ant killed this tick still lands its own bite. Ants only die (end of tick) and respawn (start of tick) between ticks.
- Respawns cycle through the nest cells in the same center-out order as the first spawn.
- Winners are checked only after every ant has acted.
- Equal path costs (within 0.001) are decided by straight-line distance, not list order.

## Status

**Done:** map format and loader, shape rendering, fixed-tick simulation, grid pathfinding with climb costs, ants with gathering, two colonies with HUD and win condition, combat with kills and respawns, role switching at the nest, the Gather / Defend / Intercept roles with role speeds and a defender leash, the team AI interface with Baseline and Fixed Mix controllers, and the rule-based Strategist (Guard, Raid, Spread gather).

Strategist results on the arena with default settings (deterministic; each matchup played from both sides). It wins every matchup:

| Opponent | Strategist as red | Strategist as blue |
|---|---|---|
| Baseline | 66-20 (158 s) | 66-20 (158 s) |
| Fixed 1 Interceptor | 52-7 (time) | 53-7 (time) |
| Fixed 2 Interceptors | 40-5 (time) | 40-5 (time) |
| Fixed 1 Defender + 1 Interceptor | 62-26 (time) | 66-22 (172 s) |
| Fixed 1 Defender | 66-54 (120 s) | 66-54 (120 s) |

Rule ablations: without **Raid** it only draws Baseline (65-65) and loses to Fixed 2 Interceptors 62-66; without **Guard** it draws Fixed 2 Interceptors 4-4; without **Spread gather** results barely change. Adding **Close out** back lowers the margin against Baseline (66-26 instead of 66-20).

**Balance note:** interceptors are still very strong. Against teams that don't defend, matches reach 66, but matchups with enemy interceptors mostly end on the time limit with lots of food left, and Fixed 2 Interceptors vs. itself ends 4-4. Knobs if this should change: a longer bite cooldown, a shorter respawn, or slower interceptors.

**Known issue:** when two paths cost the same, Godot's A* picks the same one no matter which side the ant is on, so mirrored ants can take different (equal-length) routes. Baseline vs. Baseline and Fixed 2I vs. Fixed 2I still play exactly mirrored, but Strategist vs. Strategist and Fixed 1D+1I vs. itself don't. Swapping sides in evaluation cancels this out.

**Running many matches:** threads don't speed up batches, because every A* step calls back into C# and Godot runs those calls one at a time. Run several headless Godot processes instead, each playing its own share of the matches (pass the share on the command line after `--` and read it with `OS.GetCmdlineUserArgs()`), then combine their output. 14 processes ran 360 matches in under 2 minutes.

**Not done yet:**
- **Batch evaluation.** A headless runner for many seeded matches with side swapping and CSV output.
