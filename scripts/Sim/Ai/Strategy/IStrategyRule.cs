namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>
/// One piece of the strategist's thinking. Each decision, the rules run in priority order and each one
/// may give some of the still-free ants a role (see StrategyPlan.Assign). Ants no rule
/// claims go back to gathering.
///
/// To add a behavior to the strategist, write a new rule and add it to the list in
/// StrategistController.CreateDefaultRules. A rule may keep state between decisions (rules are created
/// fresh for each match), but most of what it needs is already in the snapshot.
/// </summary>
public interface IStrategyRule
{
    /// <summary>Short name, shown in the role-change log.</summary>
    string Name { get; }

    void Apply(WorldSnapshot world, StrategyPlan plan);
}
