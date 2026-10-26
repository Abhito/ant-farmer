using System.Linq;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>
/// Keep a few standing interceptors hunting enemy carriers. A kill sends the food back to its pile, so the
/// enemy loses the whole trip, and the carrier is dead until it respawns (MatchSettings.RespawnSeconds).
/// Because ants can only change role at the nest, raiding is planned rather than grabbed on the spot: the
/// interceptors are picked once (at the start every ant is at the nest, so it costs nothing) and stay in role.
/// How many to keep depends on the enemy: defenders kill interceptors, so fewer are sent against a defended team.
/// </summary>
public class RaidRule : IStrategyRule
{
    public string Name => "Raid";

    /// <summary>Interceptors to keep while the enemy has no defenders.</summary>
    public int Raiders { get; set; } = 2;

    /// <summary>Interceptors to keep while the enemy has at least one defender.</summary>
    public int RaidersVsDefenders { get; set; } = 1;

    public void Apply(WorldSnapshot world, StrategyPlan plan)
    {
        bool defended = world.EnemyAnts.Any(e => e.Role == Role.Defend);
        int wanted = defended ? RaidersVsDefenders : Raiders;

        // Ants already intercepting keep the job (dead ones too, they respawn in role); then whoever can
        // switch soonest: dead ants and ants near the nest, preferring ones not carrying. Ties go to lower ids.
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
