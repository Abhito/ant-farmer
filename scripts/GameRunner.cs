using System.Linq;
using AntFarmer.Sim;
using AntFarmer.Sim.Ai;
using AntFarmer.View;
using Godot;

namespace AntFarmer;

/// <summary>
/// Root node of the main scene. Loads the map, creates a <see cref="Simulation"/>, and steps it on a
/// fixed clock (<see cref="MatchSettings.TicksPerSecond"/>) regardless of frame rate. Also hands the
/// state to the view and frames the camera.
/// Keys: R restarts the match, Space pauses.
/// </summary>
public partial class GameRunner : Node
{
    [Export(PropertyHint.File, "*.txt")]
    public string MapPath { get; set; } = "res://maps/arena.txt";

    [Export] public MapView MapView { get; set; }
    [Export] public AntView AntView { get; set; }
    [Export] public Hud Hud { get; set; }
    [Export] public Camera2D Camera { get; set; }

    [ExportGroup("Match")]
    [Export] public int Seed { get; set; } = 1;

    /// <summary>Ignore <see cref="Seed"/> and pick a new one each run.</summary>
    [Export] public bool RandomizeSeed { get; set; }

    [Export] public float MatchSeconds { get; set; } = 180f;

    /// <summary>Deliveries for an instant win. 0 = more than half the map's food.</summary>
    [Export] public int WinDeliveries { get; set; } = 0;

    [Export] public int AntsPerTeam { get; set; } = 3;

    /// <summary>Ant walking speed on flat ground, in cells per second.</summary>
    [Export] public float AntSpeed { get; set; } = 3f;

    /// <summary>Game seconds per real second. 1 = normal speed, 4 = four times faster.</summary>
    [Export(PropertyHint.Range, "0,20,0.25")]
    public float SimSpeed { get; set; } = 1f;

    [ExportGroup("AI")]
    [Export] public ControllerKind RedController { get; set; } = ControllerKind.Baseline;
    [Export] public ControllerKind BlueController { get; set; } = ControllerKind.Baseline;

    /// <summary>Defenders used by a <see cref="ControllerKind.FixedMix"/> side.</summary>
    [Export] public int FixedDefenders { get; set; } = 0;

    /// <summary>Interceptors used by a <see cref="ControllerKind.FixedMix"/> side.</summary>
    [Export] public int FixedInterceptors { get; set; } = 1;

    /// <summary>Print a line to the output every time an AI switches an ant to a new role.</summary>
    [Export] public bool LogRoleChanges { get; set; } = true;

    /// <summary>Fraction of the window the map fills, leaving a small border.</summary>
    [ExportGroup("View")]
    [Export(PropertyHint.Range, "0.5,1,0.01")]
    public float CameraFill { get; set; } = 0.95f;

    public MapData Map { get; private set; }
    public Simulation Sim { get; private set; }

    private double _tickTimer;
    private int _lastFoodRemaining;
    private bool _paused;

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(new Color(0.08f, 0.07f, 0.06f));

        Map = MapLoader.LoadFile(MapPath);
        PrintMapSummary();

        StartMatch();

        FitCameraToMap();
        GetViewport().SizeChanged += FitCameraToMap;
    }

    private void StartMatch()
    {
        var settings = new MatchSettings
        {
            Seed = RandomizeSeed ? (int)(Time.GetTicksUsec() % int.MaxValue) : Seed,
            MatchSeconds = MatchSeconds,
            WinDeliveries = WinDeliveries,
            AntsPerTeam = AntsPerTeam,
            AntSpeed = AntSpeed,
        };

        Sim = new Simulation(Map, settings);
        Sim.SetController(Team.Red, Controllers.Create(RedController, FixedDefenders, FixedInterceptors));
        Sim.SetController(Team.Blue, Controllers.Create(BlueController, FixedDefenders, FixedInterceptors));
        Sim.MatchEnded += OnMatchEnded;
        if (LogRoleChanges)
            Sim.RoleChanged += OnRoleChanged;
        _tickTimer = 0;
        _lastFoodRemaining = Sim.FoodRemaining;
        _paused = false;

        MapView.SetMap(Map, Sim.Food);
        AntView.SetSimulation(Sim);
        AntView.TickBlend = 1f;
        Hud.SetSimulation(Sim);
        Hud.Paused = false;

        GD.Print($"Match started: seed {settings.Seed}, {settings.TicksPerSecond} ticks/s, " +
                 $"{settings.MatchSeconds}s limit, first to {Sim.WinTarget} deliveries wins, " +
                 $"{settings.AntsPerTeam} ant(s) per side. " +
                 $"Red: {Sim.Controller(Team.Red).Name}, Blue: {Sim.Controller(Team.Blue).Name}.");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Sim == null || Sim.IsOver || _paused)
            return;

        // Run however many whole ticks fit into the time that has passed.
        // Leftover time carries over, so the tick rate stays exact on average.
        double tickLength = 1.0 / Sim.Settings.TicksPerSecond;
        _tickTimer += delta * SimSpeed;
        while (_tickTimer >= tickLength && !Sim.IsOver)
        {
            _tickTimer -= tickLength;
            Sim.Step();

            if (!Sim.IsOver && Sim.Tick % (Sim.Settings.TicksPerSecond * 10) == 0)
                GD.Print($"  t={Sim.ElapsedSeconds:0}s  red {Sim.Deliveries[Team.Red]}  blue {Sim.Deliveries[Team.Blue]}  food left {Sim.FoodRemaining}");
        }

        AntView.TickBlend = Sim.IsOver ? 1f : (float)(_tickTimer / tickLength);

        if (Sim.FoodRemaining != _lastFoodRemaining)
        {
            _lastFoodRemaining = Sim.FoodRemaining;
            MapView.QueueRedraw();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        switch (key.Keycode)
        {
            case Key.R:
                StartMatch();
                break;
            case Key.Space:
                _paused = !_paused;
                Hud.Paused = _paused;
                break;
        }
    }

    private void OnRoleChanged(Simulation sim, Ant ant)
    {
        GD.Print($"  t={sim.ElapsedSeconds:0.0}s  {ant.Team} ant {ant.Id} -> {ant.Order}");
    }

    private void OnMatchEnded(Simulation sim)
    {
        string result = sim.Winner is Team winner ? $"{winner} wins" : "Draw";
        GD.Print($"Match over at t={sim.ElapsedSeconds:0.0}s: {result} " +
                 $"(red {sim.Deliveries[Team.Red]}, blue {sim.Deliveries[Team.Blue]}; " +
                 $"bites red {sim.Bites[Team.Red]}, blue {sim.Bites[Team.Blue]}; " +
                 $"role changes red {sim.RoleChanges[Team.Red]}, blue {sim.RoleChanges[Team.Blue]}).");
    }

    /// <summary>
    /// Zooms so the whole map fits in the window between the HUD's top bar and bottom line,
    /// and centers it in that space.
    /// </summary>
    private void FitCameraToMap()
    {
        if (Camera == null || Map == null)
            return;

        Vector2 mapSize = MapView.WorldSize;
        Vector2 viewSize = GetViewport().GetVisibleRect().Size;
        Vector2 freeSize = viewSize - new Vector2(0, Hud.BarHeight + Hud.BottomMargin);
        float zoom = Mathf.Min(freeSize.X / mapSize.X, freeSize.Y / mapSize.Y) * CameraFill;

        // The free area's center sits this many pixels below the window's center.
        float screenOffset = (Hud.BarHeight - Hud.BottomMargin) / 2f;

        Camera.Position = MapView.Position + mapSize / 2 - new Vector2(0, screenOffset / zoom);
        Camera.Zoom = new Vector2(zoom, zoom);
    }

    private void PrintMapSummary()
    {
        int rockCells = 0;
        int hillCells = 0;
        for (int y = 0; y < Map.Height; y++)
        {
            for (int x = 0; x < Map.Width; x++)
            {
                if (Map[x, y].Terrain == Terrain.Rock) rockCells++;
                else if (Map[x, y].Height > 0) hillCells++;
            }
        }

        GD.Print($"Loaded {MapPath}: {Map.Width}x{Map.Height}, " +
                 $"{rockCells} rock cells, {hillCells} hill cells, " +
                 $"{Map.Food.Count} food sources ({Map.Food.Sum(f => f.Amount)} food total), " +
                 $"red nest {Map.Nests[Team.Red].Count} cells, blue nest {Map.Nests[Team.Blue].Count} cells.");
    }
}
