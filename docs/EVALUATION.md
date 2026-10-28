# Evaluation Results

What we learned from 1,000 headless matches of the Strategist against scripted opponents. Raw data: `results/results.csv`. Full tables: `results/summary.md`. How to re-run: see the README.

## Setup

| | |
|---|---|
| **Map** | `maps/arena.txt` (mirrored left/right) |
| **Rules** | 5 ants per side, first to 30 deliveries or most after 180 s |
| **Seeds** | 1001-1070, never used while tuning. Each seed changes pile size, refill time, defender area and small timing details |
| **Sides** | Every seed played twice, with the Strategist as red and as blue |

| Run | What | Matches |
|---|---|---|
| Main | Strategist vs. 5 opponents, 70 seeds, both sides | 700 |
| Ablations | Strategist with a part removed, vs. 3 opponents, 15 seeds, both sides | 270 |
| Sanity | Strategist vs. Strategist, 15 seeds, both sides | 30 |

**Opponents:** Baseline (everyone gathers) and four Fixed Mix teams whose roles never change: 1 interceptor, 2 interceptors, 1 defender, and 1 defender + 1 interceptor.

## Main results

| Opponent | Strategist wins | Avg score margin | Avg match length |
|---|---|---|---|
| Baseline | **100%** (140/140) | +10.2 | 54 s |
| Fixed 1 interceptor | **92%** (129/140) | +10.2 | 89 s |
| Fixed 2 interceptors | **100%** (140/140) | +13.6 | 116 s |
| Fixed 1 defender | **99%** (138/140) | +2.7 | 45 s |
| Fixed 1 defender + 1 interceptor | **33%** (46/140) | -1.7 | 60 s |

- The Strategist beats the Baseline every time, and four of the five opponents more than 90% of the time.
- It loses about two thirds of its matches against 1 defender + 1 interceptor (see below).
- No match ran out of time.
- Side doesn't matter: 84% wins as red, 85% as blue.

## What decides the hard matchup: pile size

Against 1 defender + 1 interceptor, the seed's pile size changes the outcome a lot:

| Pile size | 3 | 4 | 5 | 6 |
|---|---|---|---|---|
| Strategist wins | 2/36 | 16/34 | 20/36 | 8/34 |

With medium piles it's close to even; with very small or large piles it almost always loses. Other matchups barely depend on pile size.

## Ablations: which parts matter

Win rates with one part of the Strategist removed (seeds 1001-1015, 30 matches each):

| Opponent | Full | Adaptation off | No Raid | No Guard |
|---|---|---|---|---|
| Baseline | 100% | 100% | 73% | 100% |
| Fixed 2 interceptors | 100% | 0% | 3% | 20% |
| Fixed 1 defender + 1 interceptor | 40% | 0% | 87% | 0% |

- **Adapting is essential.** With roles frozen at their starting values (no defenders, 1 interceptor), it loses every match against both interceptor teams. Changing roles during the match is what turns those into wins.
- **Raid** (interceptors) is what beats the Baseline and Fixed 2 interceptors.
- **Guard** (defenders) is what survives interceptor teams.
- **Surprise:** against 1 defender + 1 interceptor, removing Raid *helps* (87% vs. 40%). See the next section.

## Why interceptors struggle against defenders

Defenders guard a circle of 12-16 cells around their nest. On this map those circles cover most of the ground:

| Defender area | Map outside both areas | Piles outside both areas |
|---|---|---|
| 12 cells | 35% | 7 of 13 |
| 16 cells | 12% | 3 of 13 |

Carriers always walk home into their own defended area, so an interceptor chasing one usually runs into the enemy defender and dies. Every interceptor is also one less gatherer. Raid does react, by dropping interceptors that keep dying, but it judges over 20 s windows, which is too slow in a match that lasts about 60 s.

## Sanity check

Strategist vs. Strategist: red won 18, blue won 12, no draws. With 30 matches this is within normal chance of 50/50, so there's no sign that either side has an advantage.

## Takeaways

1. The Strategist clearly beats the Baseline and most fixed strategies.
2. Its strength comes from **changing roles during the match**; the same rules with frozen roles lose badly to interceptor teams.
3. Its weak spot is a team that defends from the start: on this map, interceptors have too little open ground to be worth it, and the Strategist learns that too slowly.
4. The map matters (pile size, open ground). That's worth testing across generated maps later.

## Ideas for improvement

- Interceptors that avoid enemy defended areas instead of chasing carriers into them.
- A faster Raid reaction when interceptors die without getting kills.
- Maps with more ground outside the defender areas.
