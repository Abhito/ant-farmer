using AntFarmer.Sim;
using Godot;

namespace AntFarmer.View;

/// <summary>
/// On-screen score, clock, and end-of-match banner. Builds its own controls in code and reads
/// everything it shows from the <see cref="Simulation"/> each frame.
/// </summary>
public partial class Hud : CanvasLayer
{
    /// <summary>Height of the top bar in pixels. The camera keeps the map below it.</summary>
    public const float BarHeight = 44f;

    /// <summary>Space kept free at the bottom for the info line.</summary>
    public const float BottomMargin = 30f;

    private Simulation _sim;

    private Label _redScore;
    private Label _blueScore;
    private Label _clock;
    private Label _info;
    private Control _banner;
    private Label _bannerTitle;
    private Label _bannerDetail;

    /// <summary>Set by the runner; shows a "Paused" note while true.</summary>
    public bool Paused { get; set; }

    public void SetSimulation(Simulation sim)
    {
        EnsureBuilt();
        _sim = sim;
        _banner.Visible = false;
    }

    public override void _Ready() => EnsureBuilt();

    /// <summary>
    /// Creates the controls the first time they are needed. The runner may hand us a simulation
    /// before our own _Ready has run (it comes earlier in the scene), so this can't wait for _Ready.
    /// </summary>
    private void EnsureBuilt()
    {
        if (_banner != null)
            return;

        BuildTopBar();
        BuildBanner();
    }

    public override void _Process(double delta)
    {
        if (_sim == null)
            return;

        _redScore.Text = $"RED ({ControllerName(Team.Red)})  {_sim.Deliveries[Team.Red]}";
        _blueScore.Text = $"{_sim.Deliveries[Team.Blue]}  ({ControllerName(Team.Blue)}) BLUE";

        int seconds = Mathf.CeilToInt(_sim.RemainingSeconds);
        _clock.Text = $"{seconds / 60}:{seconds % 60:00}";

        string paused = Paused ? "PAUSED  ·  " : "";
        _info.Text = $"{paused}first to {_sim.WinTarget}  ·  food left {_sim.FoodRemaining}  ·  seed {_sim.Settings.Seed}";

        if (_sim.IsOver && !_banner.Visible)
            ShowBanner();
    }

    private string ControllerName(Team team) => _sim.Controller(team)?.Name ?? "no AI";

    private void ShowBanner()
    {
        if (_sim.Winner is Team winner)
        {
            _bannerTitle.Text = $"{winner.ToString().ToUpper()} WINS";
            _bannerTitle.AddThemeColorOverride("font_color", MapView.TeamColor(winner));
        }
        else
        {
            _bannerTitle.Text = "DRAW";
            _bannerTitle.AddThemeColorOverride("font_color", Colors.White);
        }

        _bannerDetail.Text = $"Red {_sim.Deliveries[Team.Red]}  –  {_sim.Deliveries[Team.Blue]} Blue\n" +
                             $"after {_sim.ElapsedSeconds:0.0} s\n\nR to restart";
        _banner.Visible = true;
    }

    private void BuildTopBar()
    {
        var background = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.55f),
        };
        background.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        background.OffsetBottom = BarHeight;
        AddChild(background);

        var row = new HBoxContainer();
        row.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        row.OffsetLeft = 16;
        row.OffsetRight = -16;
        background.AddChild(row);

        _redScore = MakeLabel(26, MapView.TeamColor(Team.Red), HorizontalAlignment.Left);
        _clock = MakeLabel(26, Colors.White, HorizontalAlignment.Center);
        _blueScore = MakeLabel(26, MapView.TeamColor(Team.Blue), HorizontalAlignment.Right);
        foreach (Label label in new[] { _redScore, _clock, _blueScore })
        {
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            label.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(label);
        }

        _info = MakeLabel(14, new Color(1f, 1f, 1f, 0.75f), HorizontalAlignment.Center);
        _info.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _info.OffsetTop = -26;
        _info.OffsetBottom = -6;
        AddChild(_info);
    }

    private void BuildBanner()
    {
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);
        _banner = center;
        _banner.Visible = false;

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.04f, 0.03f, 0.85f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 48,
            ContentMarginRight = 48,
            ContentMarginTop = 24,
            ContentMarginBottom = 24,
        });
        center.AddChild(panel);

        var column = new VBoxContainer();
        panel.AddChild(column);

        _bannerTitle = MakeLabel(48, Colors.White, HorizontalAlignment.Center);
        _bannerDetail = MakeLabel(20, Colors.White, HorizontalAlignment.Center);
        column.AddChild(_bannerTitle);
        column.AddChild(_bannerDetail);
    }

    private static Label MakeLabel(int fontSize, Color color, HorizontalAlignment align)
    {
        var label = new Label { HorizontalAlignment = align };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }
}
