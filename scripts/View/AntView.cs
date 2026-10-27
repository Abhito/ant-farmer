using AntFarmer.Sim;
using Godot;

namespace AntFarmer.View;

/// <summary>Draws the ants: role rings, food dots, pending-switch arcs, and death marks.</summary>
public partial class AntView : Node2D
{
    [Export] public MapView MapView { get; set; }

    private static readonly Color FoodColor = new(0.35f, 0.85f, 0.30f);
    private static readonly Color Outline = new(0f, 0f, 0f, 0.6f);
    private static readonly Color DefendRing = new(1f, 1f, 1f, 0.9f);
    private static readonly Color InterceptRing = new(1f, 0.85f, 0.2f, 0.95f);
    private static readonly Color DeadColor = new(0.55f, 0.55f, 0.55f);

    // Only used for the arc of a switch back to gathering.
    private static readonly Color GatherRing = new(0.45f, 0.9f, 1f, 0.95f);

    private Simulation _sim;

    // 0 to 1 between ticks, for smooth movement.
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
            if (ant.IsDead)
            {
                DrawDeathMark(MapView.GridToWorld(ant.Position), bodyRadius, MapView.TeamColor(ant.Team));
                continue;
            }

            Vector2 gridPos = ant.PreviousPosition.Lerp(ant.Position, TickBlend);
            Vector2 center = MapView.GridToWorld(gridPos);
            Vector2 headCenter = center + ant.Heading * (bodyRadius + headRadius * 0.6f);
            Color color = MapView.TeamColor(ant.Team);

            if (RingColor(ant.Order.Role) is Color ringColor)
                DrawArc(center, bodyRadius + 5f, 0, Mathf.Tau, 24, ringColor, 2.5f);

            // Arc in the color of the role it's walking home to switch to.
            if (ant.PendingOrder is RoleOrder pending)
                DrawArc(center, bodyRadius + 9f, Mathf.Pi * 1.15f, Mathf.Pi * 1.85f, 12,
                        RingColor(pending.Role) ?? GatherRing, 2.5f);

            DrawCircle(center, bodyRadius + 1.5f, Outline);
            DrawCircle(headCenter, headRadius + 1.5f, Outline);
            DrawCircle(center, bodyRadius, color);
            DrawCircle(headCenter, headRadius, color.Lightened(0.2f));

            if (ant.CarryingFood)
                DrawCircle(headCenter + ant.Heading * headRadius, headRadius * 0.9f, FoodColor);
        }
    }

    private static Color? RingColor(Role role) => role switch
    {
        Role.Defend => DefendRing,
        Role.Intercept => InterceptRing,
        _ => null,
    };

    private void DrawDeathMark(Vector2 center, float size, Color teamColor)
    {
        Color color = teamColor.Lerp(DeadColor, 0.5f) with { A = 0.7f };
        Vector2 a = new(size, size);
        Vector2 b = new(size, -size);
        DrawLine(center - a, center + a, color, 3f);
        DrawLine(center - b, center + b, color, 3f);
    }
}
