using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AntFarmer.Gen;
using Godot;

namespace AntFarmer.Tools;

/// <summary>
/// Generates many maps headless, checks and measures each one, and writes one CSV row per map.
/// Pass "-- seedsPerPreset sweepSeeds outputDir". Maps are saved next to the CSV so any row can be opened.
/// </summary>
public partial class MapBatch : Node
{
    private record Job(string Run, string Preset, Func<GenParams> Make, int Seed, bool Repair, string SweepParam = "", float SweepValue = float.NaN);

    private static readonly (string Name, Func<GenParams> Make)[] Presets =
    {
        ("Open", GenParams.Open), ("Caves", GenParams.Caves), ("Highlands", GenParams.Highlands), ("Contested", GenParams.Contested),
    };

    // One parameter at a time, starting from the default settings.
    private static readonly (string Param, float[] Values, Action<GenParams, float> Set)[] Sweeps =
    {
        ("RockDensity", new[] { 0.02f, 0.06f, 0.10f, 0.15f, 0.20f, 0.25f }, (p, v) => p.RockDensity = v),
        ("FoodBias", new[] { -1f, -0.5f, 0f, 0.5f, 1f }, (p, v) => p.FoodBias = v),
        ("MinNestDistance", new[] { 30f, 45f, 60f, 75f }, (p, v) => p.MinNestDistance = (int)v),
        ("MaxHillHeight", new[] { 1f, 3f, 5f, 7f }, (p, v) => p.MaxHillHeight = (int)v),
    };

    // Hand-built maps measured as reference points.
    private static readonly string[] ReferenceMaps = { "res://maps/arena_large.txt" };

    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        int seeds = args.Length > 0 ? int.Parse(args[0]) : 100;
        int sweepSeeds = args.Length > 1 ? int.Parse(args[1]) : 30;
        string outputDir = args.Length > 2 ? args[2] : ProjectSettings.GlobalizePath("res://results/maps");

        string mapDir = Path.Combine(outputDir, "generated");
        Directory.CreateDirectory(mapDir);
        string csvPath = Path.Combine(outputDir, "maps.csv");

        List<Job> jobs = AllJobs(seeds, sweepSeeds).ToList();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        List<string> metricNames = null;

        using (var writer = new StreamWriter(csvPath))
        {
            foreach (string path in ReferenceMaps)
            {
                ValidityReport report = MapValidator.Check(Godot.FileAccess.GetFileAsString(path));
                metricNames ??= WriteHeader(writer, report);
                writer.WriteLine(Row("reference", Path.GetFileNameWithoutExtension(path), 0, "hand", "", float.NaN, report));
            }

            foreach (Job job in jobs)
            {
                string text = MapGenerator.Generate(job.Make(), job.Seed, job.Repair);
                string name = job.Run == "sweep"
                    ? $"{job.SweepParam}_{job.SweepValue.ToString(CultureInfo.InvariantCulture)}_{job.Seed:000}.txt"
                    : $"{job.Preset}_{job.Seed:000}_{(job.Repair ? "rep" : "raw")}.txt";
                File.WriteAllText(Path.Combine(mapDir, name), text);

                ValidityReport report = MapValidator.Check(text);
                metricNames ??= WriteHeader(writer, report);
                writer.WriteLine(Row(job.Run, job.Preset, job.Seed, job.Repair ? "repaired" : "raw", job.SweepParam, job.SweepValue, report));
            }
        }

        GD.Print($"Wrote {jobs.Count + ReferenceMaps.Length} rows to {csvPath} in {watch.Elapsed.TotalSeconds:0.0}s.");
        GetTree().Quit();
    }

    private static IEnumerable<Job> AllJobs(int seeds, int sweepSeeds)
    {
        foreach (var (name, make) in Presets)
            foreach (bool repair in new[] { false, true })
                for (int seed = 1; seed <= seeds; seed++)
                    yield return new Job("preset", name, make, seed, repair);

        foreach (var (param, values, set) in Sweeps)
            foreach (float value in values)
                for (int seed = 1; seed <= sweepSeeds; seed++)
                {
                    float v = value;
                    yield return new Job("sweep", "Custom", () => { var p = new GenParams(); set(p, v); return p; }, seed, true, param, value);
                }
    }

    private static List<string> WriteHeader(StreamWriter writer, ValidityReport report)
    {
        List<string> names = MapMetrics.Measure(report).Select(m => m.Name).ToList();
        writer.WriteLine(string.Join(",", new[] { "run", "preset", "seed", "mode", "sweep_param", "sweep_value", "valid", "problems" }.Concat(names)));
        return names;
    }

    private static string Row(string run, string preset, int seed, string mode, string sweepParam, float sweepValue, ValidityReport report)
    {
        string problems = string.Join(";", report.Problems.Select(p => p.Rule).Distinct());
        var cells = new List<string> { run, preset, seed.ToString(), mode, sweepParam, Number(sweepValue), report.IsValid ? "1" : "0", problems };

        // A map that couldn't be read has no measurements.
        if (report.Map != null)
            cells.AddRange(MapMetrics.Measure(report).Select(m => Number(m.Value)));

        return string.Join(",", cells);
    }

    private static string Number(double value) => double.IsNaN(value) ? "" : value.ToString("0.####", CultureInfo.InvariantCulture);
}
