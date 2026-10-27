using Godot;

namespace AntFarmer.Sim;

/// <summary>Guard a post and attack enemies that come near it, without leaving the home area.</summary>
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

        // Keep chasing the current target unless it gets too far away.
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
