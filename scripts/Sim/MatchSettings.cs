namespace AntFarmer.Sim;

/// <summary>All the rules for one match.</summary>
public class MatchSettings
{
    public int Seed = 1;

    public int TicksPerSecond = 10;

    public float MatchSeconds = 180f;

    public int WinDeliveries = 30;

    public int AntsPerTeam = 5;

    // Cells per second on flat ground.
    public float AntSpeed = 5f;

    // Extra step cost per height level climbed. 1 means one level up takes twice as long.
    public float ClimbCost = 1f;

    public float CarrySpeedFactor = 0.7f;
    public float InterceptorSpeedFactor = 1.1f;
    public float DefenderSpeedFactor = 0.8f;

    public float SpeedFactor(Role role) => role switch
    {
        Role.Intercept => InterceptorSpeedFactor,
        Role.Defend => DefenderSpeedFactor,
        _ => 1f,
    };

    public float BiteRange = 1.2f;
    public float BiteCooldownSeconds = 2f;
    public float RespawnSeconds = 3f;

    // Each bite cooldown and each respawn is this many seconds longer or shorter, at random.
    public float BiteCooldownJitter = 0.3f;
    public float RespawnJitter = 0.5f;

    // Each match rolls one pile size and one regrow time from the seed, used for every pile.
    public int MinPileSize = 3;
    public int MaxPileSize = 6;
    public float MinRegrowSeconds = 10f;
    public float MaxRegrowSeconds = 20f;

    // The pile closest to a nest takes this much longer to regrow; the farthest takes the base time.
    public float NearNestRegrowFactor = 1.5f;

    // Defenders attack enemies inside this distance from their nest, and never leave it.
    // Each match rolls one value from this range, the same for both sides.
    public float MinDefendLeash = 12f;
    public float MaxDefendLeash = 16f;

    // How often each side's AI gives new orders.
    public float DecisionSeconds = 0.5f;

    public int MatchTicks => (int)(MatchSeconds * TicksPerSecond);

    public int DecisionTicks => System.Math.Max(1, (int)System.MathF.Round(DecisionSeconds * TicksPerSecond));
}
