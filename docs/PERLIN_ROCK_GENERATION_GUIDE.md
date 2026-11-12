# Perlin Noise Rock Generation Implementation

## Overview
The rock generation system now uses Perlin noise to create natural-looking rock clusters on maps, respecting both `RockDensity` and `RockFeatureSize` parameters.

## How It Works

### 1. Perlin Noise Setup
The implementation creates a multi-octave Perlin noise generator with weights `{ 1f, 0.5f, 0.25f }`. Each octave has double the frequency of the previous one:
- **Octave 0** (size 1): Large-scale features
- **Octave 1** (size 2): Medium-scale features  
- **Octave 2** (size 4): Fine-scale details

The octaves are blended together using their weights, creating natural clustering patterns at multiple scales.

### 2. RockFeatureSize Parameter
Controls the scale of rock clusters by dividing map coordinates:

```csharp
float nx = (float)x / p.RockFeatureSize;
float ny = (float)y / p.RockFeatureSize;
```

- **Small RockFeatureSize** (e.g., 5): Higher frequency noise → smaller, more intricate clusters
- **Large RockFeatureSize** (e.g., 15): Lower frequency noise → larger, more spread-out clusters

### 3. RockDensity Parameter
Controls what percentage of the map becomes rock by setting a threshold:

```csharp
float threshold = p.RockDensity * 2 - 1;
```

The Perlin noise output ranges from approximately -1 to 1. The threshold is mapped to this range:
- **RockDensity = 0.0**: threshold = -1 (very few rocks, only noise < -1)
- **RockDensity = 0.5**: threshold = 0 (balanced, roughly 50% rocks)
- **RockDensity = 1.0**: threshold = 1 (almost everything is rock)

Cells where `perlin.Get(nx, ny) > threshold` become rocks.

## Usage Examples

### Small, Tight Clusters (Caves-like)
```csharp
RockDensity = 0.25f,      // 25% rocks
RockFeatureSize = 5       // Small features → intricate clustering
```

### Large, Sparse Clusters (Open terrain)
```csharp
RockDensity = 0.04f,      // 4% rocks
RockFeatureSize = 15      // Large features → spread out clusters
```

### Medium Clusters (Contested)
```csharp
RockDensity = 0.10f,      // 10% rocks
RockFeatureSize = 10      // Medium features
```

## Map Generation Flow

1. **Initialization**: Create `GenParams` with desired `RockDensity` and `RockFeatureSize`
2. **Perlin Creation**: Initialize multi-octave Perlin noise with fixed seed
3. **Grid Iteration**: For each map cell (except border):
   - Scale coordinates by `RockFeatureSize`
   - Sample Perlin noise at scaled position
   - Compare against density threshold
   - Place rock if above threshold
4. **Border Enforcement**: Ensure all border cells are rock (game requirement)
5. **Output**: Generate map text with `#` for rocks and `.` for ground

## Key Features

✓ **Deterministic**: Same seed + params always produce identical maps
✓ **Clustered**: Rocks naturally group together rather than being random
✓ **Scalable**: RockFeatureSize controls cluster granularity
✓ **Density Control**: RockDensity precisely targets rock percentage
✓ **Border Safe**: All edges guaranteed rock for map boundaries

## Implementation Files

- **`scripts/Gen/PerlinNoise.cs`**: Core Perlin noise implementation
- **`scripts/Gen/MapGenerator.cs`**: 
  - `GenerateRocksWithPerlin()`: Main rock generation logic
  - `MapGenerator.Generate()`: Integration point for map generation

## Notes

- The implementation ensures the map border is always rock (required by game rules)
- Perlin noise output is naturally smooth, avoiding random-looking isolated rocks
- The octave weights can be tuned to emphasize different cluster sizes if needed
- The threshold calculation ensures density maps linearly across the parameter range
