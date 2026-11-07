# Map Generation Guide

A guide for building the map generator. The **interface** and **map rules** are fixed, because the rest of the game depends on them. Everything else is a suggestion.

For the game itself, see [OVERVIEW.md](OVERVIEW.md). For setup, see the [README](../README.md).

## In short

- Write `MapGenerator.Generate(params, seed)`. It returns a map as text, in the same format as `maps/arena.txt`.
- The same params and seed must always give the same map.
- Maps are about **91×57**, around 5× the size of the current arena.
- Maps are **not mirrored**. Nests can go anywhere, but both sides need a fair start.
- Make 3-4 presets that produce clearly different kinds of maps.

## Who does what

- **Generator** (`scripts/Gen/`): the algorithm, parameters, presets and repair step.
- **Checker and game side** (`scripts/Tools/`, `GameRunner.cs`, `tools/*.py`): validity checks, measurements, batch runs, plots, and playing matches on generated maps.

Stick to your own files and ask before changing the other side's.

## The interface

```csharp
namespace AntFarmer.Gen;

public static class MapGenerator
{
    // repair = false returns the raw map, so we can count how often it's valid without help.
    public static string Generate(GenParams p, int seed, bool repair = true);
}

public class GenParams
{
    public string Name = "Custom";

    public int Width = 91;
    public int Height = 57;

    public float RockDensity = 0.08f;   // share of cells that are rock
    public int RockFeatureSize = 10;    // rough size of rock clusters

    public int HillCount = 30;
    public int MaxHillHeight = 3;       // 0-9
    public int HillRadius = 6;

    public int FoodPiles = 31;
    public int MinFoodSpacing = 8;      // cells between piles
    public int MinFoodNestDistance = 6; // cells between a pile and a nest
    public float FoodBias = 0f;         // -1 near nests, 0 even, +1 far from both

    public int MinNestDistance = 45;    // cells between the two nests
    public int NestEdgeMargin = 3;      // cells between a nest and the border
    public float MaxNestImbalance = 0.2f; // how much better one nest's food access may be

    public static GenParams Open() { ... }
    public static GenParams Caves() { ... }
    public static GenParams Highlands() { ... }
    public static GenParams Contested() { ... }
}
```

Start the output with `;` comment lines that say how the map was made. The loader skips them.

```
; generator: Caves  seed: 1042  repaired: yes
; Width=91 Height=57 RockDensity=0.25 ...
```

The generator only returns text. Saving files, checking maps and retrying bad seeds happen on the checker side. Feel free to add parameters, but keep the names above.

## Map rules

The validity checker tests all of these.

| Rule | Why |
|---|---|
| Only `# . 1-9 F R B`, all rows the same length | The loader rejects anything else |
| Rock border all the way around | Keeps ants on the map |
| One red and one blue nest, each a 2×2 block, at least `MinNestDistance` apart | Ants spawn on the nest cells |
| No rock within 2 cells of a nest | Ants crowd around the nest |
| Both nests can reach each other and every food pile | No dead piles, no sealed-in side |
| Each nest's food access is within 20% of the other's | Keeps it fair, since maps aren't mirrored |
| At least 12 piles, none closer than `MinFoodNestDistance` to a nest | Enough food, and no free food next to home |
| At least 90% of open ground connected | No big wasted pockets |
| Width 71-121, height 45-75 | Big enough to matter, small enough to play |

"Food access" means the average path cost from a nest to its 5 nearest piles.

## Good to know

- `F`, `R` and `B` cells are always height 0.
- Climbing one level up costs double, and going down is free. Steep cliffs act almost like walls, so smooth slopes work better.
- Diagonal moves are blocked if either side cell is rock.
- For reference, the current arena is 41×25, with 5% rock, 17% hills (max height 3) and 13 piles.
- **The game's goal is changing** ([GAME_DESIGN.md](GAME_DESIGN.md)): sides will win by destroying the enemy nest. The map format stays the same, but nests become targets, so these matter more:
  - the path length between the nests (short paths favor rushing)
  - narrow entrances near a nest (easy to defend)
  - how exposed each nest is: one nest in open ground and the other behind a single corridor isn't fair
- The match rules aren't tuned for big maps yet, so many matches will hit the time limit. That's fine. Don't shrink the maps to fix it.

## How to build it

A suggested pipeline. Section numbers are from Millington, *AI for Games* 3rd ed., Chapter 8. Any source is fine as long as you cite it.

1. **Randomness.** Use one `new System.Random(seed)` for everything, never Godot's random functions (§8.1.1, p. 671).
2. **Rock.** Pick one method:
   - **Noise:** make a noise field and turn the highest values into rock (§8.3.2-8.3.3, p. 689). Simple, and covers open fields to caves.
   - **Recursive subdivision:** split the map into rooms with gaps in the walls (§8.4.3, p. 721).
   - **Cellular automata:** fill cells randomly, then smooth a few times. A cell becomes rock if 5+ of its 3×3 block are rock. Gives natural caves. Good guides:
     - [RogueBasin: Cellular Automata Cave Generation](https://www.roguebasin.com/index.php/Cellular_Automata_Method_for_Generating_Random_Cave-Like_Levels), a practical walkthrough with code
     - [*Procedural Content Generation in Games*, Ch. 3 §3.4](https://www.pcgbook.com/chapter03.pdf), a free textbook chapter that's good to cite
3. **Hills.** Add round bumps to flat ground, then round and clamp heights to 0-9 (§8.3.1, p. 688).
4. **Food.** Spread piles out with Poisson disk sampling (§8.1.4, p. 676). Use `FoodBias` to lean them toward or away from nests.
5. **Nests.** Try several pairs of 2×2 spots and keep the fairest one. Presets can place nests differently: far apart, close rivals, one on high ground.
6. **Repair** (only if `repair` is true). Fill small cut-off pockets with rock, dig corridors to anything unreachable, clear rock around nests, then recheck fairness.
7. **Write** the map text with the header lines.

Overall this is "generate and test" (§8.4.4, p. 727). Keep a short note of what repair changed; it helps find funny failures for screenshots.

## Presets

Each preset should look clearly different. The same preset should still vary from seed to seed.

| Preset | Idea | Starting settings |
|---|---|---|
| `Open` | Wide fields, gentle hills, nests far apart | RockDensity 0.04, MaxHillHeight 2, MinNestDistance 60 |
| `Caves` | Winding tunnels and chokepoints | RockDensity 0.3, RockFeatureSize 5 |
| `Highlands` | Steep climbs around the food | HillCount 40, MaxHillHeight 6, HillRadius 8 |
| `Contested` | Close nests, food between them | MinNestDistance 30, FoodBias +0.8 |

## Testing

- **See a map:** save the output to a `.txt` in `maps/`, set `MapPath` on the GameRunner node in `scenes/Main.tscn`, and press F5. The AI plays on it right away.
- **Check repeatability:** generate the same preset and seed twice. The text must match exactly.
- Anything that uses `Pathfinder` has to run inside Godot. Your own flood fills don't.

## Done when

- [ ] No generation libraries and no Godot `FastNoiseLite`. Write your own noise.
- [ ] Changing parameters visibly changes the maps.
- [ ] 3-4 presets produce clearly different maps.
- [ ] Each preset varies across seeds, including where the nests go.
- [ ] Raw output (`repair: false`) works, so we can report how many maps were valid before and after repair.

The analysis will measure each map's rock density, nest-to-nest distance, distance to food, fairness, nest exposure, chokepoints and total climbing.

## Code style

- Plain C#, no drawing code. Match the style of `scripts/Sim/`.
- Few, short comments, only where the "why" isn't obvious.
- Work on your own branch and merge small pieces.
