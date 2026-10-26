using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>
/// Counter enemy interceptors with defenders, the only role that can kill them. For each enemy interceptor
/// (up to MaxDefenders), the ant that can start soonest guards a post next to it, so the
/// defender walks over and attacks once the interceptor is within its defend radius.
/// Defenders only work near home (MatchSettings.DefendLeash), so while an interceptor is
/// farther out its defender waits at its post, or at the nest if it is new.
/// While an enemy interceptor is dead, its defender stays a defender: the interceptor is back in a few
/// seconds, and switching roles means walking home. With no enemy interceptors, nobody is held back from gathering.
/// </summary>
public class GuardRule : IStrategyRule
{
    public string Name => "Guard";

    /// <summary>Most ants pulled off gathering to defend at once.</summary>
    public int MaxDefenders { get; set; } = 1;

    /// <summary>Defenders sent after each enemy interceptor.</summary>
    public int DefendersPerThreat { get; set; } = 1;

    public void Apply(WorldSnapshot world, StrategyPlan plan)
    {
        // Living threats first, so they get defenders before dead ones hold on to theirs.
        List<AntInfo> threats = world.EnemyAnts.Where(e => e.Role == Role.Intercept).OrderBy(e => e.Dead).ToList();
        if (threats.Count == 0)
            return;

        float radius = world.Settings.DefendRadius;
        float leash = world.Settings.DefendLeash;
        // Dead ants count too: one that dies while defending respawns as a defender.
        List<AntInfo> candidates = plan.FreeAnts.ToList();

        int defenders = 0;
        foreach (AntInfo threat in threats)
        {
            for (int i = 0; i < DefendersPerThreat; i++)
            {
                if (defenders >= MaxDefenders || candidates.Count == 0)
                    return;

                if (threat.Dead)
                {
                    // Keep one of our current defenders where it is until the interceptor respawns.
                    AntInfo? waiting = candidates.Where(a => a.Order?.Role == Role.Defend && a.PendingOrder == null)
                        .OrderBy(a => a.Id).Cast<AntInfo?>().FirstOrDefault();
                    if (waiting is not AntInfo keep)
                        break;

                    candidates.Remove(keep);
                    plan.Assign(keep.Id, keep.Order.Value, $"enemy interceptor {threat.Id} respawning");
                    defenders++;
                    continue;
                }

                // An ant already guarding near this threat keeps the job; otherwise take the closest ant,
                // preferring ones not carrying food so deliveries aren't held up. Ties go to lower ids.
                AntInfo guard = candidates
                    .OrderBy(a => GuardsNear(a, threat, radius) || (a.Dead && a.Order?.Role == Role.Defend) ? 0 : 1)
                    .ThenBy(a => a.Carrying)
                    .ThenBy(a => world.CostToStart(a, Role.Defend, threat.Cell))
                    .ThenBy(a => a.Id)
                    .First();
                candidates.Remove(guard);

                // Keep the current post while the threat is still within reach of it, so the order (and the
                // ant's plan) doesn't change every time the interceptor moves a cell.
                // Post next to the threat if it is in our home area; otherwise wait where the defender already
                // guards, or at the nest.
                bool inHomeArea = world.DistanceFromMyNest(threat.Position) <= leash;
                bool keepPost = GuardsNear(guard, threat, radius) || guard.Dead || !inHomeArea;
                RoleOrder order = keepPost && guard.NextOrder is { Role: Role.Defend } current ? current
                    : RoleOrder.Defend(inHomeArea ? threat.Cell : null);
                plan.Assign(guard.Id, order, $"enemy interceptor {threat.Id} near ({threat.Cell.X},{threat.Cell.Y})");
                defenders++;
            }
        }
    }

    // True if the ant is already defending a post within the defend radius of the threat.
    private static bool GuardsNear(AntInfo ant, AntInfo threat, float radius) =>
        ant.Order is { Role: Role.Defend, Post: Vector2I post } && ((Vector2)post).DistanceTo(threat.Position) <= radius;
}
