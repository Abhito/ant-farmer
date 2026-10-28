using System.Linq;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>
/// Defend only when our gatherers are dying at home: 1 defender after a few losses, 2 if they keep coming,
/// none after a window without losses. Defenders are added right away but dropped at most once per window.
/// </summary>
public class GuardRule : IStrategyRule
{
    public string Name => "Guard";

    public int MaxDefenders { get; set; } = 2;

    // Gatherers lost at home within the window for each defender.
    public int LossesPerDefender { get; set; } = 2;

    private int _count;
    private float _changed = float.NegativeInfinity;

    public void Apply(WorldSnapshot world, StrategyPlan plan)
    {
        int losses = plan.Recent.GathererLossesAtHome;

        // One loss is not enough to add or drop a defender.
        int desired = losses >= 2 * LossesPerDefender ? 2
            : losses >= LossesPerDefender ? 1
            : losses == 0 ? 0
            : _count;
        desired = System.Math.Min(desired, MaxDefenders);

        bool canDrop = world.ElapsedSeconds - _changed >= plan.Recent.WindowSeconds;
        if (desired > _count || (desired < _count && canDrop))
        {
            _count = desired;
            _changed = world.ElapsedSeconds;
        }

        int wanted = System.Math.Min(_count, plan.AttackerSlots);
        if (wanted == 0)
            return;

        string reason = $"{losses} gatherer(s) lost at home in {plan.Recent.WindowSeconds:0}s";

        // Current defenders keep the job; then gatherers, so interceptors aren't pulled off raids.
        var guards = plan.FreeAnts
            .OrderBy(a => a.NextOrder?.Role switch { Role.Defend => 0, Role.Gather => 1, _ => 2 })
            .ThenBy(a => a.Carrying)
            .ThenBy(a => a.Dead ? 0f : world.CostHome(a))
            .ThenBy(a => a.Id)
            .Take(wanted)
            .ToList();

        foreach (AntInfo ant in guards)
        {
            RoleOrder order = ant.NextOrder is { Role: Role.Defend } current ? current : RoleOrder.Defend();
            plan.Assign(ant.Id, order, reason);
        }
    }
}
