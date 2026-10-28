# Extending the AI

How the AI works, and how to add rules, roles, or whole new AIs. For the code layout and game rules, see [OVERVIEW.md](OVERVIEW.md).

## How the system works

### The big idea

The AI is a manager, not a driver. It only decides **which job each ant has**. The ants do the job themselves.

```
1. The game takes a snapshot of the board        WorldSnapshot
2. Each side's AI looks at it and gives orders   ITeamController.Decide   (every 0.5 s)
3. The game applies the orders                   Simulation.AssignRole
4. Each ant carries out its job                  Gather / Defend / Intercept behaviors   (every tick)
5. Ants find their way                           Pathfinder (A*)
```

An order is a **role** plus an optional **target**:

| Order | Means |
|---|---|
| `RoleOrder.Gather(pileId)` | Gather, from this pile (or the nearest) |
| `RoleOrder.Defend(post)` | Defend, resting at this spot (or the nest) |
| `RoleOrder.Intercept(antId)` | Hunt this enemy (or the nearest carrier) |

### What the AI can see

The `WorldSnapshot` is a read-only copy of the board at the start of the tick: all ants (enemy roles are visible, their targets are not), food piles, scores, the clock, and helpers like `PathCost` and `CostToStart`. Both sides get their snapshot before either side's orders are applied.

### Rules every AI lives with

- **Roles only change at the nest.** An ant given a new role walks home first. A new target in the same role applies at once. Dead ants switch right away.
- **Orders for enemy ants are ignored.**
- **Matches must replay.** Only use randomness seeded from `world.Settings.Seed`.

### How the Strategist decides

Every half second:

1. **Update memory.** `RecentEvents` compares the new snapshot with the last one and notes our kills and deaths over the last 20 s.
2. **Run the rules in order.** Each rule looks at the board and its memory, and claims some free ants by giving them orders. Earlier rules get first pick.
   - **Guard:** our gatherers keep dying at home → claim a defender.
   - **Raid:** keep interceptors; more if they get kills, fewer if they keep dying.
   - **Spread gather:** send everyone left to a good pile.
3. **Fill in the rest.** Ants no rule claimed gather.
4. **Send only real changes.** Orders the ant already has are skipped, and an ant that just changed keeps its order for at least 2 s.

At least 2 ants always gather. In textbook terms (Millington, *AI for Games*): a multi-tier AI (§6.4.1) whose top tier is a rule-based system (§5.8) with first-applicable arbitration (§5.8.5) and hysteresis (§6.4.2).

## Adding a strategist rule

The most common change. It doesn't touch the game rules.

### 1. Write the rule

Create a class in `scripts/Sim/Ai/Strategy/` that implements `IStrategyRule`. This example guards the pile most of our gatherers use, while gatherers are dying at home:

```csharp
using System.Linq;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>While our gatherers are dying at home, guard the pile most of them use.</summary>
public class GuardPileRule : IStrategyRule
{
    public string Name => "Guard pile";

    public void Apply(WorldSnapshot world, StrategyPlan plan)
    {
        if (plan.Recent.GathererLossesAtHome == 0 || plan.AttackerSlots == 0)
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
            plan.Assign(ant.Id, RoleOrder.Defend(pile.Cell), $"guard pile {pile.Id}, gatherers dying at home");
    }
}
```

### 2. Add it to the list

Insert it in `StrategistController.CreateDefaultRules()`. Order matters: earlier rules get first pick. The default is Guard, Raid, Spread gather.

### 3. Check it helps

Play with and without your rule against a few opponents, over many seeds and from both sides (see [Testing](#testing)).

### What a rule can use

**From the plan (`plan`):**

| Member | Use |
|---|---|
| `FreeAnts` | Our ants no earlier rule claimed (includes dead ones) |
| `Assign(id, order, reason)` | Claim an ant. The reason shows in the log |
| `AttackerSlots` | How many more attackers are allowed (2 ants always gather) |
| `Recent` | Our recent `Kills(role)`, `Deaths(role)`, `GathererLossesAtHome` |

**From the snapshot (`world`):**

| Member | Use |
|---|---|
| `MyAnts`, `EnemyAnts` | Every ant: position, carrying, dead, role. Ours also have orders and kill counts |
| `Food` | Every pile: amount, and seconds until it refills if empty |
| `MyNest`, `EnemyNest`, `Map` | Where things are |
| `MyDeliveries`, `EnemyDeliveries`, `WinTarget`, `ElapsedSeconds` | Score and clock |
| `Settings`, `DefendLeash` | Match rules, and this match's defender area |
| `PathCost(a, b)`, `CostHome(ant)` | Walking costs |
| `CostToStart(ant, role, target)` | Cost to start a job, **including the walk home** to switch roles |

### Tips

- **Pick ants with `CostToStart`.** A gatherer next to a target still has to walk home to switch.
- **Pick roles and places, not moves.** Let the behaviors decide whom to chase.
- **Keep ants in role.** Every switch costs a walk home. Prefer ants that already have the role (`a.NextOrder?.Role`).
- **Break ties by id** (`.ThenBy(a => a.Id)`), so neither side gets an edge.
- **Write a clear reason.** It's what the log and the video show.

## Adding a new role

A role is a new job in the game, so this touches the simulation. Example: a **Scout**.

1. **`Role.cs`:** add `Scout` to the enum, a `RoleOrder.Scout(...)` factory, and a case in `ToString()`.
2. **Behavior:** create `ScoutBehavior.cs` with `Update(Ant ant, Simulation sim)`, like `DefendBehavior`. Move with `ant.GoTo(...)`. Look at other ants only through their start-of-tick values (`PreviousPosition`, `PreviousCell`, `WasCarrying`).
3. **`Simulation.RunBehavior`:** add a case for `Role.Scout`.
4. **If it fights:** say what it may attack in `Simulation.CanAttack`, and bite with `Combat.ChaseAndBite`. Attackers never pick up food.
5. **Speed (optional):** a factor in `MatchSettings.SpeedFactor`.
6. **Drawing:** a ring color in `AntView.RingColor`.
7. **Use it** from a strategist rule, or add it to `FixedMixController`.

## Adding a new AI

For an AI not built from strategist rules:

1. Implement `ITeamController` in `scripts/Sim/Ai/`: a `Name` and `Decide(world)`.
2. Add it to `ControllerKind` and `Controllers.Create`. It then shows up in the Inspector.

Re-sending an ant's current order cancels a switch it's walking home for. Compare against `ant.NextOrder` to avoid that. A new controller is created for each match, so fields are safe to use as memory.

To let AIs see something new, add it to `WorldSnapshot.Capture`. Use start-of-tick values, and don't reveal enemy targets.

## Testing

The same seed replays the same match, but seeds differ a lot. **Use many seeds (50+) from both sides**, and keep the evaluation seeds (1001-1070) separate from the ones you tune on.

- **In the editor:** pick controllers on `GameRunner`, turn on **Log Role Changes**, press F5.
- **Headless:** a small scene that plays matches without drawing:

```csharp
var map = MapLoader.LoadFile("res://maps/arena.txt");
var sim = new Simulation(map, new MatchSettings { Seed = 1 });
sim.SetController(Team.Red, new StrategistController());
sim.SetController(Team.Blue, new FixedMixController(1, 1));
while (!sim.IsOver)
    sim.Step();
GD.Print($"{sim.Winner?.ToString() ?? "Draw"} {sim.Deliveries[Team.Red]}-{sim.Deliveries[Team.Blue]}");
```

Run it with `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . res://path/to/Check.tscn`. To test without a rule, pass your own list: `new StrategistController(rules)`.

**Good opponents:** `BaselineController`, and `FixedMixController(defenders, interceptors)` with (0, 1), (0, 2), (1, 1), (1, 0).

**Worth checking:** attackers never hold food, role changes only happen at the nest (or while dead), defenders stay in their area, and identical AIs win about equally often.
