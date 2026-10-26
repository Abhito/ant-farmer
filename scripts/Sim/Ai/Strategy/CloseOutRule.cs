namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>
/// When we are ahead and close to winning, stop taking risks: every free ant gathers and the rules after
/// this one are skipped. Fires when we are within NearWinMargin deliveries of the win
/// target, or when we lead with less than LateSeconds left.
/// </summary>
public class CloseOutRule : IStrategyRule
{
    public string Name => "Close out";

    public int NearWinMargin { get; set; } = 3;

    public float LateSeconds { get; set; } = 20f;

    public void Apply(WorldSnapshot world, StrategyPlan plan)
    {
        if (world.ScoreDifference <= 0)
            return;

        bool nearWin = world.MyDeliveries >= world.WinTarget - NearWinMargin;
        bool lateLead = world.RemainingSeconds <= LateSeconds;
        if (!nearWin && !lateLead)
            return;

        string reason = nearWin
            ? $"{world.WinTarget - world.MyDeliveries} from winning"
            : $"ahead by {world.ScoreDifference} with {world.RemainingSeconds:0}s left";

        foreach (AntInfo ant in plan.FreeAnts)
            plan.Assign(ant.Id, RoleOrder.Gather(), reason);

        plan.SkipRemainingRules = true;
    }
}
