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
    /// Walks a carrying ant to its nest and delivers on arrival. Also used for any ant that has to bring
    /// food home before doing something else.
    /// </summary>
    public static void CarryHome(Ant ant, Simulation sim)
    {
        HeadToNest(ant, sim);

        // Any nest cell will do, even one it is only passing through on the way to another.
        if (sim.IsAtNest(ant))
            sim.DeliverCarried(ant);
    }

    /// <summary>
    /// Walks an ant with a waiting role switch back to its nest, where the switch happens. Food it is
    /// carrying gets delivered on the way in, so it arrives empty-handed for its new role.
    /// </summary>
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

    /// <summary>Plans a route to the nearest cell of the ant's nest, unless it is already heading to one.</summary>
    private static void HeadToNest(Ant ant, Simulation sim)
    {
        List<Vector2I> nest = sim.Map.Nests[ant.Team];
        if (ant.Destination is not Vector2I goal || !nest.Contains(goal))
            ant.GoTo(NearestCell(ant.Cell, nest, sim.Pathfinder), sim.Pathfinder);
    }

    private static void SeekFood(Ant ant, Simulation sim)
    {
        // Pick a pile if we have none or ours ran out on the way.
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
                !float.IsPositiveInfinity(sim.Pathfinder.TravelCost(ant.Cell, ordered.Cell)))
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

            float cost = sim.Pathfinder.TravelCost(ant.Cell, pile.Cell);
            if (IsCloser(ant.Cell, pile.Cell, cost, best?.Cell, bestCost))
            {
                bestCost = cost;
                best = pile;
            }
        }

        return best;
    }

    /// <summary>The cell in the list with the cheapest path from the starting cell.</summary>
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

    /// <summary>
    /// True if the candidate cell, at the given path cost, beats the current best. Costs within
    /// Simulation.TieTolerance count as equal and fall back to straight-line distance, which is the same on
    /// both halves of a mirrored map, instead of whichever target happens to come first in the list.
    /// </summary>
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
