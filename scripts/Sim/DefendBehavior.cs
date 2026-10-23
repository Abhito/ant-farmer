using Godot;

namespace AntFarmer.Sim;

/// <summary>
/// Guard a post (a food pile, a choke point, the nest). When an enemy carrying food comes within
/// <see cref="MatchSettings.DefendRadius"/> of the post, chase it and bite it; otherwise return to the post.
/// </summary>
public static class DefendBehavior
{
    public static void Update(Ant ant, Simulation sim)
    {
        if (ant.CarryingFood)
        {
            GatherBehavior.CarryHome(ant, sim);
            return;
        }

        Vector2I post = ant.Order.Post ?? sim.Map.Nests[ant.Team][0];
        float radius = sim.Settings.DefendRadius;

        // Stick with the current target while it still has food and hasn't run far from the post.
        // Past 1.5x the radius the defender gives up, so it doesn't get dragged across the map.
        Ant target = ant.ChaseTarget;
        if (target == null || !target.WasCarrying || ((Vector2)post).DistanceTo(target.PreviousPosition) > radius * 1.5f)
            target = Combat.NearestEnemyCarrier(sim, ant.Team, post, radius);

        ant.ChaseTarget = target;

        if (target != null)
        {
            ant.State = AntState.Chasing;
            Combat.ChaseAndBite(ant, target, sim);
        }
        else
        {
            ant.State = AntState.Guarding;
            ant.GoTo(post, sim.Pathfinder);
        }
    }
}
