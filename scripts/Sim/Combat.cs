using System;
using Godot;

namespace AntFarmer.Sim;

/// <summary>Helpers shared by the behaviors that go after enemy ants (Defend and Intercept).</summary>
public static class Combat
{
    /// <summary>
    /// Bites the target if it is in range and the bite is ready; otherwise keeps closing in.
    /// Like all sensing of other ants, this uses where the target stood at the start of the tick.
    /// </summary>
    public static void ChaseAndBite(Ant ant, Ant target, Simulation sim)
    {
        if (!sim.TryBite(ant, target))
            ant.GoTo(target.PreviousCell, sim.Pathfinder);
    }

    /// <summary>
    /// The closest living enemy to the given point that the attacker is allowed to attack
    /// (see Simulation.CanAttack), ignoring any more than maxDistance cells away in a straight line.
    /// Null if there is none.
    /// </summary>
    public static Ant NearestTarget(Simulation sim, Ant attacker, Vector2 point, float maxDistance = float.PositiveInfinity)
        => NearestEnemy(sim, attacker.Team, point, maxDistance, other => Simulation.CanAttack(attacker, other));

    /// <summary>
    /// The closest living enemy to the given point that passes the filter (any enemy if there is no
    /// filter), ignoring any more than maxDistance cells away in a straight line. Null if there is none.
    /// </summary>
    public static Ant NearestEnemy(Simulation sim, Team team, Vector2 point, float maxDistance = float.PositiveInfinity, Func<Ant, bool> filter = null)
    {
        Ant best = null;
        float bestDistance = maxDistance + Simulation.TieTolerance;

        // Enemies are visited in id order, and each side's ants are numbered the same way, so on a
        // mirrored map both sides break distance ties identically.
        foreach (Ant other in sim.Ants)
        {
            if (other.Team == team || other.IsDead || (filter != null && !filter(other)))
                continue;

            // Other ants are sensed where they stood at the start of the tick.
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
