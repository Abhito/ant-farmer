using Godot;

namespace AntFarmer.Sim;

/// <summary>
/// Guard a post (a food pile, a choke point, the nest). When an enemy carrier or an enemy interceptor comes
/// within the defend radius of the post (MatchSettings.DefendRadius), chase it and bite it; otherwise
/// return to the post.
/// Defenders stay home: everything happens within the leash distance of their nest
/// (MatchSettings.DefendLeash), so a post farther out falls back to the nest and a target that leaves
/// that area is let go.
/// Defenders are attackers: they never pick up food.
/// </summary>
public static class DefendBehavior
{
    public static void Update(Ant ant, Simulation sim)
    {
        float radius = sim.Settings.DefendRadius;
        float leash = sim.Settings.DefendLeash;
        bool InHomeArea(Vector2 point) => sim.DistanceFromNest(ant.Team, point) <= leash;

        Vector2I post = ant.Order.Post ?? sim.Map.Nests[ant.Team][0];
        if (!InHomeArea(post))
            post = sim.Map.Nests[ant.Team][0];

        // Stick with the current target while it is still a valid target, still in the home area, and
        // hasn't run far from the post (past 1.5x the radius the defender gives up).
        Ant target = ant.ChaseTarget;
        if (target == null || target.IsDead || !Simulation.CanAttack(ant, target)
            || ((Vector2)post).DistanceTo(target.PreviousPosition) > radius * 1.5f
            || !InHomeArea(target.PreviousPosition))
            target = Combat.NearestEnemy(sim, ant.Team, post, radius,
                other => Simulation.CanAttack(ant, other) && InHomeArea(other.PreviousPosition));

        ant.ChaseTarget = target;

        if (target != null)
            Combat.ChaseAndBite(ant, target, sim);
        else
            ant.GoTo(post, sim.Pathfinder);
    }
}
