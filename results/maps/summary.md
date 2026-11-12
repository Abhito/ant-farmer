# Generated map batch

800 preset maps and 570 parameter-sweep maps.

## Valid maps

| Preset | Raw | After repair | Why raw maps failed |
|---|---|---|---|
| Open | 100/100 | 100/100 | - |
| Caves | 56/100 | 100/100 | connected (44), fairness (1) |
| Highlands | 100/100 | 100/100 | - |
| Contested | 100/100 | 100/100 | - |

Parameter sweeps (repaired): 569/570 valid.

## Medians of repaired maps

| Measure | Open | Caves | Highlands | Contested | Hand-built arena |
|---|---|---|---|---|---|
| rock_density | 0.04 | 0.30 | 0.08 | 0.08 | 0.11 |
| hill_share | 0.48 | 0.49 | 0.77 | 0.47 | 0.17 |
| nest_distance | 67.42 | 55.51 | 55.40 | 46.22 | 69.00 |
| nest_path_cost | 71.52 | 85.41 | 64.28 | 49.76 | 75.97 |
| hill_penalty | 0.03 | 0.06 | 0.11 | 0.04 | 0.04 |
| food_access | 13.97 | 17.49 | 16.12 | 19.19 | 24.58 |
| fairness_gap | 0.01 | 0.01 | 0.02 | 0.01 | 0.00 |
| exposure_gap | 0.09 | 0.14 | 0.07 | 0.08 | 0.00 |
| entrances_red | 1.00 | 3.00 | 1.00 | 1.00 | 1.00 |
| dead_ends | 4.00 | 0.00 | 9.00 | 9.00 | 0.00 |
| narrow_cells | 0.01 | 0.00 | 0.01 | 0.01 | 0.00 |
| outside_defense | 0.77 | 0.77 | 0.76 | 0.76 | 0.76 |

The hand-built arena is `maps/arena_large.txt` (81x50, mirrored). Its rock density counts a 2-cell-thick border.
