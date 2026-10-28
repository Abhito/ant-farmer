using System.Collections.Generic;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>
/// Rule-based team AI (Millington §5.8, §6.4.1). Rules run in priority order and claim ants;
/// the rest gather. A new role is held for HoldSeconds so ants don't flip back and forth (§6.4.2).
/// </summary>
public class StrategistController : ITeamController
{
    public float HoldSeconds { get; set; } = 2f;

    public int MinGatherers { get; set; } = 2;

    // Rules judge results over this window (kills and deaths of our ants).
    public RecentEvents Recent { get; } = new(20f);

    public IReadOnlyList<IStrategyRule> Rules { get; }

    public string Name => "Strategist";

    private readonly Dictionary<int, float> _lastChange = new();

    public StrategistController() : this(CreateDefaultRules()) { }

    public StrategistController(IReadOnlyList<IStrategyRule> rules) => Rules = rules;

    // Earlier rules get first pick. CloseOutRule hurt in testing, so it's left out.
    public static List<IStrategyRule> CreateDefaultRules() => new()
    {
        new GuardRule(),
        new RaidRule(),
        new SpreadGatherRule(),
    };

    public IEnumerable<AntCommand> Decide(WorldSnapshot world)
    {
        Recent.Update(world);

        var plan = new StrategyPlan(world, Recent, MinGatherers);
        foreach (IStrategyRule rule in Rules)
        {
            plan.CurrentRule = rule;
            rule.Apply(world, plan);
            if (plan.SkipRemainingRules)
                break;
        }

        var commands = new List<AntCommand>();
        foreach (AntInfo ant in world.MyAnts)
        {
            (RoleOrder order, string reason) = plan.OrderFor(ant.Id) ?? (RoleOrder.Gather(), "no rule needed it, gather");

            // Compare with NextOrder so a switch already in progress isn't cancelled.
            if (order == ant.NextOrder)
                continue;

            bool holding = _lastChange.TryGetValue(ant.Id, out float changedAt)
                           && world.ElapsedSeconds - changedAt < HoldSeconds;
            if (holding)
                continue;

            _lastChange[ant.Id] = world.ElapsedSeconds;
            commands.Add(new AntCommand(ant.Id, order, reason));
        }

        return commands;
    }
}
