"""Plots and tables for the generated-map batch (results/maps/maps.csv, written by scenes/MapBatch.tscn).

Usage:
    python tools/plot_maps.py [results/maps/maps.csv]

Writes into the CSV's folder:
    summary.md               validity rates and per-preset medians
    distributions.png        each measure's spread per preset
    expressive_range.png     2D density of two measure pairs per preset
    sweeps.png               how single parameters move a measure
"""

import csv
import statistics
import sys
from collections import Counter
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.colors import LinearSegmentedColormap

PRESETS = ["Open", "Caves", "Highlands", "Contested"]
# Categorical slots 1-4 of the validated palette, fixed per preset.
COLORS = {"Open": "#2a78d6", "Caves": "#eb6834", "Highlands": "#1baf7a", "Contested": "#eda100"}
SURFACE = "#fcfcfb"
INK = "#0b0b0b"
INK_2 = "#52514e"
GRID = "#e4e3df"
REFERENCE = "#52514e"
SEQUENTIAL = LinearSegmentedColormap.from_list(
    "blue", ["#fcfcfb", "#cde2fb", "#86b6ef", "#3987e5", "#1c5cab", "#0d366b"])

MEASURES = [
    ("nest_path_cost", "Nest-to-nest path cost"),
    ("food_access", "Food access (5 nearest piles)"),
    ("hill_penalty", "Extra cost from climbing"),
    ("dead_ends", "Dead-end cells"),
    ("exposure_gap", "Nest exposure gap"),
    ("fairness_gap", "Food access gap"),
]
LABELS = dict(MEASURES) | {
    "rock_density": "Rock density",
    "outside_defense": "Ground outside both defender areas",
    "food_nearer_nest_cost": "Pile cost to nearer nest",
}


def load(path):
    rows = []
    with open(path, newline="") as f:
        for row in csv.DictReader(f):
            for key, value in row.items():
                if key not in ("run", "preset", "mode", "sweep_param", "problems"):
                    row[key] = float(value) if value not in ("", None) else None
            red, blue = row.get("food_access_red"), row.get("food_access_blue")
            row["food_access"] = (red + blue) / 2 if red is not None and blue is not None else None
            rows.append(row)
    return rows


def style(ax):
    ax.set_facecolor(SURFACE)
    for side in ("top", "right"):
        ax.spines[side].set_visible(False)
    for side in ("left", "bottom"):
        ax.spines[side].set_color(GRID)
    ax.tick_params(colors=INK_2, labelsize=8, length=0)
    ax.grid(axis="y", color=GRID, linewidth=0.8)
    ax.set_axisbelow(True)


def values(rows, preset, key):
    return [r[key] for r in rows if r["preset"] == preset and r[key] is not None]


def summary(rows, reference, out):
    preset_rows = [r for r in rows if r["run"] == "preset"]
    lines = ["# Generated map batch", "",
             f"{len(preset_rows)} preset maps and {sum(r['run'] == 'sweep' for r in rows)} parameter-sweep maps.", "",
             "## Valid maps", "", "| Preset | Raw | After repair | Why raw maps failed |", "|---|---|---|---|"]
    for preset in PRESETS:
        cells = []
        for mode in ("raw", "repaired"):
            group = [r for r in preset_rows if r["preset"] == preset and r["mode"] == mode]
            cells.append(f"{sum(r['valid'] == 1 for r in group)}/{len(group)}")
        raw_fails = Counter(rule for r in preset_rows if r["preset"] == preset and r["mode"] == "raw"
                            for rule in filter(None, r["problems"].split(";")))
        reasons = ", ".join(f"{rule} ({n})" for rule, n in raw_fails.most_common()) or "-"
        lines.append(f"| {preset} | {cells[0]} | {cells[1]} | {reasons} |")

    sweep_rows = [r for r in rows if r["run"] == "sweep"]
    lines += ["", f"Parameter sweeps (repaired): {sum(r['valid'] == 1 for r in sweep_rows)}/{len(sweep_rows)} valid.", "",
              "## Medians of repaired maps", "",
              "| Measure | " + " | ".join(PRESETS) + " | Hand-built arena |",
              "|---|" + "---|" * (len(PRESETS) + 1)]
    repaired = [r for r in preset_rows if r["mode"] == "repaired"]
    keys = ["rock_density", "hill_share", "nest_distance", "nest_path_cost", "hill_penalty", "food_access",
            "fairness_gap", "exposure_gap", "entrances_red", "dead_ends", "narrow_cells", "outside_defense"]
    for key in keys:
        meds = [statistics.median(values(repaired, p, key)) for p in PRESETS]
        ref = reference.get(key)
        lines.append(f"| {key} | " + " | ".join(f"{m:.2f}" for m in meds) + f" | {ref:.2f} |")
    lines += ["", "The hand-built arena is `maps/arena_large.txt` (81x50, mirrored). Its rock density counts a 2-cell-thick border."]
    (out / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def distributions(rows, reference, out):
    fig, axes = plt.subplots(2, 3, figsize=(11, 6.2), facecolor=SURFACE)
    for ax, (key, title) in zip(axes.flat, MEASURES):
        style(ax)
        data = [values(rows, p, key) for p in PRESETS]
        box = ax.boxplot(data, widths=0.5, patch_artist=True, showfliers=False,
                         medianprops={"color": INK, "linewidth": 1.5},
                         whiskerprops={"color": INK_2}, capprops={"color": INK_2})
        for patch, preset in zip(box["boxes"], PRESETS):
            patch.set_facecolor(COLORS[preset] + "40")
            patch.set_edgecolor(COLORS[preset])
        for i, (preset, vals) in enumerate(zip(PRESETS, data), start=1):
            jitter = [i + ((j * 37 % 100) / 100 - 0.5) * 0.3 for j in range(len(vals))]
            ax.scatter(jitter, vals, s=8, color=COLORS[preset], alpha=0.55, linewidths=0)
        if reference.get(key) is not None:
            ax.axhline(reference[key], color=REFERENCE, linestyle="--", linewidth=1)
            ax.annotate("hand-built arena", (4.45, reference[key]), fontsize=7, color=INK_2,
                        ha="right", va="bottom")
        ax.set_xticks(range(1, len(PRESETS) + 1), PRESETS)
        ax.set_title(title, fontsize=10, color=INK, loc="left")
    fig.suptitle("Measures of 100 repaired maps per preset", fontsize=12, color=INK, x=0.01, ha="left")
    fig.tight_layout()
    fig.savefig(out / "distributions.png", dpi=150, facecolor=SURFACE)
    plt.close(fig)


def expressive_range(rows, reference, out):
    pairs = [("nest_path_cost", "hill_penalty"), ("food_access", "dead_ends")]
    fig, axes = plt.subplots(len(pairs), len(PRESETS), figsize=(12, 6.2), facecolor=SURFACE, sharex="row", sharey="row")
    for row_axes, (x_key, y_key) in zip(axes, pairs):
        xs_all = [r[x_key] for r in rows if r[x_key] is not None]
        ys_all = [r[y_key] for r in rows if r[y_key] is not None]
        extent = [[min(xs_all), max(xs_all)], [min(ys_all), max(ys_all)]]
        for ax, preset in zip(row_axes, PRESETS):
            style(ax)
            ax.grid(False)
            group = [r for r in rows if r["preset"] == preset and r[x_key] is not None and r[y_key] is not None]
            ax.hist2d([r[x_key] for r in group], [r[y_key] for r in group], bins=14, range=extent, cmap=SEQUENTIAL, cmin=1)
            if reference.get(x_key) is not None and reference.get(y_key) is not None:
                ax.scatter([reference[x_key]], [reference[y_key]], marker="x", s=40, color=INK, linewidths=1.5)
            ax.set_title(preset, fontsize=10, color=INK, loc="left")
            ax.set_xlabel(LABELS[x_key], fontsize=8, color=INK_2)
        row_axes[0].set_ylabel(LABELS[y_key], fontsize=8, color=INK_2)
    fig.suptitle("Expressive range: 100 repaired maps per preset (darker = more maps, x = hand-built arena)",
                 fontsize=12, color=INK, x=0.01, ha="left")
    fig.tight_layout()
    fig.savefig(out / "expressive_range.png", dpi=150, facecolor=SURFACE)
    plt.close(fig)


def sweeps(rows, out):
    plan = [("RockDensity", "nest_path_cost"), ("FoodBias", "food_nearer_nest_cost"),
            ("MinNestDistance", "nest_path_cost"), ("MaxHillHeight", "hill_penalty")]
    fig, axes = plt.subplots(1, len(plan), figsize=(12, 3.4), facecolor=SURFACE)
    for ax, (param, key) in zip(axes, plan):
        style(ax)
        group = [r for r in rows if r["run"] == "sweep" and r["sweep_param"] == param and r[key] is not None]
        settings = sorted({r["sweep_value"] for r in group})
        data = [[r[key] for r in group if r["sweep_value"] == s] for s in settings]
        ax.boxplot(data, widths=0.5, patch_artist=True, showfliers=False,
                   boxprops={"facecolor": "#cde2fb", "edgecolor": "#2a78d6"},
                   medianprops={"color": INK, "linewidth": 1.5},
                   whiskerprops={"color": INK_2}, capprops={"color": INK_2})
        ax.set_xticks(range(1, len(settings) + 1), [f"{s:g}" for s in settings])
        ax.set_xlabel(param, fontsize=8, color=INK_2)
        ax.set_title(LABELS[key], fontsize=10, color=INK, loc="left")
    fig.suptitle("One parameter at a time (default settings otherwise, 30 seeds each)",
                 fontsize=12, color=INK, x=0.01, ha="left")
    fig.tight_layout()
    fig.savefig(out / "sweeps.png", dpi=150, facecolor=SURFACE)
    plt.close(fig)


def main():
    path = Path(sys.argv[1] if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent / "results" / "maps" / "maps.csv")
    rows = load(path)
    reference = next(r for r in rows if r["run"] == "reference")
    generated = [r for r in rows if r["run"] != "reference"]
    repaired_presets = [r for r in generated if r["run"] == "preset" and r["mode"] == "repaired"]
    out = path.parent

    summary(generated, reference, out)
    distributions(repaired_presets, reference, out)
    expressive_range(repaired_presets, reference, out)
    sweeps(generated, out)
    print(f"Wrote summary.md, distributions.png, expressive_range.png, sweeps.png to {out}")


if __name__ == "__main__":
    main()
