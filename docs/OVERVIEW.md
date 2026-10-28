# Ant Farmer: Developer Overview

A top-down 2D ant colony game in Godot 4.7.2 .NET (C#). Two AI-controlled colonies, red and blue, collect food from a grid map and carry it to their nests. Attacker ants can kill enemy carriers and interceptors; killed ants respawn at their nest. See the [README](../README.md) for installing Godot .NET and opening the project.

## Layout

```
maps/arena.txt          Hand-built 41x25 left/right mirror-symmetric arena: 13 food piles, a central hill, small hill patches, and a low hill strip above and below each nest (text format, legend inside)
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
| `MapData.cs` | Static map: grid of `Cell { Terrain, Height }`, nest cells per team, pile positions. `FoodPile` (amount and regrow timer) |
| `MapLoader.cs` | Parses the text map format; sorts nest cells from the map center outward so mirrored nests get mirrored spawn order |
| `MatchSettings.cs` | All tunable rules (time limit, ants per side, speeds, bite range/cooldown, respawn time, defender leash, pile size and regrow ranges, ...) |
| `Simulation.cs` | Match state and tick loop: ants, food piles (seeded size, regrowth), scores, kills, respawns, attack rules (`CanAttack`), win/end checks, `AssignRole` (role changes wait until the ant is at its nest) |
| `Pathfinder.cs` | `AStarGrid2D` wrapper. Rock is solid, diagonal moves allowed (no corner cutting), uphill steps cost extra (`MoveCost`) |
| `Ant.cs` | One ant: position, path following (speed depends on `MoveCost`), carried food (and the pile it came from), role order and any waiting role switch (`PendingOrder`), respawn/cooldown timers |
| `Role.cs` | `Role` enum and `RoleOrder` (role plus optional target pile / post / enemy ant) |
| `GatherBehavior.cs` | Go to the ordered (or cheapest-to-reach) pile, take 1 food, carry it to the nearest nest cell. Also `ReturnHome`, used by any ant walking home to switch roles |
| `DefendBehavior.cs` | Guard the home area: chase and kill any enemy carrier **or enemy interceptor** within `DefendLeash` (12-16 cells, from the seed) of its nest, then return to its post (a resting spot, the nest by default). Never leaves the area or picks up food |
| `InterceptBehavior.cs` | Hunt a specific enemy or the nearest enemy carrier anywhere on the map; with no carriers, shadow the nearest enemy gatherer. Never picks up food |
| `Combat.cs` | Shared chase-and-bite and nearest-target helpers (skip dead ants) |

### Sim/Ai/

The strategy layer only picks **roles**; the role behaviors above do the pathfinding, picking up food, and biting.

| File | Purpose |
|---|---|
| `WorldSnapshot.cs` | Read-only copy of what an AI may see: time, scores, win target, kills, its own ants (with full orders), enemy ants (position, carrying, dead, and **role**, but not their order targets), food piles (including empty ones, with time until they regrow), nests, map, plus `PathCost` (cached per decision) and `CostToStart` (cost for an ant to begin a job, including the walk home if it needs to switch roles) |
| `ITeamController.cs` | `Decide(WorldSnapshot) → AntCommand(antId, RoleOrder)` list. Called every `DecisionSeconds` (0.5 s); orders go through `Simulation.AssignRole`, which ignores orders for enemy ants and queues role changes until the ant reaches its nest |
| `BaselineController.cs` | The opponent to beat: every ant gathers from the cheapest pile, forever |
| `FixedMixController.cs` | Fixed roles from the start (N interceptors, M nest defenders, rest gather); never adapts. The comparison opponent for showing that adapting matters |
| `Controllers.cs` | `ControllerKind` enum and factory used by the Inspector |
| `Strategy/StrategistController.cs` | The adaptive AI. Runs its rules in priority order every decision; unclaimed ants gather. Compares against each ant's next order (including a queued switch). An ant keeps a new order at least `HoldSeconds` (2 s). Keeps at least `MinGatherers` (2) ants gathering. Updates `RecentEvents` before the rules run |
| `Strategy/IStrategyRule.cs` | One rule: `Apply(world, plan)` |
| `Strategy/StrategyPlan.cs` | The orders being built: `FreeAnts`, `Assign(antId, order, reason)` (first rule to claim an ant wins), `AttackerSlots` (attackers still allowed under the gatherer minimum), `Recent`, `SkipRemainingRules` |
| `Strategy/RecentEvents.cs` | Our ants' kills and deaths over the last 20 s, by role, found by comparing each snapshot with the previous one. Also counts gatherers lost inside our home circle |
| `Strategy/GuardRule.cs` | Defend based on our losses, not enemy roles: 1 defender after 2 gatherers die inside our home circle within 20 s, 2 after 4, none after 20 s without home losses. Defenders are added as soon as losses reach the threshold, but dropped at most once per 20 s. New defenders come from gatherers first, so raids aren't interrupted |
| `Strategy/CloseOutRule.cs` | Ahead and within 3 of the win target, or ahead with < 20 s left: every remaining ant gathers and later rules are skipped. **Not in the default rule list**: with role changes only at the nest it cost more than it gained in testing. If added back, put it after Guard |
| `Strategy/RaidRule.cs` | Interceptors based on our results, not enemy roles: start with 1; once per 20 s, add one (max 2) if they got 2+ more kills than deaths, drop one if they died 2+ times and at least as often as they killed, and retry 1 after 20 s at 0 |
| `Strategy/SpreadGatherRule.cs` | Pick each gatherer's pile so no pile gets more ants than it has food left (attackers moved back to gathering pay for the walk home). An empty pile counts if it regrows soon, with the wait added to the trip. Optional `SafetyWeight` (default 0) makes it take contested piles first |

Both sides' snapshots are taken before either side's orders are applied. `Simulation.RoleChanges` counts real role switches per side, and the `RoleChanged` event is printed to the output when **Log Role Changes** is on.

### Extending the AI

See [EXTENDING_AI.md](EXTENDING_AI.md) for how to add strategist rules, new roles, new controllers, and how to test them.

Default rule order: **Guard, Raid, Spread gather**.

Design references (Millington, *AI for Games*, 3rd ed.): strategist on top of role behaviors on top of pathfinding is multi-tier AI (§6.4.1); the rule list is a rule-based system (§5.8) with first-applicable arbitration (§5.8.5); the hold time is hysteresis (§6.4.2).

### View/

| File | Purpose |
|---|---|
| `MapView.cs` | Draws terrain with shapes (lighter = higher ground, contour lines), nests, each nest's defender area (faint circle), and food piles that shrink as they empty (an empty pile is a faint ring until it regrows) |
| `AntView.cs` | Draws ants (team color, head shows direction, green dot = carrying, white ring = defender, yellow ring = interceptor, faded X = killed, waiting to respawn); smooths motion between ticks |
| `Hud.cs` | Score with each side's AI name, clock, info line, end-of-match banner |

## Game rules (defaults)

- **5 ants per side**, spawned on their nest. Every ant starts in the **Gather** role; each side's AI can change roles every 0.5 s.
- Gatherers walk **5 cells/s** on flat ground, interceptors **1.1x** that, defenders **0.8x**. Each level climbed adds one normal step's cost to that step (climbing one level is twice as slow). Going downhill costs nothing extra. Carrying food slows an ant to **70%**.
- **Food:** every pile starts with the same small amount and, once emptied, refills to that amount after a delay. Both come from the **seed**: pile size **3-6** and a base regrow time of **10-20 s**. Piles closer to a nest regrow slower, up to **1.5x** the base time for the pile nearest a nest, which pushes ants toward the middle. Distance is measured to the nearest nest of either side, so both sides always get an equal board. Ants that run a pile dry have to move on, often toward the middle or the enemy side.
- **Win:** first side to **30 deliveries**, or more deliveries when the **180 s** limit runs out. Equal scores are a draw.
- **Roles:** Gatherers collect food and never attack. **Interceptors** attack enemy carriers anywhere. **Defenders** guard an area: they attack enemy carriers and enemy interceptors anywhere within **12-16 cells** (rolled from the seed, the same for both sides) of their nest (the faint circle), never leave it, and wait at their post otherwise. Attackers never pick up food.
- **Bite:** an attacker within **1.2 cells** of a valid target kills it. Each ant can bite once every **2 s ± 0.3 s** (random per bite). Food the victim was carrying goes **back to the pile it came from**.
- **Respawn:** a killed ant respawns at its nest after **3 s ± 0.5 s** (random per death), keeping its role.
- **Switching roles happens at the nest.** An ant given a new role walks home (delivering any food on the way), then switches; a thin arc over it shows the role it is heading home for. A new target within the same role (another pile, post or enemy) applies immediately, and dead ants switch at once since they respawn at the nest.

### Fairness

The arena is mirror-symmetric, and nothing in the simulation favors a side:

- Each ant's starting nest cell is shuffled by the seed (cells are listed from the map center outward, so both nests have the same choices).
- Ant update order flips every tick.
- Ants see other ants (position, cell, carrying) as they were at the **start of the tick**.
- Bites are declared during the tick and all **resolve together** at the end, so an ant killed this tick still lands its own bite. Ants only die (end of tick) and respawn (start of tick) between ticks.
- Respawns cycle through the nest cells in the same center-out order as the first spawn.
- Winners are checked only after every ant has acted.

**Randomness:** everything random comes from one generator seeded with `MatchSettings.Seed`, so the same seed always replays the same match. Per match: pile size, base regrow time, and defender area. During the match: starting cells, respawn times and bite cooldowns. The random draws aren't mirrored, so swapping sides gives a different match with the same odds; evaluations should play each seed from both sides.
- Equal path costs (within 0.001) are decided by straight-line distance, not list order.

## Status

**Done:** map format and loader, shape rendering, fixed-tick simulation, grid pathfinding with climb costs, ants with gathering, two colonies with HUD and win condition, combat with kills and respawns, role switching at the nest, the Gather / Defend / Intercept roles with role speeds and a defender leash, the team AI interface with Baseline and Fixed Mix controllers, and the rule-based Strategist (Guard, Raid, Spread gather).

Strategist results before seeded regrowing food (13 piles of 10, first to 66; deterministic, each matchup played from both sides). These need re-running with the current rules:

| Opponent | Strategist as red | Strategist as blue |
|---|---|---|
| Baseline | 66-20 (158 s) | 66-20 (158 s) |
| Fixed 1 Interceptor | 52-7 (time) | 53-7 (time) |
| Fixed 2 Interceptors | 40-5 (time) | 40-5 (time) |
| Fixed 1 Defender + 1 Interceptor | 62-26 (time) | 66-22 (172 s) |
| Fixed 1 Defender | 66-54 (120 s) | 66-54 (120 s) |

Rule ablations: without **Raid** it only draws Baseline (65-65) and loses to Fixed 2 Interceptors 62-66; without **Guard** it draws Fixed 2 Interceptors 4-4; without **Spread gather** results barely change. Adding **Close out** back lowers the margin against Baseline (66-26 instead of 66-20).

Check with seeded randomness (spawn cells, respawn and bite-cooldown jitter, defender area 12-16), results-based Guard and Raid, first to 30, regrowing food (20 seeds × both sides per opponent, 200 matches): the strategist wins all 40 against Baseline (avg 30-20) and Fixed 2I (30-17), 38 of 40 against Fixed 1I (30-19), 39 of 40 against Fixed 1D (30-27), but only **9 of 40 against Fixed 1D+1I** (avg 28-30). It changes roles mid-match (about 5-10 role switches per match against teams with attackers). Against 1D+1I its Guard only reacts after losses, while the opponent defends from the start. A larger probe of that matchup (50 seeds × both sides) puts the strategist at about **30%** wins whether or not the defender area and timing jitter vary, so the randomness didn't cause the drop; the smaller samples were noisy. Pile size matters most: it wins about half the time with piles of 4-5 and almost never with piles of 3 or 6.

**Balance note:** interceptors are strong (they always catch carriers, since carriers move at 0.7x). With first to 30 no match in the checks reached the time limit, but interceptor-heavy matchups take about twice as long. Knobs if this should change: a longer bite cooldown, a shorter respawn, or slower interceptors.

**Known issue:** when two paths cost the same, Godot's A* picks the same one no matter which side the ant is on, so mirrored ants can take different (equal-length) routes. Baseline vs. Baseline and Fixed 2I vs. Fixed 2I still play exactly mirrored, but Strategist vs. Strategist and Fixed 1D+1I vs. itself don't. Swapping sides in evaluation cancels this out.

**Running many matches:** threads don't speed up batches, because every A* step calls back into C# and Godot runs those calls one at a time. Run several headless Godot processes instead, each playing its own share of the matches (pass the share on the command line after `--` and read it with `OS.GetCmdlineUserArgs()`), then combine their output. 14 processes ran 360 matches in under 2 minutes.

**Not done yet:**
- **Batch evaluation.** A headless runner for many seeded matches with side swapping and CSV output.
