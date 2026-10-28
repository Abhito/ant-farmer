# Evaluation results

1000 matches from `results.csv`.

## Main run (seeds 1001-1070, both sides)

| Opponent | Matches | W-D-L | Win rate (95% CI) | Avg margin | Avg length | Hit time limit | Role switches |
|---|---|---|---|---|---|---|---|
| Baseline | 140 | 140-0-0 | 100% (97%-100%) | +10.2 | 54 s | 0 | 1.0 |
| Fixed 1I | 140 | 129-0-11 | 92% (86%-96%) | +10.2 | 89 s | 0 | 7.0 |
| Fixed 2I | 140 | 140-0-0 | 100% (97%-100%) | +13.6 | 116 s | 0 | 9.2 |
| Fixed 1D+1I | 140 | 46-1-93 | 33% (26%-41%) | -1.7 | 60 s | 0 | 5.8 |
| Fixed 1D | 140 | 138-0-2 | 99% (95%-100%) | +2.7 | 45 s | 0 | 1.8 |

## Side check (main run)

| Strategist side | Matches | Win rate |
|---|---|---|
| red | 350 | 84% |
| blue | 350 | 85% |

## Wins by pile size (main run)

| Opponent | Piles of 3 | Piles of 4 | Piles of 5 | Piles of 6 |
|---|---|---|---|---|
| Baseline | 36/36 | 34/34 | 36/36 | 34/34 |
| Fixed 1I | 31/36 | 29/34 | 36/36 | 33/34 |
| Fixed 2I | 36/36 | 34/34 | 36/36 | 34/34 |
| Fixed 1D+1I | 2/36 | 16/34 | 20/36 | 8/34 |
| Fixed 1D | 34/36 | 34/34 | 36/36 | 34/34 |

## Ablations (seeds 1001-1015, both sides)

| Opponent | Strategist | Matches | W-D-L | Win rate (95% CI) | Avg margin | Role switches |
|---|---|---|---|---|---|---|
| Baseline | Full | 30 | 30-0-0 | 100% (89%-100%) | +10.4 | 1.0 |
| Baseline | Adaptation off | 30 | 30-0-0 | 100% (89%-100%) | +7.5 | 0.0 |
| Baseline | No Raid | 30 | 22-0-8 | 73% (56%-86%) | +1.3 | 0.0 |
| Baseline | No Guard | 30 | 30-0-0 | 100% (89%-100%) | +10.4 | 1.0 |
| Fixed 2I | Full | 30 | 30-0-0 | 100% (89%-100%) | +14.2 | 9.2 |
| Fixed 2I | Adaptation off | 30 | 0-0-30 | 0% (0%-11%) | -19.7 | 0.0 |
| Fixed 2I | No Raid | 30 | 1-0-29 | 3% (1%-17%) | -4.6 | 4.7 |
| Fixed 2I | No Guard | 30 | 6-2-22 | 20% (10%-37%) | -2.1 | 1.0 |
| Fixed 1D+1I | Full | 30 | 12-1-17 | 40% (25%-58%) | -1.3 | 5.5 |
| Fixed 1D+1I | Adaptation off | 30 | 0-0-30 | 0% (0%-11%) | -10.2 | 0.0 |
| Fixed 1D+1I | No Raid | 30 | 26-0-4 | 87% (70%-95%) | +3.4 | 2.6 |
| Fixed 1D+1I | No Guard | 30 | 0-0-30 | 0% (0%-11%) | -7.6 | 2.4 |

## Sanity: Strategist vs. Strategist

| Matches | Red wins | Blue wins | Draws |
|---|---|---|---|
| 30 | 18 | 12 | 0 |
