using System.Collections.Generic;

namespace AntFarmer.Sim.Ai;

/// <summary>
/// Gives each ant a role at the start and never changes it: the first <see cref="Interceptors"/> ants
/// intercept, the next <see cref="Defenders"/> guard the nest, and the rest gather. Useful as a
/// comparison: it shows what a role mix is worth on its own, without any adapting.
/// </summary>
public class FixedMixController : ITeamController
{
    public int Defenders { get; }
    public int Interceptors { get; }

    public FixedMixController(int defenders, int interceptors)
    {
        Defenders = defenders;
        Interceptors = interceptors;
    }

    public string Name => $"Fixed {Defenders}D+{Interceptors}I";

    public IEnumerable<AntCommand> Decide(WorldSnapshot world)
    {
        for (int i = 0; i < world.MyAnts.Count; i++)
        {
            RoleOrder order = i < Interceptors ? RoleOrder.Intercept()
                : i < Interceptors + Defenders ? RoleOrder.Defend()
                : RoleOrder.Gather();
            yield return new AntCommand(world.MyAnts[i].Id, order);
        }
    }
}
