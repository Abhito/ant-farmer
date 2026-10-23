using Godot;

namespace AntFarmer.Sim;

/// <summary>Helpers shared by the behaviors that go after enemy ants (Defend and Intercept).</summary>
public static class Combat
{
    /// <summary>
    /// Bites <paramref name="target"/> if it is in range and the bite is ready; otherwise keeps closing in.
    /// Like all sensing of other ants, this uses where the target stood at the start of the tick.
    /// </summary>
    public static void ChaseAndBite(Ant ant, Ant target, Simulation sim)
    {
        if (!sim.TryBite(ant, target))
            ant.GoTo(target.PreviousCell, sim.Pathfinder);
    }

    /// <summary>
    /// The enemy ant carrying food that is closest to <paramref name="point"/>, ignoring any farther than
    /// <paramref name="maxDistance"/> cells (straight line). Null if there is none.
    /// </summary>
    public static Ant NearestEnemyCarrier(Simulation sim, Team team, Vector2 point, float maxDistance = float.PositiveInfinity)
        => NearestEnemy(sim, team, point, maxDistance, carriersOnly: true);

    /// <summary>The enemy ant closest to <paramref name="point"/>, carrying or not. Null if there is none.</summary>
    public static Ant NearestEnemy(Simulation sim, Team team, Vector2 point, float maxDistance = float.PositiveInfinity, bool carriersOnly = false)
    {
        const float Tolerance = 0.001f;

        Ant best = null;
        float bestDistance = maxDistance + Tolerance;

        // Enemies are visited in id order, and each side's ants are numbered the same way, so on a
        // mirrored map both sides break distance ties identically.
        foreach (Ant other in sim.Ants)
        {
            if (other.Team == team || (carriersOnly && !other.WasCarrying))
                continue;

            // Other ants are sensed where they stood at the start of the tick.
            float distance = point.DistanceTo(other.PreviousPosition);
            if (distance < bestDistance - Tolerance)
            {
                best = other;
                bestDistance = distance;
            }
        }

        return best;
    }
}
