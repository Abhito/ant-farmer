using System.Collections.Generic;
using System.Linq;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>Our ants' kills and deaths over the last few seconds, found by comparing snapshots.</summary>
public class RecentEvents
{
    public float WindowSeconds { get; }

    private readonly List<(float Time, Role Role, bool IsKill, bool AtHome)> _events = new();
    private Dictionary<int, AntInfo> _previous = new();

    public RecentEvents(float windowSeconds) => WindowSeconds = windowSeconds;

    public void Update(WorldSnapshot world)
    {
        foreach (AntInfo ant in world.MyAnts)
        {
            if (!_previous.TryGetValue(ant.Id, out AntInfo before))
                continue;

            for (int k = before.Kills; k < ant.Kills; k++)
                _events.Add((world.ElapsedSeconds, before.Role, true, false));

            if (ant.Dead && !before.Dead)
            {
                bool atHome = world.DistanceFromMyNest(ant.Position) <= world.DefendLeash;
                _events.Add((world.ElapsedSeconds, before.Role, false, atHome));
            }
        }

        _previous = world.MyAnts.ToDictionary(a => a.Id);
        _events.RemoveAll(e => world.ElapsedSeconds - e.Time > WindowSeconds);
    }

    public int Kills(Role role) => _events.Count(e => e.IsKill && e.Role == role);

    public int Deaths(Role role) => _events.Count(e => !e.IsKill && e.Role == role);

    // Gatherers killed inside our defender circle.
    public int GathererLossesAtHome => _events.Count(e => !e.IsKill && e.Role == Role.Gather && e.AtHome);
}
