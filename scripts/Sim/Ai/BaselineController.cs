using System.Collections.Generic;
using System.Linq;

namespace AntFarmer.Sim.Ai;

/// <summary>Every ant gathers from the nearest pile, all match.</summary>
public class BaselineController : ITeamController
{
    public string Name => "Baseline";

    public IEnumerable<AntCommand> Decide(WorldSnapshot world) =>
        world.MyAnts.Select(a => new AntCommand(a.Id, RoleOrder.Gather()));
}
