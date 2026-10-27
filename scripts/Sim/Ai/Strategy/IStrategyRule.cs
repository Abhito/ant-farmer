namespace AntFarmer.Sim.Ai.Strategy;

/// <summary>One strategist rule. It may give roles to ants that earlier rules didn't claim.</summary>
public interface IStrategyRule
{
    string Name { get; }

    void Apply(WorldSnapshot world, StrategyPlan plan);
}
