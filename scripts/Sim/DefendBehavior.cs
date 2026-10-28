using Godot;

namespace AntFarmer.Sim;

/// <summary>Attack enemies that enter the home area, then return to the post.</summary>
public static class DefendBehavior
{
    public static void Update(Ant ant, Simulation sim)
    {
        float leash = sim.Settings.DefendLeash;
        bool InHomeArea(Vector2 point) => sim.DistanceFromNest(ant.Team, point) <= leash;

        Vector2I post = ant.Order.Post ?? sim.Map.Nests[ant.Team][0];
        if (!InHomeArea(post))
            post = sim.Map.Nests[ant.Team][0];

        // Keep chasing the current target until it dies, stops being a valid target, or leaves the area.
        Ant target = ant.ChaseTarget;
        if (target == null || target.IsDead || !Simulation.CanAttack(ant, target) || !InHomeArea(target.PreviousPosition))
            target = Combat.NearestEnemy(sim, ant.Team, ant.PreviousPosition,
                filter: other => Simulation.CanAttack(ant, other) && InHomeArea(other.PreviousPosition));

        ant.ChaseTarget = target;

        if (target != null)
            Combat.ChaseAndBite(ant, target, sim);
        else
            ant.GoTo(post, sim.Pathfinder);
    }
}
