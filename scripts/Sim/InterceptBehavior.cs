namespace AntFarmer.Sim;

/// <summary>
/// Hunt enemy carriers anywhere on the map. Chases the ant named in the order, or else the closest
/// enemy carrying food. With no carriers around it shadows the closest enemy gatherer, so it is already
/// nearby when that ant picks something up. Interceptors are attackers: they never pick up food, and a
/// killed carrier's food goes back to its pile.
/// </summary>
public static class InterceptBehavior
{
    public static void Update(Ant ant, Simulation sim)
    {
        Ant target = null;

        if (ant.Order.TargetAntId is int id)
        {
            Ant ordered = sim.AntById(id);
            if (ordered != null && ordered.Team != ant.Team && !ordered.IsDead)
                target = ordered;
        }

        // Without a named target, stay on the current carrier until it drops its food,
        // instead of switching every time another carrier gets slightly closer.
        // When shadowing, follow gatherers only: walking up to an enemy defender gets the interceptor killed.
        target ??= ant.ChaseTarget is { WasCarrying: true, IsDead: false } current
            ? current
            : Combat.NearestTarget(sim, ant, ant.PreviousPosition)
              ?? Combat.NearestEnemy(sim, ant.Team, ant.PreviousPosition, filter: e => e.Order.Role == Role.Gather);

        ant.ChaseTarget = target;

        if (target == null)
        {
            ant.Stop();
            return;
        }

        Combat.ChaseAndBite(ant, target, sim);
    }
}
