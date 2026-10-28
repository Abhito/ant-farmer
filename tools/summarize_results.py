"""Prints the evaluation tables from results/results.csv and saves them to results/summary.md.

Usage:
    python tools/summarize_results.py [results/results.csv]
"""

import csv
import math
import sys
from collections import defaultdict
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
OPPONENTS = ["Baseline", "Fixed 1I", "Fixed 2I", "Fixed 1D+1I", "Fixed 1D"]
ABLATION_OPPONENTS = ["Baseline", "Fixed 2I", "Fixed 1D+1I"]
VARIANTS = ["Full", "Adaptation off", "No Raid", "No Guard"]


def wilson(wins, n, z=1.96):
    """95% confidence interval for a win rate (Wilson score interval)."""
    if n == 0:
        return 0.0, 0.0
    p = wins / n
    center = (p + z * z / (2 * n)) / (1 + z * z / n)
    half = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / (1 + z * z / n)
    return max(0.0, center - half), min(1.0, center + half)


def stats(rows):
    n = len(rows)
    wins = sum(r["winner"] == "strategist" for r in rows)
    draws = sum(r["winner"] == "draw" for r in rows)
    low, high = wilson(wins, n)
    return {
        "n": n,
        "wins": wins,
        "draws": draws,
        "losses": n - wins - draws,
        "rate": wins / n if n else 0.0,
        "low": low,
        "high": high,
        "margin": sum(int(r["strategist_score"]) - int(r["opponent_score"]) for r in rows) / n if n else 0.0,
        "seconds": sum(float(r["seconds"]) for r in rows) / n if n else 0.0,
        "time_limit": sum(r["end_reason"] == "time" for r in rows),
        "switches": sum(int(r["strategist_role_switches"]) for r in rows) / n if n else 0.0,
    }


def table(headers, lines):
    out = ["| " + " | ".join(headers) + " |", "|" + "---|" * len(headers)]
    out += ["| " + " | ".join(str(x) for x in line) + " |" for line in lines]
    return "\n".join(out)


def pct(x):
    return f"{100 * x:.0f}%"


def main():
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else PROJECT / "results" / "results.csv"
    with open(path, newline="") as f:
        rows = list(csv.DictReader(f))

    main_rows = [r for r in rows if r["run"] == "main"]
    ablation_rows = [r for r in rows if r["run"] == "ablation"]
    sanity_rows = [r for r in rows if r["run"] == "sanity"]
    sections = [f"# Evaluation results\n\n{len(rows)} matches from `{path.name}`."]

    # Main run: one line per opponent.
    lines = []
    for opponent in OPPONENTS:
        s = stats([r for r in main_rows if r["opponent"] == opponent])
        lines.append([opponent, s["n"], f"{s['wins']}-{s['draws']}-{s['losses']}",
                      f"{pct(s['rate'])} ({pct(s['low'])}-{pct(s['high'])})", f"{s['margin']:+.1f}",
                      f"{s['seconds']:.0f} s", s["time_limit"], f"{s['switches']:.1f}"])
    sections.append("## Main run (seeds 1001-1070, both sides)\n\n" + table(
        ["Opponent", "Matches", "W-D-L", "Win rate (95% CI)", "Avg margin", "Avg length", "Hit time limit",
         "Role switches"], lines))

    # Side check: the strategist should do about as well from either side.
    lines = []
    for side in ["red", "blue"]:
        s = stats([r for r in main_rows if r["strategist_side"] == side])
        lines.append([side, s["n"], pct(s["rate"])])
    sections.append("## Side check (main run)\n\n" + table(["Strategist side", "Matches", "Win rate"], lines))

    # Pile size breakdown, the factor that decided the hardest matchup in testing.
    lines = []
    for opponent in OPPONENTS:
        by_size = defaultdict(list)
        for r in main_rows:
            if r["opponent"] == opponent:
                by_size[r["pile_size"]].append(r)
        cells = []
        for size in ["3", "4", "5", "6"]:
            s = stats(by_size[size])
            cells.append(f"{s['wins']}/{s['n']}")
        lines.append([opponent] + cells)
    sections.append("## Wins by pile size (main run)\n\n" + table(
        ["Opponent", "Piles of 3", "Piles of 4", "Piles of 5", "Piles of 6"], lines))

    # Ablations, compared with the full strategist on the same seeds (1001-1015).
    lines = []
    for opponent in ABLATION_OPPONENTS:
        for variant in VARIANTS:
            if variant == "Full":
                group = [r for r in main_rows if r["opponent"] == opponent and int(r["seed"]) <= 1015]
            else:
                group = [r for r in ablation_rows if r["opponent"] == opponent and r["variant"] == variant]
            s = stats(group)
            lines.append([opponent, variant, s["n"], f"{s['wins']}-{s['draws']}-{s['losses']}",
                          f"{pct(s['rate'])} ({pct(s['low'])}-{pct(s['high'])})", f"{s['margin']:+.1f}",
                          f"{s['switches']:.1f}"])
    sections.append("## Ablations (seeds 1001-1015, both sides)\n\n" + table(
        ["Opponent", "Strategist", "Matches", "W-D-L", "Win rate (95% CI)", "Avg margin", "Role switches"],
        lines))

    # Sanity: identical AIs should split about evenly.
    red = sum((r["winner"] == "strategist") == (r["strategist_side"] == "red") and r["winner"] != "draw"
              for r in sanity_rows)
    draws = sum(r["winner"] == "draw" for r in sanity_rows)
    blue = len(sanity_rows) - red - draws
    sections.append("## Sanity: Strategist vs. Strategist\n\n" + table(
        ["Matches", "Red wins", "Blue wins", "Draws"], [[len(sanity_rows), red, blue, draws]]))

    report = "\n\n".join(sections) + "\n"
    print(report)
    out = path.parent / "summary.md"
    out.write_text(report)
    print(f"Saved {out}")


if __name__ == "__main__":
    main()
