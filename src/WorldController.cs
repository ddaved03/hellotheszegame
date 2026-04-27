using Godot;
using System;

public partial class WorldController : Node2D
{
    [Export] public NodePath PlayerPath;
    [Export] public NodePath PauseMenuPath;
    
    [Export] public NodePath BusAnimationPath; 
    [Export] public PackedScene[] VehiclePrefabs; 
    [Export] public float TrafficSpawnInterval = 5.0f;

    // --- ÚJ EXPORTÁLT VÁLTOZÓK A TUTORIALHOZ ---
    [Export] public PackedScene ZombieNormalScene; // A sima zombi .tscn fájlja
    [Export] public NodePath QuestLabelPath;      // Húzd be ide a Label-t a CanvasLayer-ről

    [Export] public PackedScene TutorialKeyPartScene;

    private BasePlayer _player;
    private Control _pauseMenu;
    private AnimationPlayer _busAnim;
    private Label _questLabel;
    private float _trafficTimer = 0;

    public override void _Ready()
    {
        if (PlayerPath != null) _player = GetNodeOrNull<BasePlayer>(PlayerPath);
        if (PauseMenuPath != null) _pauseMenu = GetNodeOrNull<Control>(PauseMenuPath);
        
        // Quest Label hivatkozás
        if (QuestLabelPath != null) _questLabel = GetNodeOrNull<Label>(QuestLabelPath);

        if (BusAnimationPath != null)
        {
            _busAnim = GetNodeOrNull<AnimationPlayer>(BusAnimationPath);
            if (_busAnim != null)
            {
                StartArrivalCutscene();
            }
        }

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

        _busAnim.Play("Arrival");

        float waitTime = 8.5f; 
        await ToSignal(GetTree().CreateTimer(waitTime), "timeout");

        if (_player != null)
        {
            _player.Visible = true;
            _player.ProcessMode = ProcessModeEnum.Always;
        }
    }

    // --- ÚJ FUNKCIÓ: A ZOMBIS BUSZ ESEMÉNY ---
public async void StartZombieBusEvent()
{
    if (_questLabel != null)
    {
        _questLabel.Text = "Küldetés: Szerezd meg a kulcsot a buszról!";
    }

    // 1. Keressük meg a kamerát és a buszt
    Camera2D camera = _player.GetNodeOrNull<Camera2D>("Camera2D");
    Node2D busNode = GetNodeOrNull<Node2D>("Sprite2D busz"); // Ellenőrizd a pontos nevet!

    if (camera != null && busNode != null)
    {
        // KIVESSZÜK a kamerát a játékos alól, hogy szabadon mozoghasson
        Vector2 cameraGlobalPos = camera.GlobalPosition;
        _player.RemoveChild(camera);
        AddChild(camera);
        camera.GlobalPosition = cameraGlobalPos;

        // Rövid várakozás a küldetés felirata után
        await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

        // Elindítjuk a busz animációt
        _busAnim.Play("ZombieBusArrival");

        // FOLYAMATOS KÖVETÉS: A kamera a buszhoz "tapad" az animáció alatt
        // Ez kiváltja az ugrálást, mert folyamatosan a busz pozícióját veszi fel
        float timer = 0;
        float duration = 8.5f; // Az animációd hossza másodpercben
        
        while (timer < duration)
        {
            // A kamera követi a busz aktuális pozícióját
            // Használhatunk egy kis simítást (Lerp), hogy ne legyen darabos
            camera.GlobalPosition = camera.GlobalPosition.Lerp(busNode.GlobalPosition, 0.1f);
            
            await ToSignal(GetTree(), "process_frame");
            timer += (float)GetProcessDeltaTime();
        }
    }

    // Busz megállt, jönnek a zombik
    SpawnTenZombies();
    await ToSignal(GetTree().CreateTimer(3.0f), "timeout");

    // 2. VISSZATÉRÉS a játékoshoz
    if (camera != null)
    {
        var tweenBack = CreateTween();
        tweenBack.TweenProperty(camera, "global_position", _player.GlobalPosition, 1.5f).SetTrans(Tween.TransitionType.Cubic);
        await ToSignal(tweenBack, "finished");

        // VISSZATESSZÜK a kamerát a játékos alá, hogy újra kövesse őt
        RemoveChild(camera);
        _player.AddChild(camera);
        camera.Position = Vector2.Zero;
    }
}

    private int _tutorialZombiesAlive = 0;

private void SpawnTenZombies()
{
    if (ZombieNormalScene == null) return;

    Vector2 busDoorPos = new Vector2(-463f, 493f); 
    _tutorialZombiesAlive = 10; // Beállítjuk a számlálót

    for (int i = 0; i < 10; i++)
    {
        var zombie = (Node2D)ZombieNormalScene.Instantiate();
        
        float randomX = (float)GD.RandRange(-150, 150);
        float randomY = (float)GD.RandRange(-15, 15);
        zombie.GlobalPosition = busDoorPos + new Vector2(randomX, randomY);
        
        AddChild(zombie);

        // Összekötjük a zombi "megsemmisülését" a számlálónkkal
        // A TreeExited akkor fut le, amikor a zombi QueueFree()-vel törlődik
        zombie.TreeExited += OnTutorialZombieDied;
    }
}

private void OnTutorialZombieDied()
{
    _tutorialZombiesAlive--;
    GD.Print($"Zombi meghalt. Hátralévő: {_tutorialZombiesAlive}");

    if (_tutorialZombiesAlive <= 0)
    {
        GD.Print("Minden tutorial zombi legyőzve! Jöhet a kulcsdarab.");
        SpawnFirstKeyPart();
    }
}

private void SpawnFirstKeyPart()
{
    if (TutorialKeyPartScene == null)
    {
        GD.PrintErr("HIBA: Nincs behúzva a TutorialKeyPartScene a World-ön!");
        return;
    }

    // Létrehozzuk a tárgyat
    var keyPart = (Area2D)TutorialKeyPartScene.Instantiate();
    
    // Oda rakjuk, ahol a buszmegálló van (hogy a játékos megtalálja)
    keyPart.GlobalPosition = new Vector2(-463f, 493f);
    
    // Beállítjuk a nevét, amit a TutorialItem.cs felismer
    if (keyPart is TutorialItem itemScript)
    {
        itemScript.ItemName = "KeyPart1";
    }

    AddChild(keyPart);
    GD.Print("Az első kulcsdarab megjelent a földön!");

    // 3. Frissítsük a küldetést
    if (_questLabel != null)
    {
        _questLabel.Text = "Küldetés: Vedd fel a kulcsdarabot a földről!";
    }
}

public void OnKeyPartCollected(string name)
{
    if (name == "KeyPart1")
    {
        _questLabel.Text = "Küldetés: Menj a parkolóhoz a következő darabért!";
        // Itt indíthatnánk majd a 2. hullámot a parkoló felől...
    }
}

    private void OnArrivalFinished(StringName animName)
    {
        if (animName == "Arrival" && _player != null)
        {
            _player.Visible = true;
            _player.ProcessMode = ProcessModeEnum.Always;
        }
    }

    private void SpawnRandomTraffic()
    {
        if (VehiclePrefabs == null || VehiclePrefabs.Length == 0) return;

        int index = GD.RandRange(0, VehiclePrefabs.Length - 1);
        var vehicle = (Node2D)VehiclePrefabs[index].Instantiate();

        bool fromRight = GD.Randf() > 0.5f;
        float baseYPos = 850; 
        float yPos = fromRight ? baseYPos : baseYPos + 100.0f;
        float startX = fromRight ? 3200 : -900; 
        float targetX = fromRight ? -900 : 3200;

        vehicle.Position = new Vector2(startX, yPos);
        AddChild(vehicle);
        vehicle.ZIndex = fromRight ? 5 : 6;
        
        var tween = CreateTween();
        tween.TweenProperty(vehicle, "position:x", targetX, 8.0f);
        tween.Finished += () => vehicle.QueueFree();

        if (!fromRight && vehicle.HasNode("Sprite2D"))
        {
            vehicle.GetNode<Sprite2D>("Sprite2D").FlipH = true;
        }
    }

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