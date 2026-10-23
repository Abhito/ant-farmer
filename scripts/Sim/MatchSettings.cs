namespace AntFarmer.Sim;

/// <summary>Rules for one match. Everything that changes how a match plays out lives here.</summary>
public class MatchSettings
{
    /// <summary>Seed for the match's random number generator. Same seed + same settings = same match.</summary>
    public int Seed = 1;

    /// <summary>Simulation steps per second of game time. Movement and timers are counted in ticks.</summary>
    public int TicksPerSecond = 10;

    /// <summary>Match ends when time runs out; the side with more deliveries wins.</summary>
    public float MatchSeconds = 180f;

    /// <summary>
    /// A side that reaches this many deliveries wins immediately.
    /// 0 means "more than half the map's food", the point where the other side can no longer catch up.
    /// </summary>
    public int WinDeliveries = 0;

    /// <summary>Ants spawned for each side at the start of the match.</summary>
    public int AntsPerTeam = 3;

    /// <summary>Ant walking speed on flat ground, in cells per second.</summary>
    public float AntSpeed = 3f;

    /// <summary>
    /// Extra movement cost per height level climbed, as a fraction of a normal step.
    /// 1 means climbing one level takes twice as long as a flat step. Going downhill costs nothing extra.
    /// </summary>
    public float ClimbCost = 1f;

    /// <summary>Carrying food slows an ant to this fraction of its normal speed.</summary>
    public float CarrySpeedFactor = 0.75f;

    /// <summary>How close (in cells) an ant must be to bite an enemy.</summary>
    public float BiteRange = 1.2f;

    /// <summary>Seconds between bites for the same ant.</summary>
    public float BiteCooldownSeconds = 2f;

    /// <summary>Seconds a bitten ant is frozen in place.</summary>
    public float StunSeconds = 1.5f;

    /// <summary>Defenders chase enemy carriers that come within this many cells of their post.</summary>
    public float DefendRadius = 5f;

    public int MatchTicks => (int)(MatchSeconds * TicksPerSecond);
}
