namespace AntFarmer.Sim.Ai;

/// <summary>The controllers that can be picked for a side in the Inspector or a batch run.</summary>
public enum ControllerKind
{
    Baseline,
    FixedMix,
}

public static class Controllers
{
    /// <summary>
    /// Creates a fresh controller for one match. <paramref name="defenders"/> and
    /// <paramref name="interceptors"/> are only used by <see cref="ControllerKind.FixedMix"/>.
    /// </summary>
    public static ITeamController Create(ControllerKind kind, int defenders = 0, int interceptors = 1) => kind switch
    {
        ControllerKind.FixedMix => new FixedMixController(defenders, interceptors),
        _ => new BaselineController(),
    };
}
