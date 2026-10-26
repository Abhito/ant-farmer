using AntFarmer.Sim.Ai.Strategy;

namespace AntFarmer.Sim.Ai;

/// <summary>The controllers that can be picked for a side in the Inspector or a batch run.</summary>
public enum ControllerKind
{
    Baseline,
    FixedMix,
    Strategist,
}

public static class Controllers
{
    /// <summary>
    /// Creates a fresh controller for one match. The defender and interceptor counts are only
    /// used by ControllerKind.FixedMix.
    /// </summary>
    public static ITeamController Create(ControllerKind kind, int defenders = 0, int interceptors = 1) => kind switch
    {
        ControllerKind.FixedMix => new FixedMixController(defenders, interceptors),
        ControllerKind.Strategist => new StrategistController(),
        _ => new BaselineController(),
    };
}
