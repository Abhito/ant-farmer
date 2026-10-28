# Ant Farmer: Developer Overview

A top-down 2D ant colony game in Godot 4.7.2 .NET (C#). Two AI colonies, red and blue, race to bring food home. Some ants can be made attackers that kill enemy carriers. See the [README](../README.md) for setup.

## Project layout

```
maps/arena.txt          The map (text format, legend inside)
scenes/Main.tscn        Main scene
scripts/GameRunner.cs   Starts a match, steps it on a fixed clock, connects the views
scripts/Sim/            Game logic (plain C#, no drawing)
scripts/Sim/Ai/         Team AIs and what they can see and command
scripts/View/           Drawing only (reads the game, never changes it)
```

## How it fits together

- **Logic and drawing are separate.** `Simulation` holds the whole match and advances it 10 times a second. It never looks at frame time, so a match plays the same in the editor, sped up, or headless.
- **Everything works on the grid.** Only `MapView.GridToWorld` converts grid cells to screen positions.
- **Maps are data.** `MapLoader.Parse(text)` builds a map from text, so generated maps can be loaded the same way.
- **Three layers of control** (Millington, *AI for Games*, §6.4.1):

```
Team AI (every 0.5 s)     decides which ROLE each ant has
Role behaviors (every tick)   decide HOW: where to walk, what to pick up, whom to bite
Pathfinder (A*)           finds the route
```

### Game logic: `scripts/Sim/`

| File | What it does |
|---|---|
| `Simulation.cs` | The match: ants, food, scores, kills, respawns, attack rules, win check, role changes |
| `MatchSettings.cs` | Every tunable number (speeds, timers, ranges) |
| `MapData.cs`, `MapLoader.cs` | The map and its loader |
| `Pathfinder.cs` | Wraps Godot's `AStarGrid2D`; rock blocks, climbing costs extra |
| `Ant.cs` | One ant: position, path, carried food, role, timers |
| `Role.cs` | The roles and `RoleOrder` (a role plus an optional target) |
| `GatherBehavior.cs` | Go to a pile, take one food, carry it home |
| `DefendBehavior.cs` | Wait at a post; chase enemies that enter the home area |
| `InterceptBehavior.cs` | Hunt enemy carriers anywhere |
| `Combat.cs` | Shared chase-and-bite helpers |

### Team AI: `scripts/Sim/Ai/`

| File | What it does |
|---|---|
| `WorldSnapshot.cs` | What an AI can see (a read-only copy of the board) |
| `ITeamController.cs` | The AI interface: look at the snapshot, return role orders |
| `BaselineController.cs` | Opponent to beat: everyone gathers |
| `FixedMixController.cs` | Fixed roles all match (e.g. 1 defender + 1 interceptor) |
| `Strategy/` | The adaptive Strategist and its rules (see below) |

### Drawing: `scripts/View/`

| File | What it draws |
|---|---|
| `MapView.cs` | Terrain, nests, defender areas (faint circles), food piles |
| `AntView.cs` | Ants: team color, role ring (white = defender, yellow = interceptor), green dot = carrying, X = dead |
| `Hud.cs` | Score, clock, match info |

## The Strategist

Every half second it runs three rules in order. Each rule can claim ants that earlier rules left free. Ants nobody claims gather.

| Rule | What it does |
|---|---|
| **Guard** | Adds a defender when our gatherers keep dying near home (2 losses in 20 s; 2 defenders at 4). Drops it after 20 s with no losses. |
| **Raid** | Starts with 1 interceptor. Every 20 s: adds one if they're getting kills, drops one if they keep dying. |
| **Spread gather** | Sends each remaining ant to the best pile, without crowding one pile. |

It reacts to what happens to **its own ants**, not to what roles the enemy has. Two guards against flip-flopping: an ant keeps a new order for at least 2 s, and defenders are dropped slowly (both are hysteresis, §6.4.2). At least 2 ants always gather.

The rules are a rule-based system with first-applicable arbitration (Millington §5.8, §5.8.5). [EXTENDING_AI.md](EXTENDING_AI.md) explains how to add your own.

## Game rules

| | |
|---|---|
| **Teams** | 5 ants per side, all start as gatherers |
| **Win** | First to 30 deliveries, or most after 180 s |
| **Speed** | 5 cells/s. Interceptors 1.1×, defenders 0.8×, carrying food 0.7×. Climbing a level takes twice as long |
| **Food** | Piles start small and refill after being emptied. Piles near a nest refill up to 1.5× slower |
| **Gatherers** | Collect food, never attack |
| **Interceptors** | Kill enemy carriers anywhere |
| **Defenders** | Kill enemy carriers and interceptors inside their home area (the faint circle), never leave it |
| **Bites** | Within 1.2 cells; about one bite per 2 s. The victim's food goes back to its pile |
| **Respawn** | About 3 s, at the nest, keeping its role |
| **Role changes** | Only at the nest: an ant given a new role walks home first (a thin arc shows the new role) |

### The seed

The same seed always replays the same match. Different seeds change:

| Once per match | During the match |
|---|---|
| Pile size (3-6) | Which nest cell each ant starts on |
| Refill time (10-20 s) | Respawn time (3 s ± 0.5) |
| Defender area (12-16 cells) | Bite cooldown (2 s ± 0.3) |

### Fairness

The map is mirrored left/right, and neither side gets an edge: ants see each other as they were at the start of the tick, all bites land at the same time, the update order flips every tick, and ties are broken fairly. Random draws aren't mirrored, so always **play each seed from both sides**.

## Status

All game features and the Strategist are done. Next is the batch evaluation.

**Development checks** (seeds 1-50, also used for tuning, so not the evaluation):

| Opponent | Strategist wins |
|---|---|
| Baseline | 40 / 40 |
| Fixed 1 interceptor | 38 / 40 |
| Fixed 2 interceptors | 40 / 40 |
| Fixed 1 defender | 39 / 40 |
| Fixed 1 defender + 1 interceptor | about 30% (100 matches) |

Against 1 defender + 1 interceptor, pile size decides most: the Strategist wins about half the time with piles of 4-5, almost never with 3 or 6. Small samples swing a lot (20 seeds gave anywhere from 9 to 20 wins out of 40), so use many seeds.

**Known quirks**
- Interceptors are strong: carriers can't outrun them. Interceptor-heavy matches take about twice as long.
- Interceptors have little safe ground against a team with a defender. The defender areas cover most of the map: only 12-35% of walkable cells (radius 16 vs. 12) and 3-7 of the 13 piles lie outside both areas. Carriers always head home into their own area, so an interceptor chasing one usually ends up near the enemy defender. In the evaluation, the Strategist did better against Fixed 1D+1I without its Raid rule (87% vs. 40%). Ideas for later: maps with more open ground (a map generator could vary this), or interceptors that avoid defended areas.
- When two routes cost the same, A* picks the same one for both sides, so mirrored ants can walk different routes. Playing both sides evens this out.

## Evaluation plan

Results and what they mean: [EVALUATION.md](EVALUATION.md).

| Run | Matches |
|---|---|
| **Main:** Strategist vs. all 5 opponents, seeds **1001-1070** (not the tuning seeds), both sides | 700 |
| **Ablations:** adaptation off (`new GuardRule { Adaptive = false }` and `new RaidRule { Adaptive = false }`: 0 defenders and 1 interceptor all match), no Raid, no Guard (leave the rule out of the list passed to `new StrategistController(rules)`) | 3 variants × Baseline, Fixed 2I, Fixed 1D+1I × seeds 1001-1015 × both sides = 270 |
| **Sanity:** Strategist vs. Strategist, seeds 1001-1015, both sides (should be about even) | 30 |

The runner is `scenes/BatchRunner.tscn` (`scripts/Tools/BatchRunner.cs`), started by `tools/run_evaluation.py`; see the README. It records for each match: seed, side, controllers, winner, scores, length, how it ended, kills, role switches, pile size, refill time, defender area. Report win rates with 95% confidence intervals.

**Running many matches:** threads don't help (Godot runs the A* callbacks one at a time). Run several headless Godot processes, each with its own share of matches (pass it after `--`, read it with `OS.GetCmdlineUserArgs()`), and merge the output. 14 processes ran 200 matches in about 2 minutes. If the editor is open, run from a copy of the project folder.
