using AntFarmer.Sim.Ai.Strategy;

namespace AntFarmer.Sim.Ai;

public enum ControllerKind
{
    Baseline,
    FixedMix,
    Strategist,
}

public static class Controllers
{
    // The counts are only used by FixedMix.
    public static ITeamController Create(ControllerKind kind, int defenders = 0, int interceptors = 1) => kind switch
    {
        ControllerKind.FixedMix => new FixedMixController(defenders, interceptors),
        ControllerKind.Strategist => new StrategistController(),
        _ => new BaselineController(),
    };
}
