using System.Collections.Generic;
using System.Linq;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>
/// The orders being put together during one decision. Rules claim ants with Assign;
/// the first rule to claim an ant wins, which is "first applicable" rule arbitration
/// (Millington, AI for Games, §5.8.5).
/// </summary>
public class StrategyPlan
{
    public WorldSnapshot World { get; }

    /// <summary>Set by a rule to skip every rule after it this decision.</summary>
    public bool SkipRemainingRules { get; set; }

    /// <summary>The rule currently running; recorded with each assignment for the log.</summary>
    internal IStrategyRule CurrentRule { get; set; }

    private readonly Dictionary<int, (RoleOrder Order, string Reason)> _assigned = new();

    public StrategyPlan(WorldSnapshot world) => World = world;

    /// <summary>Own ants no rule has claimed yet, in id order.</summary>
    public IEnumerable<AntInfo> FreeAnts => World.MyAnts.Where(a => !_assigned.ContainsKey(a.Id));

    public bool IsFree(int antId) => !_assigned.ContainsKey(antId);

    /// <summary>
    /// Gives a free ant an order. Returns false (and changes nothing) if an earlier rule already
    /// claimed it. The reason is a short explanation for the log.
    /// </summary>
    public bool Assign(int antId, RoleOrder order, string reason)
    {
        if (!IsFree(antId))
            return false;

        _assigned[antId] = (order, $"{CurrentRule?.Name}: {reason}");
        return true;
    }

    /// <summary>The order and reason given to an ant, or null if no rule claimed it.</summary>
    public (RoleOrder Order, string Reason)? OrderFor(int antId) =>
        _assigned.TryGetValue(antId, out var entry) ? entry : null;
}
