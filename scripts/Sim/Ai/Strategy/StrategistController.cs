using System.Collections.Generic;

namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>
/// The adaptive team AI: the top tier of a multi-tier AI (Millington, AI for Games, §6.4.1). It only decides
/// which role each ant has; the role behaviors and the pathfinder below it work out how to carry that out.
///
/// It is a small rule-based system (§5.8): each decision, the rules run in priority order and claim ants,
/// and any ant left unclaimed gathers. To stop ants flipping back and forth when the situation is
/// borderline, an ant keeps a new role for at least HoldSeconds (hysteresis, §6.4.2).
/// </summary>
public class StrategistController : ITeamController
{
    /// <summary>Minimum time an ant keeps a role before the strategist may change it again.</summary>
    public float HoldSeconds { get; set; } = 2f;

    public IReadOnlyList<IStrategyRule> Rules { get; }

    public string Name => "Strategist";

    // When each ant last changed role, so the hold time can be enforced.
    private readonly Dictionary<int, float> _lastChange = new();

    public StrategistController() : this(CreateDefaultRules()) { }

    public StrategistController(IReadOnlyList<IStrategyRule> rules) => Rules = rules;

    /// <summary>
    /// The rules in priority order: earlier rules get first pick of the ants. CloseOutRule is
    /// left out: since roles only change at the nest, pulling attackers back to gather near the end of a match
    /// cost more than it gained in testing. If it is added back, put it after Guard so defenders stay.
    /// </summary>
    public static List<IStrategyRule> CreateDefaultRules() => new()
    {
        new GuardRule(),
        new RaidRule(),
        new SpreadGatherRule(),
    };

    public IEnumerable<AntCommand> Decide(WorldSnapshot world)
    {
        var plan = new StrategyPlan(world);
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

            // Compare with the order the ant will end up with, so a switch it is already walking home
            // for isn't sent again. (Sending its current order instead cancels that switch.)
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
