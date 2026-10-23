using AntFarmer.Sim;
using Godot;

namespace AntFarmer.View;

/// <summary>
/// Draws every ant as a body plus a head pointing the way it walks, in its team color.
/// Ants carrying food get a green dot. Defenders get a white ring, interceptors a yellow ring,
/// and stunned ants are drawn grey. Positions go through <see cref="MapView.GridToWorld(Vector2)"/>.
/// </summary>
public partial class AntView : Node2D
{
    [Export] public MapView MapView { get; set; }

    private static readonly Color FoodColor = new(0.35f, 0.85f, 0.30f);
    private static readonly Color Outline = new(0f, 0f, 0f, 0.6f);
    private static readonly Color DefendRing = new(1f, 1f, 1f, 0.9f);
    private static readonly Color InterceptRing = new(1f, 0.85f, 0.2f, 0.95f);
    private static readonly Color StunnedColor = new(0.55f, 0.55f, 0.55f);

    private Simulation _sim;

    /// <summary>
    /// How far we are between the last tick and the next, 0 to 1. The simulation only moves ants
    /// 10 times a second; blending between the previous and current position keeps motion smooth.
    /// </summary>
    public float TickBlend { get; set; } = 1f;

    public void SetSimulation(Simulation sim)
    {
        _sim = sim;
        QueueRedraw();
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (_sim == null || MapView == null)
            return;

        float tile = MapView.TileSize;
        float bodyRadius = tile * 0.28f;
        float headRadius = tile * 0.16f;

        foreach (Ant ant in _sim.Ants)
        {
            Vector2 gridPos = ant.PreviousPosition.Lerp(ant.Position, TickBlend);
            Vector2 center = MapView.GridToWorld(gridPos);
            Vector2 headCenter = center + ant.Heading * (bodyRadius + headRadius * 0.6f);
            Color color = ant.IsStunned ? StunnedColor : MapView.TeamColor(ant.Team);

            Color? ring = ant.Order.Role switch
            {
                Role.Defend => DefendRing,
                Role.Intercept => InterceptRing,
                _ => null,
            };
            if (ring is Color ringColor)
                DrawArc(center, bodyRadius + 5f, 0, Mathf.Tau, 24, ringColor, 2.5f);

            DrawCircle(center, bodyRadius + 1.5f, Outline);
            DrawCircle(headCenter, headRadius + 1.5f, Outline);
            DrawCircle(center, bodyRadius, color);
            DrawCircle(headCenter, headRadius, color.Lightened(0.2f));

            if (ant.CarryingFood)
                DrawCircle(headCenter + ant.Heading * headRadius, headRadius * 0.9f, FoodColor);
        }
    }
}
