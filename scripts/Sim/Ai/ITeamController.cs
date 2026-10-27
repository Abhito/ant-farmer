using System.Collections.Generic;

namespace AntFarmer.Sim.Ai;

// Reason is only used for logging.
public readonly record struct AntCommand(int AntId, RoleOrder Order, string Reason = null);

/// <summary>
/// A team's AI. Decide is called every DecisionSeconds and returns role orders.
/// Ants left out keep their current order.
/// </summary>
public interface ITeamController
{
    string Name { get; }

    IEnumerable<AntCommand> Decide(WorldSnapshot world);
}
