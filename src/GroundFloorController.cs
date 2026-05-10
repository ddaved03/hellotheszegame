using Godot;
using System;

public partial class GroundFloorController : Node2D
{
    [Export] public NodePath PlayerPath;
    [Export] public NodePath PauseMenuPath;
    [Export] public NodePath QuestLabelPath;

    private BasePlayer _player;
    private Control _pauseMenu;
    private Label _questLabel;
    private ColorRect _darknessOverlay;

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
            _darknessOverlay.Visible = true;
            _darknessOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            _darknessOverlay.Color = new Color(0f, 0f, 0f, 0.88f);
        }

        // Kezdeti küldetés beállítása a földszinten
        UpdateQuestText("Küldetés: Menj a lifthez!");

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

        // Keep darkness overlay state as-is. We don't automatically clear darkness
        // when the key exists — the user requested the darkness remain.

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


        // Intentionally do NOT hide the darkness overlay here. The scene
        // remains dark even after picking up the key.

        UpdateQuestText("Küldetés: Nyisd ki a liftet a kulccsal!");
    }

    public void TryUseElevator()
    {
        if (InventoryManager.Items.Contains("UniversityKey"))
        {
            GD.Print("Liftkulcs megvan, mehet a C100-as terem.");
            GetTree().ChangeSceneToFile("res://scenes/C100.tscn");
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
            _darknessOverlay.Visible = true;
        }

        var elevatorTrigger = GetNodeOrNull<Area2D>("ElevatorDoor/DetectionArea");
        _liftSpawnCenter = elevatorTrigger != null ? elevatorTrigger.GlobalPosition : (_player != null ? _player.GlobalPosition : GlobalPosition);

        if (_questLabel != null)
        {
            _questLabel.Text = "Küldetés: A sötétben el kell intézni a 3 nagy zombit!";
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