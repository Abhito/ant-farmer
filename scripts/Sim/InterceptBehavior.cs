namespace AntFarmer.Sim;

/// <summary>Hunt enemy carriers anywhere. With none around, shadow the nearest enemy gatherer.</summary>
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

        // Stick with the current carrier rather than switching whenever another gets closer.
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
