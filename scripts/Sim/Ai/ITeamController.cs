using System.Collections.Generic;

namespace AntFarmer.Sim.Ai;

/// <summary>An order for one of the team's own ants.</summary>
public readonly record struct AntCommand(int AntId, RoleOrder Order);

/// <summary>
/// A team's strategy. The simulation calls <see cref="Decide"/> every
/// <see cref="MatchSettings.DecisionSeconds"/> with a fresh <see cref="WorldSnapshot"/> and applies the
/// returned orders through <see cref="Simulation.AssignRole"/>. The controller only picks roles;
/// the role behaviors handle pathfinding, picking up food, and biting.
///
/// Ants not mentioned keep their current order, and re-sending an unchanged order does nothing,
/// so a controller can return orders for every ant every time. Orders for enemy ants are ignored.
/// A controller must not use any randomness except the match's own, so matches stay replayable.
/// </summary>
public interface ITeamController
{
    /// <summary>Short name for logs, the HUD, and result tables.</summary>
    string Name { get; }

    IEnumerable<AntCommand> Decide(WorldSnapshot world);
}
