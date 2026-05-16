using Godot;
using System;
using System.Collections.Generic;

public partial class GroundFloorController : Node2D
{
    [Export] public NodePath PlayerPath;
    [Export] public NodePath PauseMenuPath;
    [Export] public NodePath QuestLabelPath;
    [Export] public NodePath AnimationPlayerPath;

    private BasePlayer _player;
    private Control _pauseMenu;
    private Label _questLabel;
    private ColorRect _darknessOverlay;
    private ShaderMaterial _darknessMaterial;
    private AnimationPlayer _sceneAnimPlayer;

    private PackedScene _zombieNormalScene;
    private PackedScene _zombieSmallScene;
    private PackedScene _zombieBigScene;

    // Global spawn settings for big zombies roaming the map
    [Export] public float BigZombieSpawnInterval = 10f;
    [Export] public int MaxBigZombies = 6;
    [Export] public float SpawnMinDistance = 300f;
    [Export] public float SpawnMaxDistance = 800f;

    private Timer _bigZombieSpawnTimer;
    private int _activeBigZombies = 0;
    private bool _globalKeyAssigned = false;
    private bool _globalKeySpawned = false;
    private int _spawnsWithoutAssignment = 0;
    private Rect2 _mapBounds = new Rect2();
    private Vector2[] _spawnAnchors = new Vector2[0];
    private int _nextAnchorIndex = 0;

    private bool _liftEventStarted = false;
    private bool _liftEventCompleted = false;
    private int _liftZombiesAlive = 0;
    private Vector2 _liftSpawnCenter = Vector2.Zero;
    private bool _liftKeySpawned = false;
    // Earthquake approach trigger
    private float _earthquakeTriggerX = float.MaxValue;
    private bool _earthquakeTriggered = false;

    public override void _Ready()
    {
        GD.Print("\n--- FÖLDSZINT KONTROLLER INDUL ---");

        if (PlayerPath != null) _player = GetNodeOrNull<BasePlayer>(PlayerPath);
        if (QuestLabelPath != null) _questLabel = GetNodeOrNull<Label>(QuestLabelPath);
        _darknessOverlay = GetNodeOrNull<ColorRect>("CanvasLayer/DarknessOverlay");

        _zombieNormalScene = GD.Load<PackedScene>("res://scenes/Zombie.tscn");
        _zombieSmallScene = GD.Load<PackedScene>("res://scenes/ZombieSmall.tscn");
        _zombieBigScene = GD.Load<PackedScene>("res://scenes/ZombieBig.tscn");

        // Setup global roaming big-zombie spawner (will start after anchors computed)
        _bigZombieSpawnTimer = new Timer
        {
            WaitTime = BigZombieSpawnInterval,
            OneShot = false,
            Autostart = false
        };
        AddChild(_bigZombieSpawnTimer);
        _bigZombieSpawnTimer.Timeout += OnBigZombieSpawnTimerTimeout;

        // Compute map bounds and spawn anchors (center, left edge, right edge)
        _mapBounds = ComputeMapBounds();
        if (_mapBounds.Size == Vector2.Zero)
        {
            GD.PrintErr("[GroundFloor] Map bounds not found — global spawner will use player-relative fallback.");
        }
        else
        {
            Vector2 center = _mapBounds.Position + _mapBounds.Size * 0.5f;
            float inset = 80f; // keep a little inside the map edge
            Vector2 left = new Vector2(_mapBounds.Position.X + inset, center.Y);
            Vector2 right = new Vector2(_mapBounds.Position.X + _mapBounds.Size.X - inset, center.Y);
            _spawnAnchors = new Vector2[] { center, left, right };
            GD.Print($"[GroundFloor] Spawn anchors set: center={center}, left={left}, right={right}");
        }

        // Start the timer now that anchors are ready
        _bigZombieSpawnTimer.Start();

        // Debug: spawn one initial zombie per anchor so we can verify visibility
        if (_spawnAnchors != null && _spawnAnchors.Length > 0)
        {
            GD.Print("[GroundFloor] Spawning initial test zombies for anchors...");
            for (int i = 0; i < _spawnAnchors.Length; i++)
            {
                SpawnGlobalBigZombie();
            }
        }

        // Start background music for GroundFloor (if present)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBackground();
        }

        // 1. PAUSE MENÜ KERESÉSE ÉS BEKÖTÉSE
        if (PauseMenuPath != null)
        {
            _pauseMenu = GetNodeOrNull<Control>(PauseMenuPath);
            if (_pauseMenu != null)
            {
                GD.Print("SIKER: A Pause menü csomópont megtalálva!");
                _pauseMenu.Visible = false;
                _pauseMenu.ProcessMode = ProcessModeEnum.WhenPaused;

                // Gombok bekötése pontosan úgy, ahogy a World-ben van!
                try
                {
                    _pauseMenu.GetNode<Button>("VBoxContainer/ResumeButton").Pressed += OnResumePressed;
                    _pauseMenu.GetNode<Button>("VBoxContainer/SaveButton").Pressed += OnSavePressed;
                    _pauseMenu.GetNode<Button>("VBoxContainer/LoadButton").Pressed += OnLoadPressed;
                    _pauseMenu.GetNode<Button>("VBoxContainer/MainMenuButton").Pressed += OnMainMenuPressed;
                    GD.Print("SIKER: Pause menü gombjai sikeresen bekötve!");
                }
                catch (Exception e)
                {
                    GD.PrintErr("HIBA: Megvan a Pause menü, de nem találom benne a gombokat! " + e.Message);
                }
            }
            else
            {
                GD.PrintErr("HIBA: A PauseMenuPath be van állítva, de érvénytelen helyre mutat!");
            }
        }
        else
        {
            GD.PrintErr("HIBA: Nincs beállítva a PauseMenuPath a Godot Inspectorban jobb oldalt!");
        }

        if (_darknessOverlay != null)
        {
            _darknessOverlay.Visible = false;
            _darknessOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            _darknessOverlay.Color = new Color(0f, 0f, 0f, 0f);
            SetupDarknessOverlayShader();
        }

        if (AnimationPlayerPath != null)
        {
            _sceneAnimPlayer = GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath);
            if (_sceneAnimPlayer != null) GD.Print("[GroundFloor] AnimationPlayer assigned from inspector.");
        }

        // Determine a simple X position to trigger the earthquake when the player approaches the elevator
        var elevatorTriggerNode = GetNodeOrNull<Area2D>("ElevatorDoor/DetectionArea");
        if (elevatorTriggerNode != null)
        {
            _earthquakeTriggerX = elevatorTriggerNode.GlobalPosition.X - 120f;
            GD.Print($"[GroundFloor] Earthquake trigger X set to {_earthquakeTriggerX}");
        }
        else if (_player != null)
        {
            // fallback: trigger a bit ahead of player's start position
            _earthquakeTriggerX = _player.GlobalPosition.X + 300f;
            GD.Print($"[GroundFloor] Elevator trigger not found, fallback earthquake X set to {_earthquakeTriggerX}");
        }

        // Also try to connect to a dedicated Area2D named "EarthquakeTrigger" (place it in the scene to specify exact spot)
        var quakeArea = GetNodeOrNull<Area2D>("EarthquakeTrigger");
        if (quakeArea == null) quakeArea = GetNodeOrNull<Area2D>("ElevatorDoor/DetectionArea");
        if (quakeArea != null)
        {
            // If the trigger is far away from player start, move it to player's position for easier testing
            if (_player != null)
            {
                float dist = _player.GlobalPosition.DistanceTo(quakeArea.GlobalPosition);
                if (dist > 200f)
                {
                    GD.Print($"[GroundFloor] Moving EarthquakeTrigger above player for testing (dist={dist}).");
                    // place the trigger slightly above the player so it doesn't immediately overlap
                    quakeArea.GlobalPosition = _player.GlobalPosition + new Vector2(0, -150);
                    // disable immediate monitoring and defer connecting to avoid immediate BodyEntered firing
                    quakeArea.Monitoring = false;
                    CallDeferred(nameof(DeferredConnectQuakeArea), quakeArea);
                }
            }

            quakeArea.BodyEntered += OnEarthquakeTriggerBodyEntered;
            GD.Print("[GroundFloor] Connected earthquake trigger area.");
        }

    

        // Kezdeti küldetés beállítása a földszinten
        UpdateQuestText("Küldetés: Menj a lifthez!");

        // Spawn a flashlight slightly ahead of the player and auto-pick it up
        if (_player != null && !InventoryManager.Items.Contains("Flashlight"))
        {
            var flashScene = GD.Load<PackedScene>("res://scenes/FlashlightPickup.tscn");
            if (flashScene != null)
            {
                var flashInst = (Area2D)flashScene.Instantiate();
                // If the script is accessible, set its ItemName to Flashlight
                if (flashInst is TutorialItem ti) ti.ItemName = "Flashlight";

                flashInst.GlobalPosition = _player.GlobalPosition + new Vector2(48, 0);
                flashInst.Scale = new Vector2(0.35f, 0.35f);
                flashInst.ZIndex = 200;
                AddChild(flashInst);
            }
        }

        // Betöltés ellenőrzése
        if (SaveSystem.LoadRequested)
        {
            SaveSystem.LoadRequested = false;
            if (_player != null) SaveSystem.Load(_player);
            RestoreGroundFloorProgress();
        }
    }

    public void RestoreGroundFloorProgress()
    {
        _liftEventCompleted = InventoryManager.Items.Contains("UniversityKey");
        _liftEventStarted = _liftEventCompleted;

        if (_darknessOverlay != null)
        {
            _darknessOverlay.Visible = false;
            _darknessOverlay.Color = new Color(0f, 0f, 0f, 0f);
        }

        if (_liftEventCompleted)
        {
            UpdateQuestText("Küldetés: Nyisd ki a liftet a kulccsal!");
        }
        else
        {
            UpdateQuestText("Küldetés: Menj a lifthez!");
        }
    }

    public void UpdateQuestText(string text)
    {
        if (_questLabel != null) _questLabel.Text = text;
    }

    public void OnKeyPartCollected(string name)
    {
        if (name != "UniversityKey") return;

        _liftEventCompleted = true;


        if (_darknessOverlay != null)
        {
            _darknessOverlay.Visible = false;
            _darknessOverlay.Color = new Color(0f, 0f, 0f, 0f);
        }

        UpdateQuestText("Küldetés: Nyisd ki a liftet a kulccsal!");
    }

    public void TryUseElevator()
    {
        if (InventoryManager.Items.Contains("UniversityKey"))
        {
            GD.Print("Liftkulcs megvan, indítom a földrengés eseményt...");
            StartEarthquakeSequence();
            return;
        }

        if (!_liftEventStarted)
        {
            _liftEventStarted = true;
            UpdateQuestText("Küldetés: Kell a lift kulcsa! 3 nagy zombi jön...");
            StartLiftEncounter();
        }
        else if (!_liftEventCompleted)
        {
            UpdateQuestText("Küldetés: Győzd le a 3 nagy zombit a kulcsért!");
        }
    }

    private async void StartEarthquakeSequence()
    {
        // Darken the scene to simulate power outage
        if (_darknessOverlay != null)
        {
            _darknessOverlay.Visible = true;
            _darknessOverlay.Color = new Color(0f, 0f, 0f, 0.88f);
            UpdateDarknessFocus();
        }

        // Turn on player's flashlight so they can see
        if (_player != null) _player.EquipFlashlight();

        // Spawn falling rocks to form a rough labyrinth toward the elevator
        var rockScene = GD.Load<PackedScene>("res://scenes/Rock.tscn");
        if (rockScene == null)
        {
            GD.PrintErr("Nem találom a Rock.tscn scene-t — nem spawnolok köveket.");
            await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
            GetTree().ChangeSceneToFile("res://scenes/C100.tscn");
            return;
        }

        // Determine a right-side zone around the elevator so the labyrinth forms where the screenshot shows
        var elevatorTrigger = GetNodeOrNull<Area2D>("ElevatorDoor/DetectionArea");
        Vector2 elevatorPos = elevatorTrigger != null ? elevatorTrigger.GlobalPosition : (_player != null ? _player.GlobalPosition + new Vector2(300, 0) : GlobalPosition + new Vector2(300, 0));

        float right = elevatorPos.X + 180f;
        float left = elevatorPos.X - 1200f;
        float top = elevatorPos.Y - 760f;
        if (_player != null)
        {
            left = Mathf.Min(left, _player.GlobalPosition.X + 120f);
        }
        if (left < 0f) left = 0f;

        // Use the bus arrival animation as a centerline for a carved corridor.
        Vector2[] busPoints = null;

        if (_sceneAnimPlayer != null)
        {
            try
            {
                Animation anim = _sceneAnimPlayer.HasAnimation("Arrival") ? _sceneAnimPlayer.GetAnimation("Arrival") : _sceneAnimPlayer.GetAnimation("ZombieBusArrival");
                if (anim != null)
                {
                    for (int t = 0; t < anim.GetTrackCount(); t++)
                    {
                        var path = anim.TrackGetPath(t);
                        if (path != null && path.ToString().Contains("busz"))
                        {
                            int keyCount = anim.TrackGetKeyCount(t);
                            busPoints = new Vector2[keyCount];
                            for (int k = 0; k < keyCount; k++)
                            {
                                try
                                {
                                    var raw = anim.TrackGetKeyValue(t, k);
                                    busPoints[k] = (Vector2)raw;
                                }
                                catch
                                {
                                    busPoints[k] = Vector2.Zero;
                                }
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr("Hiba az AnimationPlayer animáció beolvasásakor: " + e.Message);
            }
        }
        else
        {
            try
            {
                var worldPacked = GD.Load<PackedScene>("res://scenes/World.tscn");
                if (worldPacked != null)
                {
                    var worldInst = (Node2D)worldPacked.Instantiate();
                    var animPlayer = worldInst.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
                    if (animPlayer != null)
                    {
                        // prefer the Arrival animation, fall back to ZombieBusArrival
                        Animation anim = animPlayer.HasAnimation("Arrival") ? animPlayer.GetAnimation("Arrival") : animPlayer.GetAnimation("ZombieBusArrival");
                        if (anim != null)
                        {
                            // find the track that targets the bus position
                            for (int t = 0; t < anim.GetTrackCount(); t++)
                            {
                                var path = anim.TrackGetPath(t);
                                if (path != null && path.ToString().Contains("busz"))
                                {
                                    int keyCount = anim.TrackGetKeyCount(t);
                                    busPoints = new Vector2[keyCount];
                                    for (int k = 0; k < keyCount; k++)
                                    {
                                        try
                                        {
                                            var raw = anim.TrackGetKeyValue(t, k);
                                            busPoints[k] = (Vector2)raw;
                                        }
                                        catch
                                        {
                                            busPoints[k] = Vector2.Zero;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    // free the temporary instance
                    worldInst.QueueFree();
                }
            }
            catch (Exception e)
            {
                GD.PrintErr("Hiba a World.tscn animáció beolvasásakor: " + e.Message);
            }
        }

        int cols = 18;
        int rows = 6;
        float cellW = (right - left) / (cols - 1);
        float cellH = 40f;

        // helper: sample the bus path's Y at given X by linear interpolation of nearest keys
        float SamplePathY(float x)
        {
            if (busPoints == null || busPoints.Length == 0) return elevatorPos.Y;

            // if x before first or after last, clamp
            if (x <= busPoints[0].X) return busPoints[0].Y;
            if (x >= busPoints[busPoints.Length - 1].X) return busPoints[busPoints.Length - 1].Y;

            for (int i = 0; i < busPoints.Length - 1; i++)
            {
                var a = busPoints[i];
                var b = busPoints[i + 1];
                if ((a.X <= x && x <= b.X) || (b.X <= x && x <= a.X))
                {
                    float t = (x - a.X) / (b.X - a.X);
                    return Mathf.Lerp(a.Y, b.Y, t);
                }
            }

            return busPoints[0].Y;
        }

        // corridor half-height — cells within this vertical distance from path center will be left empty
        float corridorHalf = 64f;

        var rockStaticPacked = GD.Load<PackedScene>("res://scenes/RockStatic.tscn");

        // Preferred mode: use designer-defined marker points from RockLandingPoints
        var landingRoot = GetNodeOrNull<Node2D>("RockLandingPoints");
        var landingPoints = new List<Vector2>();
        if (landingRoot != null)
        {
            foreach (var child in landingRoot.GetChildren())
            {
                if (child is Marker2D marker)
                {
                    landingPoints.Add(marker.GlobalPosition);
                }
                else if (child is Node2D node2D)
                {
                    landingPoints.Add(node2D.GlobalPosition);
                }
            }
        }

        if (landingPoints.Count > 0)
        {
            GD.Print($"[GroundFloor] Marker-based rock spawn: {landingPoints.Count} point(s)");

            foreach (var landing in landingPoints)
            {
                Vector2 targetPos = landing;
                var rock = (RigidBody2D)rockScene.Instantiate();
                float spawnAbove = (float)GD.RandRange(160, 280);
                rock.GlobalPosition = targetPos + new Vector2(0, -spawnAbove);
                AddChild(rock);

                var fallTime = (float)GD.RandRange(0.45, 0.95);
                var tween = CreateTween();
                tween.TweenProperty(rock, "global_position", targetPos, fallTime)
                     .SetTrans(Tween.TransitionType.Quad)
                     .SetEase(Tween.EaseType.In);
                tween.Finished += () => {
                    try
                    {
                        if (rockStaticPacked != null)
                        {
                            var staticRock = (Node2D)rockStaticPacked.Instantiate();
                            staticRock.GlobalPosition = targetPos;
                            AddChild(staticRock);
                        }
                    }
                    catch { }
                    rock.QueueFree();
                };

                await ToSignal(GetTree().CreateTimer(0.06f), "timeout");
            }
        }
        else
        {
            // Fallback mode: generated grid with carved corridor
            for (int c = 0; c < cols; c++)
            {
                float x = left + c * cellW;
                float pathY = SamplePathY(x);

                for (int r = 0; r < rows; r++)
                {
                    float y = top + r * cellH;

                    // carve corridor: skip spawn if cell is close to sampled path Y
                    if (Mathf.Abs(y - pathY) <= corridorHalf) continue;
                    var rock = (RigidBody2D)rockScene.Instantiate();
                    // compute target landing position and spawn above so it "falls"
                    Vector2 targetPos = new Vector2(x + (float)GD.RandRange(-6, 6), y + (float)GD.RandRange(-6, 6));
                    float spawnAbove = (float)GD.RandRange(160, 280);
                    Vector2 spawnPos = targetPos + new Vector2(0, -spawnAbove);
                    rock.GlobalPosition = spawnPos;
                    AddChild(rock);

                    // Tween the rock down to the fixed landing spot, then replace with a static rock
                    var fallTime = (float)GD.RandRange(0.45, 0.95);
                    var tween = CreateTween();
                    tween.TweenProperty(rock, "global_position", targetPos, fallTime)
                         .SetTrans(Tween.TransitionType.Quad)
                         .SetEase(Tween.EaseType.In);
                    tween.Finished += () => {
                        try
                        {
                            if (rockStaticPacked != null)
                            {
                                var staticRock = (Node2D)rockStaticPacked.Instantiate();
                                staticRock.GlobalPosition = targetPos;
                                AddChild(staticRock);
                            }
                        }
                        catch { }
                        rock.QueueFree();
                    };

                    await ToSignal(GetTree().CreateTimer(0.02f), "timeout");
                }
            }
        }

        // Let rocks settle for a moment, keep darkness but flashlight on
        await ToSignal(GetTree().CreateTimer(2.0f), "timeout");

        // Keep the player on GroundFloor; the earthquake only creates the blackout and obstacle event.
        return;
    }

    // --- PAUSE MENÜ GOMBOK ÉS BILLENTYŰZET ---
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pause"))
        {
            GD.Print("ESC gomb megnyomva!");
            
            if (_pauseMenu == null) 
            {
                GD.PrintErr("Nem tudom kinyitni a menüt, mert a _pauseMenu nulla! (Nézd meg az Inspectort!)");
                return;
            }

            bool openPauseMenu = !GetTree().Paused;
            if (openPauseMenu)
            {
                GetTree().Paused = true;
                _pauseMenu.Visible = true;
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
            else if (_pauseMenu.Visible) 
            {
                OnResumePressed();
            }
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnResumePressed()
    {
        AudioManager.Instance?.PlayUiClick();
        if (_pauseMenu != null) _pauseMenu.Visible = false;
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void OnSavePressed() { if (_player != null) SaveSystem.Save(_player); }
    
    private void OnLoadPressed() 
    { 
        if (_player != null) 
        {
            SaveSystem.Load(_player); 
            RestoreGroundFloorProgress();
        }
        OnResumePressed(); 
    }

    private void OnMainMenuPressed() { OnResumePressed(); GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn"); }

    private async void StartLiftEncounter()
    {
        if (_darknessOverlay != null)
        {
            _darknessOverlay.Visible = false;
            _darknessOverlay.Color = new Color(0f, 0f, 0f, 0f);
        }

        var elevatorTrigger = GetNodeOrNull<Area2D>("ElevatorDoor/DetectionArea");
        _liftSpawnCenter = elevatorTrigger != null ? elevatorTrigger.GlobalPosition : (_player != null ? _player.GlobalPosition : GlobalPosition);

        if (_questLabel != null)
        {
            _questLabel.Text = "Küldetés: Intézd el a 3 nagy zombit!";
        }

        for (int i = 0; i < 3; i++)
        {
            SpawnLiftZombie(i);
            await ToSignal(GetTree().CreateTimer(0.9f), "timeout");
        }
    }

    private void SpawnLiftZombie(int index)
    {
        PackedScene scene = _zombieBigScene != null ? _zombieBigScene : _zombieNormalScene;
        if (scene == null) return;

        var zombie = (Node2D)scene.Instantiate();
        Vector2[] spawnOffsets =
        {
            new Vector2(-520, -120),
            new Vector2(0, 160),
            new Vector2(520, -40)
        };

        Vector2 randomJitter = new Vector2(
            (float)GD.RandRange(-80, 80),
            (float)GD.RandRange(-60, 60)
        );

        Vector2 spawnPosition = _liftSpawnCenter + spawnOffsets[Mathf.Clamp(index, 0, spawnOffsets.Length - 1)] + randomJitter;

        zombie.GlobalPosition = spawnPosition;
        zombie.ZIndex = 50;

        AddChild(zombie);
        _liftZombiesAlive++;
        AudioManager.Instance?.PlayZombieSpawn(spawnPosition);

        Vector2 dropPosition = spawnPosition;

        zombie.TreeExited += () =>
        {
            _liftZombiesAlive--;

            if (_liftEventStarted && !_liftEventCompleted && _liftZombiesAlive <= 0 && !_liftKeySpawned)
            {
                _liftKeySpawned = true;
                SpawnLiftKey(dropPosition);
            }
        };
    }

    // Global roaming spawner timeout handler
    private void OnBigZombieSpawnTimerTimeout()
    {
        // stop spawning if key already obtained or event completed
        if (_liftEventCompleted || InventoryManager.Items.Contains("UniversityKey"))
        {
            if (_bigZombieSpawnTimer != null) _bigZombieSpawnTimer.Stop();
            return;
        }

        // If the player already triggered the lift encounter, don't spawn roaming zombies
        if (_liftEventStarted) return;

        if (_activeBigZombies >= MaxBigZombies) return;

        SpawnGlobalBigZombie();
    }

    public override void _Process(double delta)
    {
        UpdateDarknessFocus();
        CheckEarthquakeApproach();
    }

    private void SetupDarknessOverlayShader()
    {
        if (_darknessOverlay == null) return;
        if (_darknessMaterial != null) return;

        var shader = new Shader();
        shader.Code = @"
shader_type canvas_item;

uniform vec2 focus_pos = vec2(0.0, 0.0);
uniform float hole_radius = 95.0;
uniform float softness = 70.0;
uniform float darkness_alpha = 0.88;

void fragment() {
    float dist = distance(FRAGCOORD.xy, focus_pos);
    float reveal = 1.0 - smoothstep(hole_radius, hole_radius + softness, dist);
    float alpha = darkness_alpha * (1.0 - reveal);
    COLOR = vec4(0.0, 0.0, 0.0, alpha);
}
";

        _darknessMaterial = new ShaderMaterial();
        _darknessMaterial.Shader = shader;
        _darknessOverlay.Material = _darknessMaterial;
    }

    private void UpdateDarknessFocus()
    {
        if (_darknessOverlay == null || _darknessMaterial == null || _player == null) return;

        Vector2 focusPos = GetViewport().GetCanvasTransform() * (_player.GlobalPosition + new Vector2(0, -10));
        _darknessMaterial.SetShaderParameter("focus_pos", focusPos);
    }

    private void CheckEarthquakeApproach()
    {
        if (_earthquakeTriggered) return;
        if (_player == null) return;

        // First, if there's a named Area2D trigger, check precise overlap (works even if signals didn't fire)
        var quakeArea = GetNodeOrNull<Area2D>("EarthquakeTrigger");
        if (quakeArea != null)
        {
            var cs = quakeArea.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (cs != null && cs.Shape != null)
            {
                bool inside = false;
                if (cs.Shape is CircleShape2D circle)
                {
                    float radius = circle.Radius;
                    inside = _player.GlobalPosition.DistanceTo(quakeArea.GlobalPosition) <= radius;
                }
                else if (cs.Shape is RectangleShape2D rect)
                {
                    Vector2 local = _player.GlobalPosition - quakeArea.GlobalPosition;
                    Vector2 half = rect.Size * 0.5f;
                    inside = Math.Abs(local.X) <= half.X && Math.Abs(local.Y) <= half.Y;
                }

                if (inside)
                {
                    _earthquakeTriggered = true;
                    GD.Print("Játékos beállt az EarthquakeTrigger területre — indítom a rengést.");
                    _liftEventStarted = true;
                    StartEarthquakeSequence();
                    return;
                }
            }
        }

        // Fallback: horizontal threshold — requires UniversityKey to activate
        if (!InventoryManager.Items.Contains("UniversityKey")) return;

        if (_player.GlobalPosition.X >= _earthquakeTriggerX)
        {
            _earthquakeTriggered = true;
            GD.Print("Játékos elérte a rengés trigger küszöböt — indítom a rengést.");
            _liftEventStarted = true;
            StartEarthquakeSequence();
        }
    }

    private void OnEarthquakeTriggerBodyEntered(Node body)
    {
        if (_earthquakeTriggered) return;
        if (body is BasePlayer)
        {
            _earthquakeTriggered = true;
            GD.Print("Játékos belépett a rengés trigger területre — indítom a rengést.");
            _liftEventStarted = true;
            StartEarthquakeSequence();
        }
    }

    private void DeferredConnectQuakeArea(Area2D quakeArea)
    {
        if (quakeArea == null) return;
        quakeArea.BodyEntered += OnEarthquakeTriggerBodyEntered;
        quakeArea.Monitoring = true;
        GD.Print("[GroundFloor] Deferred-connected earthquake trigger.");
    }

    private void SpawnGlobalBigZombie()
    {
        PackedScene scene = _zombieBigScene != null ? _zombieBigScene : _zombieNormalScene;
        if (scene == null) return;
        if (_player == null) return;

        Vector2 spawnPos = Vector2.Zero;

        if (_spawnAnchors != null && _spawnAnchors.Length > 0)
        {
            // Round-robin anchor selection to ensure all anchors are used
            int idx = _nextAnchorIndex % _spawnAnchors.Length;
            _nextAnchorIndex++;
            Vector2 anchor = _spawnAnchors[idx];

            // Narrow jitter so spawns remain visible near anchor
            float jitterX = (float)GD.RandRange(-40f, 40f);
            float jitterY = (float)GD.RandRange(-120f, 120f);
            spawnPos = anchor + new Vector2(jitterX, jitterY);

            // Clamp inside map bounds
            if (_mapBounds.Size != Vector2.Zero)
            {
                spawnPos.X = Mathf.Clamp(spawnPos.X, _mapBounds.Position.X + 10f, _mapBounds.Position.X + _mapBounds.Size.X - 10f);
                spawnPos.Y = Mathf.Clamp(spawnPos.Y, _mapBounds.Position.Y + 10f, _mapBounds.Position.Y + _mapBounds.Size.Y - 10f);
            }

            GD.Print($"[Spawner] Anchor idx={idx}, anchor={anchor}, spawnPos={spawnPos}");
        }
        else
        {
            float angle = (float)GD.Randf() * Mathf.Pi * 2f;
            float dist = (float)GD.RandRange(SpawnMinDistance, SpawnMaxDistance);
            spawnPos = _player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
        }

        var zombie = (Node2D)scene.Instantiate();
        zombie.GlobalPosition = spawnPos;
        zombie.ZIndex = 50;

        AddChild(zombie);
        _activeBigZombies++;
        AudioManager.Instance?.PlayZombieSpawn(spawnPos);

        bool assignKey = false;
        if (!_globalKeyAssigned)
        {
            _spawnsWithoutAssignment++;
            if (GD.Randf() <= 0.25f || _spawnsWithoutAssignment >= 6)
            {
                assignKey = true;
                _globalKeyAssigned = true;
            }
        }

        if (assignKey)
        {
            zombie.SetMeta("KeyCarrier", true);
            GD.Print($"[Spawner] Assigned key carrier at {spawnPos}");
        }
        else
        {
            zombie.SetMeta("KeyCarrier", false);
            GD.Print($"[Spawner] Spawned big zombie at {spawnPos}");
        }

        // Capture drop position now so we don't rely on zombie after it's freed
        Vector2 dropPosition = spawnPos;
        zombie.TreeExited += () =>
        {
            _activeBigZombies--;

            if (!_globalKeySpawned)
            {
                if (zombie.HasMeta("KeyCarrier") && (bool)zombie.GetMeta("KeyCarrier"))
                {
                    _globalKeySpawned = true;
                    GD.Print($"[Spawner] Key carrier died, spawning key at {dropPosition}");
                    SpawnLiftKey(dropPosition);
                }
            }
        };
    }

    private void SpawnLiftKey(Vector2 pos)
    {
        var key = (TutorialItem)GD.Load<PackedScene>("res://scenes/TutorialKeyPart.tscn").Instantiate();
        key.Name = "LiftKey";
        key.ItemName = "UniversityKey";
        key.GlobalPosition = pos;

        var sprite = key.GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite != null)
        {
            sprite.Texture = GD.Load<Texture2D>("res://kepek/kulcs-egybe.png");
        }

        AddChild(key);

        if (_questLabel != null)
        {
            _questLabel.Text = "Küldetés: Vedd fel a lift kulcsát!";
        }
    }

    // Compute map bounds by aggregating RectangleShape2D CollisionShape2D under MapBoundaries
    private Rect2 ComputeMapBounds()
    {
        var mapNode = GetNodeOrNull<Node2D>("MapBoundaries");
        if (mapNode == null) return new Rect2();

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var child in mapNode.GetChildren())
        {
            if (child is CollisionShape2D cs)
            {
                if (cs.Shape is RectangleShape2D rectShape)
                {
                    Vector2 worldPos = cs.GlobalPosition;
                    Vector2 size = rectShape.Size;
                    Vector2 topLeft = worldPos - size * 0.5f;
                    Vector2 bottomRight = topLeft + size;

                    minX = Mathf.Min(minX, topLeft.X);
                    minY = Mathf.Min(minY, topLeft.Y);
                    maxX = Mathf.Max(maxX, bottomRight.X);
                    maxY = Mathf.Max(maxY, bottomRight.Y);
                }
            }
        }

        if (minX == float.MaxValue) return new Rect2();
        return new Rect2(new Vector2(minX, minY), new Vector2(maxX - minX, maxY - minY));
    }
}