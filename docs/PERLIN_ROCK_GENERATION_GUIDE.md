# Perlin Noise Rock Generation

The `Open`, `Highlands` and `Contested` presets place rock with Perlin noise (Millington §8.3.3). `Caves` uses cellular automata instead (`CellularAutomata.cs`).

## How it works

1. **Noise.** `PerlinNoise` adds up three octaves with weights `{1, 0.5, 0.25}`. The first octave sets the base scale. Each next one has twice the frequency and half the weight, adding finer detail.
2. **Gradients.** Each octave shuffles a 256-entry table that gives every lattice point its own random gradient, so the pattern doesn't repeat across the map. A fade curve blends between lattice points, so the lattice lines don't show.
3. **`RockFeatureSize`.** Map coordinates are divided by it, so the base octave changes over about `RockFeatureSize` cells. Bigger values give bigger, smoother rock clusters.
4. **`RockDensity`.** The inside cells are ranked by their noise value, and the top `RockDensity` share becomes rock. This hits the target density exactly, whatever range the noise values cover.
5. **Border.** The outer edge is always rock.

| Example | RockDensity | RockFeatureSize | Result |
|---|---|---|---|
| Open | 0.04 | 10 | A few scattered rock clusters |
| Highlands, Contested | 0.08 | 10 | More, larger clusters |

The same seed and parameters always give the same rock.

## Files

- `tools/MapGenerator.Core/PerlinNoise.cs`: the noise
- `tools/MapGenerator.Core/MapGenerator.cs`: `GenerateRocks()` turns the noise into rock
