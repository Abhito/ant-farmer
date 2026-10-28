"""Plays all evaluation matches in parallel headless Godot processes, then merges and summarizes them.

Usage:
    python tools/run_evaluation.py --godot PATH_TO_GODOT_CONSOLE_EXE [--processes 14]

The Godot path can also come from the GODOT environment variable. Close the editor first
(or run this from a copy of the project folder): an open editor can stall the headless runs.
"""

import argparse
import csv
import os
import subprocess
import sys
import time
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
RAW_DIR = PROJECT / "results" / "raw"
MERGED = PROJECT / "results" / "results.csv"
EXPECTED_MATCHES = 1000


def main():
    parser = argparse.ArgumentParser(description="Run the evaluation matches.")
    parser.add_argument("--godot", default=os.environ.get("GODOT"), help="Godot .NET console executable")
    parser.add_argument("--processes", type=int, default=min(14, max(1, (os.cpu_count() or 2) - 2)))
    args = parser.parse_args()

    if not args.godot:
        sys.exit("Pass --godot PATH or set the GODOT environment variable.")

    print("Building C#...")
    subprocess.run(["dotnet", "build"], cwd=PROJECT, check=True, stdout=subprocess.DEVNULL)

    # Makes sure the project is imported (needed on a fresh clone).
    subprocess.run([args.godot, "--headless", "--path", str(PROJECT), "--import"],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    RAW_DIR.mkdir(parents=True, exist_ok=True)
    for old in RAW_DIR.glob("slice_*.csv"):
        old.unlink()

    print(f"Playing {EXPECTED_MATCHES} matches in {args.processes} processes...")
    start = time.time()
    processes = [
        subprocess.Popen([args.godot, "--headless", "--path", str(PROJECT), "res://scenes/BatchRunner.tscn",
                          "--", str(i), str(args.processes), str(RAW_DIR)],
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        for i in range(args.processes)
    ]
    for p in processes:
        p.wait()
    print(f"Done in {time.time() - start:.0f} s.")

    rows = []
    header = None
    for path in sorted(RAW_DIR.glob("slice_*.csv")):
        with open(path, newline="") as f:
            reader = csv.reader(f)
            header = next(reader)
            rows.extend(reader)
    rows.sort(key=lambda r: int(r[0]))

    if len(rows) != EXPECTED_MATCHES:
        print(f"Warning: expected {EXPECTED_MATCHES} matches, got {len(rows)}. Check the raw files in {RAW_DIR}.")

    with open(MERGED, "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(header)
        writer.writerows(rows)
    print(f"Merged results: {MERGED}")

    subprocess.run([sys.executable, str(PROJECT / "tools" / "summarize_results.py"), str(MERGED)], check=True)


if __name__ == "__main__":
    main()
