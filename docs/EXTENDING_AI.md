# Extending the AI

How to add strategist rules, new roles, and new team controllers. Read [OVERVIEW.md](OVERVIEW.md) first for the layout of the code and the game rules.

## How the layers fit together

```
ITeamController.Decide(WorldSnapshot)        every 0.5 s, once per side
  └─ StrategistController                    runs its rules in priority order
       └─ IStrategyRule.Apply(world, plan)    each rule claims free ants: plan.Assign(id, order, reason)
  → list of AntCommand(antId, RoleOrder)
Simulation.AssignRole                        same role: applies now; new role: queued until the ant is at its nest
Role behaviors (Gather/Defend/Intercept)     every tick: pathfinding, picking up food, chasing, biting
```

The AI only decides **which role each ant has** (plus an optional target: a pile, a post, or an enemy ant). It never moves ants itself. Everything below the controller is game rules, and everything above it is strategy. This split is the multi-tier AI from Millington, *AI for Games*, §6.4.1.

Things every AI has to live with:

- **Roles only change at the nest.** An ant given a new role walks home first (delivering any food on the way). A new target within the same role applies at once, and dead ants switch immediately. Use `world.CostToStart(...)` to account for the walk.
- **The AI sees the board as it was at the start of the tick**, through a read-only `WorldSnapshot`. Enemy roles are visible; enemy targets aren't.
- **Orders for enemy ants are ignored.**
- **Matches must stay replayable.** Never use `System.Random`, `GD.Randf()` or the clock. If a rule needs randomness, make a `new System.Random(world.Settings.Seed)` once in its constructor or first call, so the same seed gives the same match.

## Adding a strategist rule

This is the most common change, and it doesn't touch the game rules.

### 1. Write the rule

Create a class in `scripts/Sim/Ai/Strategy/` that implements `IStrategyRule`:

```csharp
using System.Linq;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>While the enemy has interceptors, guard the pile most of our gatherers use.</summary>
public class GuardPileRule : IStrategyRule
{
    public string Name => "Guard pile";

    public void Apply(WorldSnapshot world, StrategyPlan plan)
    {
        if (!world.EnemyAnts.Any(e => e.Role == Role.Intercept))
            return;

        int? busiestId = world.MyAnts
            .Where(a => a.Order is { Role: Role.Gather, FoodId: not null })
            .GroupBy(a => a.Order.Value.FoodId.Value)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => (int?)g.Key)
            .FirstOrDefault();
        if (busiestId is not int id)
            return;

        FoodInfo pile = world.Food.First(f => f.Id == id);
        if (world.DistanceFromMyNest(pile.Cell) > world.DefendLeash)
            return;

        AntInfo? guard = plan.FreeAnts
            .Where(a => !a.Carrying)
            .OrderBy(a => world.CostToStart(a, Role.Defend, pile.Cell))
            .ThenBy(a => a.Id)
            .Cast<AntInfo?>()
            .FirstOrDefault();

        if (guard is AntInfo ant)
            plan.Assign(ant.Id, RoleOrder.Defend(pile.Cell), $"guard pile {pile.Id}, enemy has interceptors");
    }
}
```

### 2. Add it to the rule list

In `StrategistController.CreateDefaultRules()`, insert it where it belongs. **Earlier rules get first pick of the ants**: a rule can only claim ants no earlier rule took (first-applicable arbitration, Millington §5.8.5). The default order is:

1. `GuardRule`: defenders when our gatherers keep dying at home (protect the economy first)
2. `RaidRule`: interceptors, adjusted by their kills and deaths
3. `SpreadGatherRule`: everyone left gathers, spread across piles

Ants that no rule claims gather.

### 3. Check it helps

See [Testing a change](#testing-a-change). At minimum, play strategist vs. Baseline and vs. Fixed 2 Interceptors from **both sides**, with and without your rule.

### What a rule can use

**`StrategyPlan plan`**

| Member | Use |
|---|---|
| `FreeAnts` | Our ants no earlier rule has claimed, in id order. Includes dead ants (check `Dead`) |
| `Assign(antId, order, reason)` | Claim a free ant. Returns false if it was already taken. The reason is printed when the role changes (Inspector: **Log Role Changes**) |
| `IsFree(antId)` | Whether an ant is still unclaimed |
| `AttackerSlots` | How many more defenders/interceptors may be assigned while keeping `MinGatherers` (2) ants gathering. Respect it in attacker rules |
| `Recent` | `RecentEvents`: `Kills(role)`, `Deaths(role)` and `GathererLossesAtHome` over the last `WindowSeconds` (20 s) |
| `SkipRemainingRules = true` | Stop the rules after this one from running this decision (see `CloseOutRule`) |

**`WorldSnapshot world`**

| Member | Use |
|---|---|
| `MyAnts`, `EnemyAnts` | `AntInfo` for every ant: `Id`, `Position`, `Cell`, `Carrying`, `Dead`, `Role`. Own ants also have `Order`, `PendingOrder` (a switch it's walking home for) and `NextOrder` (whichever applies next) |
| `MyAnts[i].Kills` | Enemy ants each of our ants has killed so far (own ants only) |
| `Food` | `FoodInfo` for every pile: `Id`, `Cell`, `Amount`, `StartAmount`, `RegrowSecondsLeft` (0 unless the pile is empty and waiting to refill) |
| `MyNest`, `EnemyNest` | Nest cells |
| `MyDeliveries`, `EnemyDeliveries`, `ScoreDifference`, `WinTarget` | Score |
| `MyKills`, `EnemyKills`, `MyCarriers`, `EnemyCarriers` | Combat and carrying counts |
| `ElapsedSeconds`, `RemainingSeconds` | Clock |
| `Settings` | All match rules (speeds, `RespawnSeconds`, ...) |
| `DefendLeash` | How far defenders may go from their nest this match (rolled from the seed, 12-16 cells) |
| `PathCost(from, to)` | Walking cost between two cells, the same measure ants use. Cached for the rest of the decision |
| `CostHome(ant)` | Cost for one of our ants to reach our nest |
| `CostToStart(ant, role, target)` | Cost for an ant to begin a job, **including the walk home if it has to switch roles**. Use this to pick who gets a job |
| `DistanceFromMyNest(point)` | Straight-line distance to our nest, e.g. to check a post is inside the defender leash |

### Tips

- **Pick ants by `CostToStart`**, not plain distance. A gatherer next to a target still has to walk home before it can defend or intercept.
- **Pick roles and places, not moves.** A post is where a defender rests; the defender behavior decides whom to chase inside its area. Don't steer ants by changing their target every decision.
- **Keep ants in role.** Every role change costs a trip home, so rules should prefer ants that already have the role (`a.NextOrder?.Role == ...`). Re-assign an ant's current order rather than a slightly different one if nothing important changed. `GuardRule` and `RaidRule` keep an ant's current order for this reason. The controller also holds each new order for at least 2 s (`HoldSeconds`, hysteresis, Millington §6.4.2).
- **Break ties by id** (`.ThenBy(a => a.Id)`), never by list position or a dictionary order. Each side's ants are numbered the same way, so this keeps mirrored matches mirrored.
- **Handle dead ants on purpose.** A dead ant respawns at the nest after 3 s with its role. Including it in a rule keeps it in role; leaving it out means the controller sets it to gather.
- **Write a reason** a viewer can understand ("2 gatherer(s) lost at home in 20s"). It's what the log and the video show.
- **A rule may keep state** between decisions (the strategist creates fresh rules each match), but anything it needs is usually already in the snapshot.

## Adding a new role

A role is a new job in the game itself, so this touches the simulation. Using a hypothetical **Scout** role as the example:

1. **`scripts/Sim/Role.cs`**
   - Add `Scout` to the `Role` enum.
   - Add a factory, e.g. `public static RoleOrder Scout(Vector2I? spot = null) => new(Role.Scout, Post: spot);`. Reuse `FoodId`, `Post` or `TargetAntId` for the target if one fits; add a field to `RoleOrder` only if none does.
   - Add a case to `RoleOrder.ToString()`.
2. **Behavior:** create `scripts/Sim/ScoutBehavior.cs` with `public static void Update(Ant ant, Simulation sim)`, following `DefendBehavior` / `InterceptBehavior`. It runs every tick for every living ant with that role. Use `ant.GoTo(cell, sim.Pathfinder)` to move. Sense other ants only through `PreviousPosition`, `PreviousCell` and `WasCarrying` (start-of-tick values), so results don't depend on update order.
3. **`Simulation.RunBehavior`:** add `case Role.Scout: ScoutBehavior.Update(ant, this); break;`.
4. **Combat (only if it fights):** add a case to `Simulation.CanAttack(biter, target)` saying what it may attack. In the behavior, attack with `Combat.ChaseAndBite(ant, target, sim)`. Bites are declared there and all resolve together at the end of the tick. Attackers must never pick up food.
5. **Speed (optional):** add a `ScoutSpeedFactor` to `MatchSettings` and a case in `MatchSettings.SpeedFactor(role)`.
6. **Drawing:** add a ring color in `AntView.RingColor(role)` so viewers can tell the role apart (and the pending-switch arc uses it too).
7. **Let AIs use it:** strategist rules can now hand out `RoleOrder.Scout(...)`. If you want a fixed comparison, extend `FixedMixController`.

Enemy AIs see the new role automatically through `AntInfo.Role`.

## Adding a new team controller

For a completely different AI (not built from strategist rules):

1. Create a class in `scripts/Sim/Ai/` that implements `ITeamController`: a `Name` and `IEnumerable<AntCommand> Decide(WorldSnapshot world)`. Re-sending an ant's current order does nothing, **except that it cancels a role switch the ant is walking home for**. Compare against `ant.NextOrder` (as `StrategistController` does) if you don't want that.
2. Add a value to `ControllerKind` and a case in `Controllers.Create` (`scripts/Sim/Ai/Controllers.cs`). It then shows up in the Inspector on `GameRunner` (**AI** group).
3. The controller is created fresh for each match, so instance fields are safe for per-match memory.

## Changing what an AI can see

Add a property to `WorldSnapshot` and fill it in `WorldSnapshot.Capture`. Keep to two rules:

- Read start-of-tick values (`PreviousPosition`, `PreviousCell`, `WasCarrying`), like `Describe` does.
- Don't expose anything a side shouldn't know, such as the enemy's order targets or queued switches.

## Testing a change

Matches are deterministic: the same seed and settings always play out the same way. A single run per matchup is enough, but play **both sides**, since equal-cost A* paths can make red and blue differ slightly.

**Quick look in the editor:** set Red/Blue controllers on `GameRunner`, turn on **Log Role Changes**, and press F5. The output prints every role switch with the rule's reason.

**Headless check scene:** a small throwaway scene with a script that plays matches without drawing:

```csharp
var map = MapLoader.LoadFile("res://maps/arena.txt");
var sim = new Simulation(map, new MatchSettings());
sim.SetController(Team.Red, new StrategistController());
sim.SetController(Team.Blue, new FixedMixController(0, 2));
while (!sim.IsOver)
    sim.Step();
GD.Print($"{sim.Winner?.ToString() ?? "Draw"} {sim.Deliveries[Team.Red]}-{sim.Deliveries[Team.Blue]}");
```

Run it with `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . res://path/to/Check.tscn`. To test one rule's value (an ablation), build the rule list yourself: `new StrategistController(rules)`.

**Opponents worth testing against:** `BaselineController`, `FixedMixController(0, 1)`, `FixedMixController(0, 2)`, `FixedMixController(1, 1)`, `FixedMixController(1, 0)`.

**Many matches:** threads don't help, because Godot runs the A* callbacks one at a time. Start several headless processes instead, each playing its share of the matches. Pass the share after `--` (for example `-- 3 14` = share 3 of 14) and read it with `OS.GetCmdlineUserArgs()`, then combine the printed results. 14 processes ran 360 matches in under 2 minutes.

**Sanity checks worth keeping in a test scene:**

- Food is only created by regrowth: an empty pile refills to its starting amount, never more.
- No attacker (Defend or Intercept) ever holds food.
- Every role change happens at the ant's nest or while it is dead (`Simulation.RoleChanged` event, `Simulation.IsAtNest`).
- Defenders stay within `DefendLeash` of their nest.
- Identical controllers on both sides should mostly mirror (see the known A* issue in OVERVIEW.md).
