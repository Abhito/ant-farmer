using System.Collections.Generic;
using Godot;

namespace AntFarmer.Sim;

/// <summary>Walk to a pile, take one food, carry it home, repeat.</summary>
public static class GatherBehavior
{
    public static void Update(Ant ant, Simulation sim)
    {
        if (ant.CarryingFood)
            CarryHome(ant, sim);
        else
            SeekFood(ant, sim);
    }

    public static void CarryHome(Ant ant, Simulation sim)
    {
        HeadToNest(ant, sim);

        // Any nest cell counts, even one it's just passing through.
        if (sim.IsAtNest(ant))
            sim.DeliverCarried(ant);
    }

    // For an ant walking home to switch roles. Drops off food on the way.
    public static void ReturnHome(Ant ant, Simulation sim)
    {
        if (ant.CarryingFood)
        {
            CarryHome(ant, sim);
            return;
        }

        ant.ChaseTarget = null;
        HeadToNest(ant, sim);
    }

    private static void HeadToNest(Ant ant, Simulation sim)
    {
        List<Vector2I> nest = sim.Map.Nests[ant.Team];
        if (ant.Destination is not Vector2I goal || !nest.Contains(goal))
            ant.GoTo(NearestCell(ant.Cell, nest, sim.Pathfinder), sim.Pathfinder);
    }

    private static void SeekFood(Ant ant, Simulation sim)
    {
        if (ant.TargetFood == null || ant.TargetFood.Amount <= 0)
        {
            ant.TargetFood = ChooseFood(ant, sim);
            if (ant.TargetFood == null)
            {
                ant.Stop();
                return;
            }
        }

        ant.GoTo(ant.TargetFood.Cell, sim.Pathfinder);

        if (ant.Arrived && ant.Cell == ant.TargetFood.Cell)
        {
            if (sim.TakeFood(ant.TargetFood))
            {
                ant.CarryingFood = true;
                ant.CarriedFrom = ant.TargetFood;
                CarryHome(ant, sim);
            }
            else
            {
                // Someone took the last unit first.
                ant.TargetFood = null;
            }
        }
    }

    // The ordered pile if it still has food and is reachable, otherwise the nearest one.
    private static FoodPile ChooseFood(Ant ant, Simulation sim)
    {
        if (ant.Order.FoodId is int id)
        {
            FoodPile ordered = sim.FoodById(id);
            if (ordered != null && ordered.Amount > 0 &&
                !float.IsPositiveInfinity(sim.Pathfinder.TravelCost(ant.Cell, ordered.Cell)))
                return ordered;
        }

        return NearestFood(ant, sim);
    }

    private static FoodPile NearestFood(Ant ant, Simulation sim)
    {
        FoodPile best = null;
        float bestCost = float.MaxValue;

        foreach (FoodPile pile in sim.Food)
        {
            if (pile.Amount <= 0)
                continue;

            float cost = sim.Pathfinder.TravelCost(ant.Cell, pile.Cell);
            if (IsCloser(ant.Cell, pile.Cell, cost, best?.Cell, bestCost))
            {
                bestCost = cost;
                best = pile;
            }
        }

        return best;
    }

    private static Vector2I NearestCell(Vector2I from, List<Vector2I> cells, Pathfinder pathfinder)
    {
        Vector2I? best = null;
        float bestCost = float.MaxValue;

        foreach (Vector2I cell in cells)
        {
            float cost = pathfinder.TravelCost(from, cell);
            if (IsCloser(from, cell, cost, best, bestCost))
            {
                bestCost = cost;
                best = cell;
            }
        }

        return best ?? cells[0];
    }

    // Near-equal path costs are broken by straight-line distance, which keeps mirrored sides identical.
    private static bool IsCloser(Vector2I from, Vector2I candidate, float cost, Vector2I? best, float bestCost)
    {
        if (float.IsPositiveInfinity(cost))
            return false;
        if (best == null || cost < bestCost - Simulation.TieTolerance)
            return true;
        if (cost > bestCost + Simulation.TieTolerance)
            return false;

        return ((Vector2)from).DistanceSquaredTo(candidate) < ((Vector2)from).DistanceSquaredTo(best.Value);
    }
}
