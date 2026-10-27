using System;
using Godot;

namespace AntFarmer.Sim;

/// <summary>Helpers for ants that attack (Defend and Intercept).</summary>
public static class Combat
{
    public static void ChaseAndBite(Ant ant, Ant target, Simulation sim)
    {
        if (!sim.TryBite(ant, target))
            ant.GoTo(target.PreviousCell, sim.Pathfinder);
    }

    // The closest enemy this attacker is allowed to bite.
    public static Ant NearestTarget(Simulation sim, Ant attacker, Vector2 point, float maxDistance = float.PositiveInfinity)
        => NearestEnemy(sim, attacker.Team, point, maxDistance, other => Simulation.CanAttack(attacker, other));

    public static Ant NearestEnemy(Simulation sim, Team team, Vector2 point, float maxDistance = float.PositiveInfinity, Func<Ant, bool> filter = null)
    {
        Ant best = null;
        float bestDistance = maxDistance + Simulation.TieTolerance;

        // Id order breaks ties the same way for both sides.
        foreach (Ant other in sim.Ants)
        {
            if (other.Team == team || other.IsDead || (filter != null && !filter(other)))
                continue;

            float distance = point.DistanceTo(other.PreviousPosition);
            if (distance < bestDistance - Simulation.TieTolerance)
            {
                best = other;
                bestDistance = distance;
            }
        }

        return best;
    }
}
