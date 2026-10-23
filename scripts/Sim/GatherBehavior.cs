using System.Collections.Generic;
using Godot;

namespace AntFarmer.Sim;

/// <summary>
/// Gathering loop for one ant: walk to a food pile, take one unit, carry it to the nearest cell of
/// its own nest, deliver, repeat. The pile is the one named in the ant's order if it still has food,
/// otherwise the cheapest one to reach.
/// </summary>
public static class GatherBehavior
{
    public static void Update(Ant ant, Simulation sim)
    {
        if (ant.CarryingFood)
            CarryHome(ant, sim);
        else
            SeekFood(ant, sim);
    }

    /// <summary>
    /// Walks a carrying ant to its nest and delivers on arrival. Other behaviors call this too, so an
    /// ant that switches roles while holding food still brings it home first.
    /// </summary>
    public static void CarryHome(Ant ant, Simulation sim)
    {
        List<Vector2I> nest = sim.Map.Nests[ant.Team];
        ant.State = AntState.GoingHome;

        // Plan a route home unless already on one (e.g. after picking up, or after a stun cleared the path).
        if (ant.Destination is not Vector2I goal || !nest.Contains(goal))
            ant.GoTo(NearestCell(ant.Cell, nest, sim.Pathfinder), sim.Pathfinder);

        if (ant.Arrived && nest.Contains(ant.Cell))
        {
            sim.Deliver(ant.Team);
            ant.CarryingFood = false;
            ant.TargetFood = null;
            ant.State = AntState.GoingToFood;
        }
    }

    private static void SeekFood(Ant ant, Simulation sim)
    {
        // Pick a pile if we have none or ours ran out on the way.
        if (ant.TargetFood == null || ant.TargetFood.Amount <= 0)
        {
            ant.TargetFood = ChooseFood(ant, sim);
            if (ant.TargetFood == null)
            {
                ant.State = AntState.Idle;
                ant.Stop();
                return;
            }
        }

        ant.State = AntState.GoingToFood;
        ant.GoTo(ant.TargetFood.Cell, sim.Pathfinder);

        if (ant.Arrived && ant.Cell == ant.TargetFood.Cell)
        {
            if (sim.TakeFood(ant.TargetFood))
            {
                ant.CarryingFood = true;
                CarryHome(ant, sim);
            }
            else
            {
                // Someone else took the last unit just before us.
                ant.TargetFood = null;
            }
        }
    }

    /// <summary>The pile from the ant's order if it still has food and can be reached, else the nearest pile.</summary>
    private static FoodPile ChooseFood(Ant ant, Simulation sim)
    {
        if (ant.Order.FoodId is int id)
        {
            FoodPile ordered = sim.FoodById(id);
            if (ordered != null && ordered.Amount > 0 &&
                !float.IsPositiveInfinity(PathCostOrInfinity(ant.Cell, ordered.Cell, sim.Pathfinder)))
                return ordered;
        }

        return NearestFood(ant, sim);
    }

    /// <summary>The non-empty food pile with the cheapest path from the ant, or null if none is reachable.</summary>
    private static FoodPile NearestFood(Ant ant, Simulation sim)
    {
        FoodPile best = null;
        float bestCost = float.MaxValue;

        foreach (FoodPile pile in sim.Food)
        {
            if (pile.Amount <= 0)
                continue;

            float cost = PathCostOrInfinity(ant.Cell, pile.Cell, sim.Pathfinder);
            if (IsCloser(ant.Cell, pile.Cell, cost, best?.Cell, bestCost))
            {
                bestCost = cost;
                best = pile;
            }
        }

        return best;
    }

    /// <summary>The cell in <paramref name="cells"/> with the cheapest path from <paramref name="from"/>.</summary>
    private static Vector2I NearestCell(Vector2I from, List<Vector2I> cells, Pathfinder pathfinder)
    {
        Vector2I? best = null;
        float bestCost = float.MaxValue;

        foreach (Vector2I cell in cells)
        {
            float cost = PathCostOrInfinity(from, cell, pathfinder);
            if (IsCloser(from, cell, cost, best, bestCost))
            {
                bestCost = cost;
                best = cell;
            }
        }

        return best ?? cells[0];
    }

    /// <summary>
    /// True if <paramref name="candidate"/> (path cost <paramref name="cost"/>) beats the current best.
    /// Costs within a small tolerance count as equal, because mirrored routes can add up their steps in a
    /// different order and differ in the last decimal places. Equal costs fall back to straight-line
    /// distance, which is the same on both halves of a mirrored map, instead of whichever target happens
    /// to come first in the list.
    /// </summary>
    private static bool IsCloser(Vector2I from, Vector2I candidate, float cost, Vector2I? best, float bestCost)
    {
        const float Tolerance = 0.001f;

        if (float.IsPositiveInfinity(cost))
            return false;
        if (best == null || cost < bestCost - Tolerance)
            return true;
        if (cost > bestCost + Tolerance)
            return false;

        return ((Vector2)from).DistanceSquaredTo(candidate) < ((Vector2)from).DistanceSquaredTo(best.Value);
    }

    private static float PathCostOrInfinity(Vector2I from, Vector2I to, Pathfinder pathfinder)
    {
        if (from == to)
            return 0f;

        List<Vector2I> path = pathfinder.FindPath(from, to);
        return path.Count == 0 ? float.PositiveInfinity : pathfinder.PathCost(from, path);
    }
}
