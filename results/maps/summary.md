# Generated map batch

800 preset maps and 570 parameter-sweep maps.

## Valid maps

| Preset | Raw | After repair | Why raw maps failed |
|---|---|---|---|
| Open | 100/100 | 100/100 | - |
| Caves | 56/100 | 100/100 | connected (44) |
| Highlands | 100/100 | 100/100 | - |
| Contested | 100/100 | 100/100 | - |

Parameter sweeps (repaired): 568/570 valid.

## Medians of repaired maps

| Measure | Open | Caves | Highlands | Contested | Hand-built arena |
|---|---|---|---|---|---|
| rock_density | 0.04 | 0.30 | 0.08 | 0.08 | 0.11 |
| hill_share | 0.48 | 0.49 | 0.77 | 0.47 | 0.17 |
| nest_distance | 68.49 | 54.70 | 58.00 | 37.41 | 69.00 |
| nest_path_cost | 72.71 | 80.85 | 66.54 | 40.51 | 75.97 |
| hill_penalty | 0.03 | 0.05 | 0.11 | 0.05 | 0.04 |
| food_access | 14.44 | 17.90 | 16.68 | 15.31 | 24.58 |
| fairness_gap | 0.00 | 0.01 | 0.02 | 0.01 | 0.00 |
| exposure_gap | 0.08 | 0.11 | 0.07 | 0.10 | 0.00 |
| entrances_red | 1.00 | 3.00 | 2.00 | 2.00 | 1.00 |
| dead_ends | 4.00 | 0.00 | 9.00 | 9.00 | 0.00 |
| narrow_cells | 0.01 | 0.00 | 0.01 | 0.01 | 0.00 |
| outside_defense | 0.77 | 0.78 | 0.78 | 0.78 | 0.76 |

The hand-built arena is `maps/arena_large.txt` (81x50, mirrored). Its rock density counts a 2-cell-thick border.
