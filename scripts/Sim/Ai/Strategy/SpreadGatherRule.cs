using System.Collections.Generic;
using System.Linq;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>
/// Decides which pile each free ant gathers from, spreading the gatherers out: a pile only takes as many
/// ants as it has food left, so ants don't all walk to the same pile and arrive to find it empty.
/// Each pile's score is the ant's trip cost (to the pile, then home) plus SafetyWeight times
/// how much closer the pile is to our nest than to the enemy's; the lowest score wins. A positive weight
/// takes contested piles first and saves safe ones near home for later. Testing found 0 (no saving)
/// works best once the enemy can kill carriers, so that is the default.
/// </summary>
public class SpreadGatherRule : IStrategyRule
{
    public string Name => "Spread gather";

    public float SafetyWeight { get; set; } = 0f;

    // Path costs from each pile to each nest never change during a match, so work them out once.
    private readonly Dictionary<int, (float Mine, float Theirs)> _nestCosts = new();

    public void Apply(WorldSnapshot world, StrategyPlan plan)
    {
        if (world.Food.Count == 0)
            return;

        // How many ants are already sent to each pile, so a nearly empty pile doesn't attract everyone.
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
                // Attackers sent back to gathering have to go home to switch first.
                float trip = world.CostToStart(ant, Role.Gather, pile.Cell) + mine;
                float score = trip + SafetyWeight * System.Math.Max(0f, theirs - mine);

                // Equal scores go to the pile closer in a straight line, not to whichever is listed first,
                // so the choice is the same for both sides of a mirrored map.
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
