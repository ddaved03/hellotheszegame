using Godot;
using System;

public partial class WorldController : Node2D
{
    [Export] public NodePath PlayerPath;
    [Export] public NodePath PauseMenuPath;
    
    // --- ÚJ EXPORTÁLT VÁLTOZÓK ---
    [Export] public NodePath BusAnimationPath; // Húzd be ide a busz AnimationPlayer-ét
    [Export] public PackedScene[] VehiclePrefabs; // Húzd be ide az autó és busz .tscn fájljait
    [Export] public float TrafficSpawnInterval = 5.0f; // Hány másodpercenként jöjjön egy autó

    private BasePlayer _player;
    private Control _pauseMenu;
    private AnimationPlayer _busAnim;
    private float _trafficTimer = 0;

    public override void _Ready()
    {
        // Alapértelmezett hivatkozások lekérése
        if (PlayerPath != null) _player = GetNodeOrNull<BasePlayer>(PlayerPath);
        if (PauseMenuPath != null) _pauseMenu = GetNodeOrNull<Control>(PauseMenuPath);

        // --- BUSZ ÉS KEZDÉS BEÁLLÍTÁSA ---
        if (BusAnimationPath != null)
        {
            _busAnim = GetNodeOrNull<AnimationPlayer>(BusAnimationPath);
            if (_busAnim != null)
            {
                StartArrivalCutscene();
            }
        }

        // Pause Menu inicializálása (maradt az eredeti logika)
        if (_pauseMenu != null)
        {
            _pauseMenu.Visible = false;
            _pauseMenu.ProcessMode = ProcessModeEnum.WhenPaused;

            _pauseMenu.GetNode<Button>("VBoxContainer/ResumeButton").Pressed += OnResumePressed;
            _pauseMenu.GetNode<Button>("VBoxContainer/SaveButton").Pressed += OnSavePressed;
            _pauseMenu.GetNode<Button>("VBoxContainer/LoadButton").Pressed += OnLoadPressed;
            _pauseMenu.GetNode<Button>("VBoxContainer/MainMenuButton").Pressed += OnMainMenuPressed;
        }

        if (SaveSystem.LoadRequested)
        {
            SaveSystem.LoadRequested = false;
            if (_player != null) SaveSystem.Load(_player);
        }
    }

    public override void _Process(double delta)
    {
    // Csak akkor jöjjenek autók, ha NINCS szünet (tehát nincs nyitva sem az Inventory, sem a Pause menü)
    if (GetTree().Paused) return;

    _trafficTimer += (float)delta;
    if (_trafficTimer >= TrafficSpawnInterval)
    {
        SpawnRandomTraffic();
        _trafficTimer = 0;
    }
}

    private async void StartArrivalCutscene()
{
    if (_player != null)
    {
        _player.Visible = false;
        _player.ProcessMode = ProcessModeEnum.Disabled;
    }

    // Elindítjuk a buszt
    _busAnim.Play("Arrival");

    // --- IDŐZÍTÉS ---
    // Itt add meg, hány másodperc múlva jelenjen meg a karakter!
    // Példa: 8.5 másodperc
    float waitTime = 8.5f; 
    
    await ToSignal(GetTree().CreateTimer(waitTime), "timeout");

    // Megjelenítjük a karaktert és visszaadjuk az irányítást
    if (_player != null)
    {
        _player.Visible = true;
        _player.ProcessMode = ProcessModeEnum.Always;
    }
}

    private void OnArrivalFinished(StringName animName)
    {
        if (animName == "Arrival" && _player != null)
        {
            _player.Visible = true;
            _player.ProcessMode = ProcessModeEnum.Always; // Visszaadjuk az irányítást
        }
    }

    private void SpawnRandomTraffic()
    {
        if (VehiclePrefabs == null || VehiclePrefabs.Length == 0) return;

        // Kiválasztunk egy véletlen járművet
        int index = GD.RandRange(0, VehiclePrefabs.Length - 1);
        var vehicle = (Node2D)VehiclePrefabs[index].Instantiate();

        // Véletlen irány (0 = jobbról balra, 1 = balról jobbra)
        bool fromRight = GD.Randf() > 0.5f;
        
        float baseYPos = 850; // Az út Y koordinátája

        float yPos = fromRight ? baseYPos : baseYPos + 100.0f;

        // Ezeket az értékeket a mapod méretéhez kell igazítanod!
        float startX = fromRight ? 3200 : -900; 
        float targetX = fromRight ? -900 : 3200;
        

        vehicle.Position = new Vector2(startX, yPos);
        AddChild(vehicle);
        vehicle.ZIndex = fromRight ? 5 : 6;
        
        // Egy egyszerű Tween-nel elmozgatjuk a járművet az út végéig
        var tween = CreateTween();
        tween.TweenProperty(vehicle, "position:x", targetX, 8.0f); // 8 másodperc alatt ér át
        tween.Finished += () => vehicle.QueueFree(); // Ha átért, töröljük

        // Ha balról jön, megfordítjuk a Sprite-ot
        if (!fromRight && vehicle.HasNode("Sprite2D"))
        {
            vehicle.GetNode<Sprite2D>("Sprite2D").FlipH = true;
        }
    }

    // --- EREDETI FUNKCIÓK (Módosítás nélkül) ---

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pause"))
        {
            if (_pauseMenu == null) return;

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

    private void OnSavePressed()
    {
        AudioManager.Instance?.PlayUiClick();
        if (_player != null) SaveSystem.Save(_player);
    }

    private void OnLoadPressed()
    {
        AudioManager.Instance?.PlayUiClick();
        if (_player != null) SaveSystem.Load(_player);
        OnResumePressed();
    }

    private void OnMainMenuPressed()
    {
        AudioManager.Instance?.PlayUiClick();
        OnResumePressed();
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}