using System.Collections.Generic;
using System.Linq;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>
/// Sends each free ant to the pile with the cheapest round trip. A pile takes no more ants than it has food.
/// SafetyWeight above 0 favors contested piles first; 0 tested best.
/// </summary>
public class SpreadGatherRule : IStrategyRule
{
    public string Name => "Spread gather";

    public float SafetyWeight { get; set; } = 0f;

    private readonly Dictionary<int, (float Mine, float Theirs)> _nestCosts = new();

    public void Apply(WorldSnapshot world, StrategyPlan plan)
    {
        if (world.Food.Count == 0)
            return;

        var sent = new Dictionary<int, int>();

        foreach (AntInfo ant in plan.FreeAnts.Where(a => !a.Dead).ToList())
        {
            FoodInfo? best = null;
            float bestScore = float.PositiveInfinity;

            foreach (FoodInfo pile in world.Food)
            {
                if (sent.GetValueOrDefault(pile.Id) >= pile.Amount)
                    continue;

                (float mine, float theirs) = NestCosts(world, pile);
                float trip = world.CostToStart(ant, Role.Gather, pile.Cell) + mine;
                float score = trip + SafetyWeight * System.Math.Max(0f, theirs - mine);

                // Ties go to the closer pile in a straight line, so mirrored sides choose alike.
                bool better = score < bestScore - Simulation.TieTolerance
                              || (score <= bestScore + Simulation.TieTolerance && best is FoodInfo current
                                  && ant.Cell.DistanceSquaredTo(pile.Cell) < ant.Cell.DistanceSquaredTo(current.Cell));
                if (better)
                {
                    best = pile;
                    bestScore = score;
                }
            }

            if (best is not FoodInfo chosen)
                continue;

            sent[chosen.Id] = sent.GetValueOrDefault(chosen.Id) + 1;
            (float m, float t) = NestCosts(world, chosen);
            string kind = t - m < 4f ? "contested" : "safe";
            plan.Assign(ant.Id, RoleOrder.Gather(chosen.Id), $"pile {chosen.Id} ({kind}, {chosen.Amount} left)");
        }
    }

    private (float Mine, float Theirs) NestCosts(WorldSnapshot world, FoodInfo pile)
    {
        if (!_nestCosts.TryGetValue(pile.Id, out var costs))
        {
            costs = (world.MyNest.Min(c => world.PathCost(pile.Cell, c)),
                     world.EnemyNest.Min(c => world.PathCost(pile.Cell, c)));
            _nestCosts[pile.Id] = costs;
        }
        return costs;
    }
}
