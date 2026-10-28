using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AntFarmer.Sim;
using AntFarmer.Sim.Ai;
using AntFarmer.Sim.Ai.Strategy;
using Godot;

namespace AntFarmer.Tools;

/// <summary>
/// Plays the evaluation matches headless and writes one CSV row per match.
/// Run several copies in parallel, each with its own share (pass "-- slice count outputDir").
/// </summary>
public partial class BatchRunner : Node
{
    private const string MapPath = "res://maps/arena.txt";

    // One match to play. Index is its position in the fixed list, so any match can be re-run on its own.
    private record Job(int Index, string Run, string Variant, string Opponent, int Seed, Team StrategistSide);

    private static readonly string[] Opponents = { "Baseline", "Fixed 1I", "Fixed 2I", "Fixed 1D+1I", "Fixed 1D" };
    private static readonly string[] AblationOpponents = { "Baseline", "Fixed 2I", "Fixed 1D+1I" };
    private static readonly string[] AblationVariants = { "Adaptation off", "No Raid", "No Guard" };

    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        int slice = args.Length > 0 ? int.Parse(args[0]) : 0;
        int count = args.Length > 1 ? int.Parse(args[1]) : 1;
        string outputDir = args.Length > 2 ? args[2] : ProjectSettings.GlobalizePath("res://results/raw");

        Directory.CreateDirectory(outputDir);
        string path = Path.Combine(outputDir, $"slice_{slice}.csv");

        MapData map = MapLoader.LoadFile(MapPath);
        List<Job> jobs = AllJobs().Where(j => j.Index % count == slice).ToList();

        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine(string.Join(",", Columns));
            foreach (Job job in jobs)
            {
                writer.WriteLine(Play(job, map));
                writer.Flush();
            }
        }

        GD.Print($"Slice {slice}/{count}: {jobs.Count} matches written to {path}");
        GetTree().Quit();
    }

    // 700 main + 270 ablation + 30 sanity = 1,000 matches. Seeds 1001+ were never used for tuning.
    private static List<Job> AllJobs()
    {
        var jobs = new List<Job>();
        Team[] sides = { Team.Red, Team.Blue };

        foreach (string opponent in Opponents)
            for (int seed = 1001; seed <= 1070; seed++)
                foreach (Team side in sides)
                    jobs.Add(new Job(jobs.Count, "main", "Full", opponent, seed, side));

        foreach (string variant in AblationVariants)
            foreach (string opponent in AblationOpponents)
                for (int seed = 1001; seed <= 1015; seed++)
                    foreach (Team side in sides)
                        jobs.Add(new Job(jobs.Count, "ablation", variant, opponent, seed, side));

        for (int seed = 1001; seed <= 1015; seed++)
            foreach (Team side in sides)
                jobs.Add(new Job(jobs.Count, "sanity", "Full", "Strategist", seed, side));

        return jobs;
    }

    private static readonly string[] Columns =
    {
        "index", "run", "variant", "opponent", "seed", "strategist_side", "winner",
        "strategist_score", "opponent_score", "seconds", "end_reason",
        "strategist_kills", "opponent_kills", "strategist_role_switches", "opponent_role_switches",
        "pile_size", "regrow_seconds", "defend_leash",
    };

    private static string Play(Job job, MapData map)
    {
        var sim = new Simulation(map, new MatchSettings { Seed = job.Seed });
        Team me = job.StrategistSide;
        Team them = me == Team.Red ? Team.Blue : Team.Red;
        sim.SetController(me, MakeStrategist(job.Variant));
        sim.SetController(them, MakeOpponent(job.Opponent));

        // Count switches to a different role (not pile changes), after the opening decision.
        var lastRole = sim.Ants.ToDictionary(a => a.Id, a => a.Order.Role);
        var switches = new Dictionary<Team, int> { [Team.Red] = 0, [Team.Blue] = 0 };
        sim.RoleChanged += (s, ant, reason) =>
        {
            if (ant.Order.Role != lastRole[ant.Id] && s.Tick > 1)
                switches[ant.Team]++;
            lastRole[ant.Id] = ant.Order.Role;
        };

        while (!sim.IsOver)
            sim.Step();

        string winner = sim.Winner == null ? "draw" : sim.Winner == me ? "strategist" : "opponent";
        string endReason = sim.Deliveries.Values.Any(d => d >= sim.WinTarget) ? "target" : "time";
        var c = CultureInfo.InvariantCulture;

        return string.Join(",",
            job.Index, job.Run, job.Variant, job.Opponent, job.Seed, me.ToString().ToLowerInvariant(), winner,
            sim.Deliveries[me], sim.Deliveries[them], sim.ElapsedSeconds.ToString("0.0", c), endReason,
            sim.Kills[me], sim.Kills[them], switches[me], switches[them],
            sim.PileSize, sim.RegrowSeconds.ToString("0.00", c), sim.DefendLeash.ToString("0.00", c));
    }

    private static ITeamController MakeStrategist(string variant) => variant switch
    {
        "Adaptation off" => new StrategistController(new List<IStrategyRule>
        {
            new GuardRule { Adaptive = false },
            new RaidRule { Adaptive = false },
            new SpreadGatherRule(),
        }),
        "No Raid" => new StrategistController(new List<IStrategyRule> { new GuardRule(), new SpreadGatherRule() }),
        "No Guard" => new StrategistController(new List<IStrategyRule> { new RaidRule(), new SpreadGatherRule() }),
        _ => new StrategistController(),
    };

    private static ITeamController MakeOpponent(string name) => name switch
    {
        "Baseline" => new BaselineController(),
        "Fixed 1I" => new FixedMixController(0, 1),
        "Fixed 2I" => new FixedMixController(0, 2),
        "Fixed 1D+1I" => new FixedMixController(1, 1),
        "Fixed 1D" => new FixedMixController(1, 0),
        "Strategist" => new StrategistController(),
        _ => throw new ArgumentException($"Unknown opponent {name}"),
    };
}
