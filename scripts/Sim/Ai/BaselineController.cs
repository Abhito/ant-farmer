using System.Collections.Generic;
using System.Linq;

namespace AntFarmer.Sim.Ai;

/// <summary>
/// The opponent to beat: every ant gathers from whichever pile is cheapest to reach, all match long.
/// It never defends, never attacks, and never changes its plan.
/// </summary>
public class BaselineController : ITeamController
{
    public string Name => "Baseline";

    public IEnumerable<AntCommand> Decide(WorldSnapshot world) =>
        world.MyAnts.Select(a => new AntCommand(a.Id, RoleOrder.Gather()));
}
