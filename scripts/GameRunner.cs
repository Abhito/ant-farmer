using System.Linq;
using AntFarmer.Sim;
using AntFarmer.Sim.Ai;
using AntFarmer.View;
using Godot;

namespace AntFarmer;

/// <summary>Main scene: runs the simulation on a fixed clock and draws it. R restarts, Space pauses.</summary>
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

    [Export] public bool RandomizeSeed { get; set; }

    [Export] public float MatchSeconds { get; set; } = 180f;

    // 0 = more than half the map's food.
    [Export] public int WinDeliveries { get; set; } = 0;

    [Export] public int AntsPerTeam { get; set; } = 5;

    [Export] public float AntSpeed { get; set; } = 5f;

    // Game seconds per real second.
    [Export(PropertyHint.Range, "0,20,0.25")]
    public float SimSpeed { get; set; } = 1f;

    [ExportGroup("AI")]
    [Export] public ControllerKind RedController { get; set; } = ControllerKind.Baseline;
    [Export] public ControllerKind BlueController { get; set; } = ControllerKind.Baseline;

    [Export] public int FixedDefenders { get; set; } = 0;

    [Export] public int FixedInterceptors { get; set; } = 1;

    [Export] public bool LogRoleChanges { get; set; } = true;

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
        {
            Sim.RoleChanged += (sim, ant, reason) => LogRole(sim, ant, $"-> {ant.Order}", reason);
            Sim.RoleQueued += (sim, ant, reason) => LogRole(sim, ant, $"heading home to switch to {ant.PendingOrder}", reason);
        }
        _tickTimer = 0;
        _lastFoodRemaining = Sim.FoodRemaining;
        _paused = false;

        MapView.DefendLeash = settings.DefendLeash;
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

    private static void LogRole(Simulation sim, Ant ant, string what, string reason)
    {
        string why = reason == null ? "" : $"  ({reason})";
        GD.Print($"  t={sim.ElapsedSeconds:0.0}s  {ant.Team} ant {ant.Id} {what}{why}");
    }

    private void OnMatchEnded(Simulation sim)
    {
        string result = sim.Winner is Team winner ? $"{winner} wins" : "Draw";
        GD.Print($"Match over at t={sim.ElapsedSeconds:0.0}s: {result} " +
                 $"(red {sim.Deliveries[Team.Red]}, blue {sim.Deliveries[Team.Blue]}; " +
                 $"kills red {sim.Kills[Team.Red]}, blue {sim.Kills[Team.Blue]}; " +
                 $"role changes red {sim.RoleChanges[Team.Red]}, blue {sim.RoleChanges[Team.Blue]}).");
    }

    // Fit the map between the HUD's top bar and bottom line.
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
