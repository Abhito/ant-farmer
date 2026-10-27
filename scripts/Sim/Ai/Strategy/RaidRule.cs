using System.Linq;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>Keep a few interceptors hunting enemy carriers. Fewer if the enemy has defenders.</summary>
public class RaidRule : IStrategyRule
{
    public string Name => "Raid";

    public int Raiders { get; set; } = 2;

    public int RaidersVsDefenders { get; set; } = 1;

    public void Apply(WorldSnapshot world, StrategyPlan plan)
    {
        bool defended = world.EnemyAnts.Any(e => e.Role == Role.Defend);
        int wanted = defended ? RaidersVsDefenders : Raiders;

        // Current interceptors keep the job; otherwise pick whoever can get home soonest.
        var raiders = plan.FreeAnts
            .OrderBy(a => a.NextOrder?.Role == Role.Intercept ? 0 : 1)
            .ThenBy(a => a.Carrying)
            .ThenBy(a => a.Dead ? 0f : world.CostHome(a))
            .ThenBy(a => a.Id)
            .Take(wanted)
            .ToList();

        foreach (AntInfo ant in raiders)
        {
            RoleOrder order = ant.NextOrder is { Role: Role.Intercept } current ? current : RoleOrder.Intercept();
            string reason = defended
                ? $"keep {wanted} interceptor(s), enemy has defenders"
                : $"keep {wanted} interceptor(s), enemy carriers are unguarded";
            plan.Assign(ant.Id, order, reason);
        }
    }
}
