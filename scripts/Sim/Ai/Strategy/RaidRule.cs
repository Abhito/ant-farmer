using System.Linq;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>
/// Keep interceptors hunting enemy carriers. Starts with 1, then adjusts once per window from how the raids went.
/// </summary>
public class RaidRule : IStrategyRule
{
    public string Name => "Raid";

    public int Raiders { get; set; } = 1;

    public int MaxRaiders { get; set; } = 2;

    // Off: keep Raiders interceptors for the whole match. Used to measure what adapting is worth.
    public bool Adaptive { get; set; } = true;

    // How far the results have moved the count away from the starting number.
    private int _adjustment;
    private float _lastAdjusted;
    private string _lastResult;

    public void Apply(WorldSnapshot world, StrategyPlan plan)
    {
        int count = System.Math.Clamp(Raiders + _adjustment, 0, MaxRaiders);

        // Wait a full window after each change so the next judgment only sees the new setup.
        if (Adaptive && world.ElapsedSeconds - _lastAdjusted >= plan.Recent.WindowSeconds)
        {
            int kills = plan.Recent.Kills(Role.Intercept);
            int deaths = plan.Recent.Deaths(Role.Intercept);
            int change = count == 0 ? 1                                        // retry after a window without raids
                : kills - deaths >= 2 ? 1                                      // raids are paying off
                : deaths >= 2 && deaths >= kills ? -1                          // interceptors keep dying
                : 0;

            int adjusted = System.Math.Clamp(count + change, 0, MaxRaiders);
            if (adjusted != count)
            {
                _lastResult = count == 0 ? "retrying after a window without raids"
                    : $"{kills} kill(s), {deaths} death(s) in {plan.Recent.WindowSeconds:0}s";
                _adjustment = adjusted - Raiders;
                _lastAdjusted = world.ElapsedSeconds;
                count = adjusted;
            }
        }

        int wanted = System.Math.Min(count, plan.AttackerSlots);
        string reason = $"keep {count} interceptor(s): {_lastResult ?? "opening raid"}";

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
            plan.Assign(ant.Id, order, reason);
        }
    }
}
