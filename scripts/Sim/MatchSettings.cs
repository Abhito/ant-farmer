namespace AntFarmer.Sim;

/// <summary>All the rules for one match.</summary>
public class MatchSettings
{
    public int Seed = 1;

    public int TicksPerSecond = 10;

    public float MatchSeconds = 180f;

    // Deliveries for an instant win. 0 means more than half the map's food.
    public int WinDeliveries = 0;

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

    // Defenders attack enemies this close to their post...
    public float DefendRadius = 5f;

    // ...and never go farther than this from their nest.
    public float DefendLeash = 14f;

    // How often each side's AI gives new orders.
    public float DecisionSeconds = 0.5f;

    public int MatchTicks => (int)(MatchSeconds * TicksPerSecond);

    public int DecisionTicks => System.Math.Max(1, (int)System.MathF.Round(DecisionSeconds * TicksPerSecond));
}
