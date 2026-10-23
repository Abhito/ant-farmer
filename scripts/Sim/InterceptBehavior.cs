namespace AntFarmer.Sim;

/// <summary>
/// Hunt enemy carriers anywhere on the map. Chases the ant named in the order, or else the closest
/// enemy carrying food. With no carriers around it shadows the closest enemy, so it is already nearby
/// when that ant picks something up.
/// </summary>
public static class InterceptBehavior
{
    public static void Update(Ant ant, Simulation sim)
    {
        if (ant.CarryingFood)
        {
            GatherBehavior.CarryHome(ant, sim);
            return;
        }

        Ant target = null;

        if (ant.Order.TargetAntId is int id)
        {
            Ant ordered = sim.AntById(id);
            if (ordered != null && ordered.Team != ant.Team)
                target = ordered;
        }

        // Without a named target, stay on the current carrier until it drops its food,
        // instead of switching every time another carrier gets slightly closer.
        target ??= ant.ChaseTarget is { WasCarrying: true } current
            ? current
            : Combat.NearestEnemyCarrier(sim, ant.Team, ant.PreviousPosition) ?? Combat.NearestEnemy(sim, ant.Team, ant.PreviousPosition);

        ant.ChaseTarget = target;

        if (target == null)
        {
            ant.State = AntState.Idle;
            ant.Stop();
            return;
        }

        ant.State = AntState.Chasing;
        Combat.ChaseAndBite(ant, target, sim);
    }
}
