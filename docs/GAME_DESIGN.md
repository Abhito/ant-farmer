# Game Design: Nest Assault

A planned new goal for the game. Instead of racing to collect food, each colony uses food to **build an army** and wins by **destroying the enemy nest**.

Numbers marked *(start)* are first guesses. We'll tune them once matches are running.

## In short

- Destroy the enemy nest to win.
- Delivered food is money, and it buys new ants.
- Dead ants stay dead.
- There are several ant types with different costs and jobs.
- The AI decides **what to build** and **what each ant does**.

The map, pathfinding and ant movement stay the same.

## Rules

| | |
|---|---|
| **Win** | Enemy nest HP reaches 0 |
| **Time limit** | 300 s *(start)*. If time runs out, most nest HP left wins, then most army value alive, then most food delivered |
| **Nest** | 100 HP *(start)*. Builds one ant at a time, 3 s each *(start)* |
| **Food** | Gathered as now. Each delivery adds 1 to the food bank |
| **Start** | 10 food, 3 workers and 1 soldier per side *(start)* |
| **Cap** | 20 living ants per side *(start)* |
| **Death** | Permanent. A carrier's food goes back to its pile |
| **Combat** | Ants have HP and deal damage. 1 s between attacks, range 1.2 cells *(start)* |

## Ant types

An ant's type is set when it spawns and never changes.

| Type | Cost | HP | Job | Strong against | Weak against |
|---|---|---|---|---|---|
| **Worker** | 3 | 2 | Gathers food, can't fight | nothing | everything |
| **Soldier** | 5 | 4 | Fights ants (1 damage) | breakers, runners | groups |
| **Breaker** | 8 | 5 | Damages the nest (3 damage), slow | the nest | soldiers |
| **Runner** *(optional)* | 3 | 2 | Fast, hunts workers | workers | soldiers |

All costs and stats are *(start)* values. The counters matter: without them, the best plan would be to build only one type, and the AI wouldn't have real choices.

## What the AI decides

Every 0.5 s, the AI makes two kinds of choices.

**1. What to build.** Examples:
- Short on food → build a worker.
- Losing ants near home → build soldiers.
- The enemy has few soldiers → save up for breakers.

**2. What each ant does.** The orders each type accepts:

| Order | Worker | Soldier | Breaker | Runner |
|---|---|---|---|---|
| Gather from a pile | ✓ | | | |
| Guard a spot | | ✓ | | |
| Hunt an enemy | | ✓ | | ✓ |
| Escort a friend | | ✓ | | |
| Wait at a rally point | ✓ | ✓ | ✓ | ✓ |
| Attack the nest | | ✓ | ✓ | |
| Retreat home | ✓ | ✓ | ✓ | ✓ |

Attack timing is a big new decision. With no respawns, breakers that arrive one at a time just die one at a time. Gathering a group first and attacking together works much better.

**What the AI can see:** every ant on the map (type, HP, position), all food piles, both nests' HP, and its own food bank. It **can't** see the enemy's food bank or build queue, so it has to guess what's coming.

**In code,** `Decide()` returns two lists, ant orders and build requests:

```csharp
public class TeamOrders
{
    public List<AntCommand> Commands = new();   // orders for ants (as now)
    public List<SpawnRequest> Spawns = new();   // ant types to build
}
```

## Opponents

These scripted opponents replace Baseline and Fixed Mix:

| Opponent | Plan |
|---|---|
| **Baseline** | Repeats worker, worker, soldier, breaker. Attacks once it has 3 breakers |
| **Rush** | Builds an army right away and attacks early |
| **Economy** | Builds 8 workers first, then one big attack |
| **Turtle** | Mostly soldiers at home. Attacks late |

Each one punishes a different mistake, so a good Strategist has to adapt.

## Risks to watch

We'll measure these in the first matches and decide on fixes after seeing the data.

| Risk | What it looks like | Possible fixes |
|---|---|---|
| **Snowball** | Whoever wins the first fight wins the match | Nest makes a free worker now and then, or fights back |
| **Rush wins** | Early all-in attacks beat everything | More nest HP, nest fights back |
| **Stalemate** | Matches keep hitting the time limit | Stronger or cheaper breakers |
| **One type dominates** | Winners build only one type | Change costs and stats |

## What this means for maps

The map format doesn't change. Nests are now targets, though, so the distance between nests, narrow entrances and nest exposure all matter more. These are noted in [MAP_GENERATION.md](MAP_GENERATION.md).

## Build order

Each step leaves a working game.

1. **Core rules:** food bank, nest HP, ant HP and damage, no respawns, new win check.
2. **Ant types and orders.**
3. **Building ants:** build queue, new AI interface.
4. **Scripted opponents.**
5. **Strategist:** build rules, updated guard and raid rules, attack timing.
6. **Visuals:** shapes per type, HP bars, nest HP and food in the HUD.
7. **Batch runs:** check the risks above and tune numbers.

## Open decisions

1. Does the nest heal, or fight back?
2. How much food and how many ants does each side start with?
3. Keep the Runner, or start with three types?
4. Are the *(start)* stats and costs OK as a first pass?
5. Can orders change anywhere, or only at the nest like now?
6. Is the time-limit tiebreak order right?
7. Should the enemy's food bank and build queue stay hidden?
8. When do we build this, relative to the map generator?
