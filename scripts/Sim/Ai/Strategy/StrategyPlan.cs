using System.Collections.Generic;
using System.Linq;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>Orders built up during one decision. The first rule to claim an ant wins (Millington §5.8.5).</summary>
public class StrategyPlan
{
    public WorldSnapshot World { get; }
    public RecentEvents Recent { get; }
    public int MinGatherers { get; }

    public bool SkipRemainingRules { get; set; }

    internal IStrategyRule CurrentRule { get; set; }

    private readonly Dictionary<int, (RoleOrder Order, string Reason)> _assigned = new();

    public StrategyPlan(WorldSnapshot world, RecentEvents recent, int minGatherers)
    {
        World = world;
        Recent = recent;
        MinGatherers = minGatherers;
    }

    public IEnumerable<AntInfo> FreeAnts => World.MyAnts.Where(a => !_assigned.ContainsKey(a.Id));

    // How many more attackers can be assigned while keeping MinGatherers ants gathering.
    public int AttackerSlots => System.Math.Max(0,
        World.MyAnts.Count - MinGatherers - _assigned.Values.Count(a => a.Order.Role != Role.Gather));

    public bool IsFree(int antId) => !_assigned.ContainsKey(antId);

    // Returns false if an earlier rule already claimed the ant.
    public bool Assign(int antId, RoleOrder order, string reason)
    {
        if (!IsFree(antId))
            return false;

        _assigned[antId] = (order, $"{CurrentRule?.Name}: {reason}");
        return true;
    }

    public (RoleOrder Order, string Reason)? OrderFor(int antId) =>
        _assigned.TryGetValue(antId, out var entry) ? entry : null;
}
