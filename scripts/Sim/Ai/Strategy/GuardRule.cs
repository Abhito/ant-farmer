using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>Send a defender after each enemy interceptor, up to MaxDefenders.</summary>
public class GuardRule : IStrategyRule
{
    public string Name => "Guard";

    public int MaxDefenders { get; set; } = 1;

    public int DefendersPerThreat { get; set; } = 1;

    public void Apply(WorldSnapshot world, StrategyPlan plan)
    {
        // Living threats get defenders first.
        List<AntInfo> threats = world.EnemyAnts.Where(e => e.Role == Role.Intercept).OrderBy(e => e.Dead).ToList();
        if (threats.Count == 0)
            return;

        float radius = world.Settings.DefendRadius;
        float leash = world.Settings.DefendLeash;
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
                    // The interceptor will respawn soon, so keep a defender waiting.
                    AntInfo? waiting = candidates.Where(a => a.Order?.Role == Role.Defend && a.PendingOrder == null)
                        .OrderBy(a => a.Id).Cast<AntInfo?>().FirstOrDefault();
                    if (waiting is not AntInfo keep)
                        break;

                    candidates.Remove(keep);
                    plan.Assign(keep.Id, keep.Order.Value, $"enemy interceptor {threat.Id} respawning");
                    defenders++;
                    continue;
                }

                // Prefer an ant already guarding nearby, then the closest one not carrying food.
                AntInfo guard = candidates
                    .OrderBy(a => GuardsNear(a, threat, radius) || (a.Dead && a.Order?.Role == Role.Defend) ? 0 : 1)
                    .ThenBy(a => a.Carrying)
                    .ThenBy(a => world.CostToStart(a, Role.Defend, threat.Cell))
                    .ThenBy(a => a.Id)
                    .First();
                candidates.Remove(guard);

                // Keep the current post while it still covers the threat, so the order doesn't change every cell.
                bool inHomeArea = world.DistanceFromMyNest(threat.Position) <= leash;
                bool keepPost = GuardsNear(guard, threat, radius) || guard.Dead || !inHomeArea;
                RoleOrder order = keepPost && guard.NextOrder is { Role: Role.Defend } current ? current
                    : RoleOrder.Defend(inHomeArea ? threat.Cell : null);
                plan.Assign(guard.Id, order, $"enemy interceptor {threat.Id} near ({threat.Cell.X},{threat.Cell.Y})");
                defenders++;
            }
        }
    }

    private static bool GuardsNear(AntInfo ant, AntInfo threat, float radius) =>
        ant.Order is { Role: Role.Defend, Post: Vector2I post } && ((Vector2)post).DistanceTo(threat.Position) <= radius;
}
