using System.Linq;
using Godot;

namespace AntFarmer.Tools;

/// <summary>
/// Checks map files headless and prints what's wrong with each.
/// Pass map paths after "--", or "selftest" to run the checker's own tests. With nothing, checks every map in res://maps.
/// </summary>
public partial class MapCheck : Node
{
    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        int exitCode;

        if (args.Length > 0 && args[0] == "selftest")
            exitCode = ValiditySelfTest.Run() ? 0 : 1;
        else
            exitCode = CheckFiles(args.Length > 0 ? args.SelectMany(Expand).ToArray() : MapsInFolder("res://maps")) ? 0 : 1;

        GetTree().Quit(exitCode);
    }

    private static bool CheckFiles(string[] paths)
    {
        bool allValid = true;
        foreach (string path in paths)
        {
            ValidityReport report = MapValidator.Check(FileAccess.GetFileAsString(path));
            allValid &= report.IsValid;

            GD.Print($"{(report.IsValid ? "VALID  " : "INVALID")} {path}");
            foreach (ValidityProblem problem in report.Problems)
                GD.Print($"    {problem}");

            if (report.Map != null)
                GD.Print("    " + string.Join("  ", MapMetrics.Measure(report).Select(m => $"{m.Name}={m.Value:0.###}")));
        }
        return allValid;
    }

    // A folder stands for every map in it.
    private static string[] Expand(string path) => DirAccess.DirExistsAbsolute(path) ? MapsInFolder(path) : new[] { path };

    private static string[] MapsInFolder(string folder) =>
        DirAccess.GetFilesAt(folder).Where(f => f.EndsWith(".txt")).Select(f => $"{folder}/{f}").ToArray();
}
