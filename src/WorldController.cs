using Godot;
using System;
using System.Collections.Generic;

public partial class WorldController : Node2D
{
    [Export] public NodePath PlayerPath;
    [Export] public NodePath PauseMenuPath;
    
    [Export] public NodePath BusAnimationPath; 
    [Export] public PackedScene[] VehiclePrefabs; 
    [Export] public float TrafficSpawnInterval = 5.0f;

    // --- EXPORTÁLT VÁLTOZÓK A TUTORIALHOZ ---
    [Export] public PackedScene ZombieNormalScene; 
    [Export] public PackedScene ZombieSmallScene; // ÚJ: Kicsi zombi scene
    [Export] public PackedScene CarPrefab;         // ÚJ: Az autó sprite/prefabja
    [Export] public NodePath QuestLabelPath;      

    [Export] public PackedScene TutorialKeyPartScene;

    private BasePlayer _player;
    private Control _pauseMenu;
    private AnimationPlayer _busAnim;
    private Label _questLabel;
    private float _trafficTimer = 0;

    // Parkolós esemény változói
    private bool _parkingEventStarted = false;
    private int _parkingZombiesAlive = 0;
    private const int TotalParkingZombies = 15;

    // --- KÖVES (TÚLÉLŐ) ESEMÉNY VÁLTOZÓI ---
    private bool _rocksEventStarted = false;
    private bool _rocksTimerActive = false;
    private float _rocksTimeLeft = 60f; // 1 perc
    private float _rocksSpawnCooldown = 0f;
    private int _rocksZombiesAlive = 0;
    private Vector2 _rocksSpawnCenter;
    private bool _rocksEventCompleted = false;


    public override void _Ready()
    {
        if (PlayerPath != null) _player = GetNodeOrNull<BasePlayer>(PlayerPath);
        if (PauseMenuPath != null) _pauseMenu = GetNodeOrNull<Control>(PauseMenuPath);
        if (QuestLabelPath != null) _questLabel = GetNodeOrNull<Label>(QuestLabelPath);

        if (BusAnimationPath != null)
        {
            _busAnim = GetNodeOrNull<AnimationPlayer>(BusAnimationPath);
            if (_busAnim != null)
            {
                StartArrivalCutscene();
            }
        }

        // Parkoló Trigger keresése (feltételezve, hogy ParkingTrigger a neve a fában)
        var parkingTrigger = GetNodeOrNull<Area2D>("ParkingTrigger");
        if (parkingTrigger != null)
        {
            parkingTrigger.BodyEntered += OnParkingTriggerEntered;
        }

        var rocksTrigger = GetNodeOrNull<Area2D>("RocksTrigger");
        if (rocksTrigger != null) 
        {
            rocksTrigger.BodyEntered += OnRocksTriggerEntered;
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

        // Az egyetem ajtajának triggere (a képed alapján a UniversityDoor gyermeke)
        var doorTrigger = GetNodeOrNull<Area2D>("UniversityDoor/DetectionArea");
        if (doorTrigger != null) doorTrigger.BodyEntered += OnDoorTriggerEntered;
    }

    public override void _Process(double delta)
{
    if (GetTree().Paused) return;

    // Forgalom logika
    _trafficTimer += (float)delta;
    if (_trafficTimer >= TrafficSpawnInterval)
    {
        SpawnRandomTraffic();
        _trafficTimer = 0;
    }

    // --- ÚJ: KÖVES ESEMÉNY LOGIKÁJA ---
    if (_rocksTimerActive)
    {
        _rocksTimeLeft -= (float)delta; // Visszaszámlálás
        
        // Kiírjuk a képernyőre a maradék másodpercet (felkerekítve)
        if (_questLabel != null)
        {
            int seconds = Mathf.CeilToInt(_rocksTimeLeft);
            _questLabel.Text = $"Túlélsz: {seconds} másodperc...";
        }

        // Zombik folyamatos érkezése (pl. 1.5 másodpercenként)
        _rocksSpawnCooldown -= (float)delta;
        if (_rocksSpawnCooldown <= 0)
        {
            SpawnRocksZombie();
            _rocksSpawnCooldown = 1.5f; // <-- Módosíthatod, ha nehezebb/könnyebb kell
        }

        // Amikor lejárt az 1 perc (0 lett az idő):
        if (_rocksTimeLeft <= 0 && _rocksTimerActive)
        {
            _rocksTimerActive = false;
            if (_questLabel != null)
                _questLabel.Text = "Küldetés: Tisztítsd meg a területet az utolsó kulcsért!";
                
            // Ha véletlenül épp egyetlen zombi sem élne, azonnal eldobjuk
            if (_rocksZombiesAlive <= 0 && !_rocksEventCompleted)
            {
                _rocksEventCompleted = true; // Kész, lezárva!
                SpawnKeyPart(_rocksSpawnCenter, "KeyPart3");
            }
        }
    }
}

    // --- PARKOLÓ ESEMÉNY LOGIKA ---
    private void OnParkingTriggerEntered(Node2D body)
    {
        if (_parkingEventStarted) return;

        if (body is BasePlayer player)
        {
            // Csak akkor indul el, ha már nálunk van az első kulcs
            if (player.Inventory != null && InventoryManager.Items.Contains("KeyPart1"))
            {
                _parkingEventStarted = true;
                StartParkingSequence();
            }
        }
    }

    private async void StartParkingSequence()
    {
        if (_questLabel != null) _questLabel.Text = "Küldetés: Vigyázz! Erősítés érkezik a parkolóba!";

        // 3 autó érkezik egymás után
        for (int i = 0; i < 3; i++)
        {
            SpawnParkingCar(i);
            await ToSignal(GetTree().CreateTimer(0.8f), "timeout");
        }
    }

    private void SpawnParkingCar(int index)
{
    if (CarPrefab == null) return;

    var car = (Node2D)CarPrefab.Instantiate();
    AddChild(car);

    // Y-sorrend beállítása: ami lejjebb van a képernyőn, az takarja azt, ami feljebb van
    car.YSortEnabled = true;

    var trigger = GetNodeOrNull<Area2D>("ParkingTrigger");
    Vector2 centerPos = trigger != null ? trigger.GlobalPosition : _player.GlobalPosition;

    // BALRÓL JOBBRA:
    // Kezdőpont: centerPos-tól balra (-1200)
    Vector2 startPos = new Vector2(centerPos.X - 1200, centerPos.Y + 50 + (index * 110));
    
    // Célpont: centerPos környékén, kicsit eltolva
    Vector2 targetPos = new Vector2(centerPos.X - 200 + (index * 180), centerPos.Y + 50 + (index * 110));

    car.GlobalPosition = startPos;
    
    // Most NEM kell a FlipH = true, mert az autó alapból jobbra néz
    if (car.HasNode("Sprite2D")) 
    {
        car.GetNode<Sprite2D>("Sprite2D").FlipH = true; 
    }

    var tween = CreateTween();
    tween.TweenProperty(car, "global_position", targetPos, 2.0f)
         .SetTrans(Tween.TransitionType.Quad)
         .SetEase(Tween.EaseType.Out);
    
    tween.Finished += () => 
    {
        GD.Print($"Autó {index} megállt. Zombik lehívása ide: {targetPos}");
        SpawnZombiesFromCar(targetPos, index);
    };
}

// --- KÖVES ESEMÉNY FUNKCIÓI ---

private void OnRocksTriggerEntered(Node2D body)
{
    // Ha már elindult, VAGY már sikeresen befejeztük, ne csináljon semmit!
    if (_rocksEventStarted || _rocksEventCompleted) return;

    if (body is BasePlayer player)
    {
        // Csak akkor indul el, ha megvan a 2. kulcs, ÉS MÉG NINCS meg a 3.
        if (player.Inventory != null && 
            InventoryManager.Items.Contains("KeyPart2") && 
            !InventoryManager.Items.Contains("KeyPart3"))
        {
            _rocksEventStarted = true;
            _rocksTimerActive = true;
            _rocksTimeLeft = 60f; // Visszaszámláló kezdete
            
            var trigger = GetNodeOrNull<Area2D>("RocksTrigger");
            _rocksSpawnCenter = trigger != null ? trigger.GlobalPosition : player.GlobalPosition;
        }
    }
}

private void SpawnRocksZombie()
{
    if (ZombieNormalScene == null) return;

    var zombie = (Node2D)ZombieNormalScene.Instantiate();
    
    // Ide tedd vissza azokat az X és Y számokat, amiket korábban beállítottál és jól működtek!
    float offsetX = (float)GD.RandRange(-100, 900);
    float offsetY = (float)GD.RandRange(-700, 0);
    
    zombie.GlobalPosition = _rocksSpawnCenter + new Vector2(offsetX, offsetY);
    zombie.ZIndex = 50; 
    
    AddChild(zombie);
    _rocksZombiesAlive++;
    
    zombie.TreeExited += () => {
        _rocksZombiesAlive--;
        
        // HA lejárt az idő ÉS mindenki meghalt ÉS még nem dobtuk el a kulcsot
        if (!_rocksTimerActive && _rocksZombiesAlive <= 0 && !_rocksEventCompleted)
        {
            _rocksEventCompleted = true; // KÉSZ! Örökre lezárjuk az eseményt.
            GD.Print("Lejárt az idő, és mindenki meghalt! 3. Kulcs érkezik!");
            SpawnKeyPart(zombie.GlobalPosition, "KeyPart3");
        }
    };
}

private void SpawnZombiesFromCar(Vector2 pos, int carIndex)
{
    // Autónként 4 normál és 1 kicsi (összesen 15 zombi)
    for (int i = 0; i < 4; i++) SpawnSingleZombie(ZombieNormalScene, pos);
    
    // A kicsi zombi spawnolása (már nem kell az isKeyCarrier változó)
    SpawnSingleZombie(ZombieSmallScene, pos);
}

private void SpawnSingleZombie(PackedScene scene, Vector2 pos)
{
    if (scene == null) return;

    var zombie = (Node2D)scene.Instantiate();
    
    // A te jól beállított pozícióid:
    float offsetX = (float)GD.RandRange(-600, -400);
    float offsetY = -800f; 
    
    // A zombi abszolút pozíciója az autóhoz képest
    zombie.GlobalPosition = new Vector2(pos.X + offsetX, pos.Y + offsetY);
    zombie.ZIndex = 50; 
    
    AddChild(zombie);
    _parkingZombiesAlive++;
    
    zombie.TreeExited += () => {
        _parkingZombiesAlive--; // Egy meghalt, levonjuk
        
        GD.Print($"Parkolós zombi meghalt. Hátralévő: {_parkingZombiesAlive}");

        // JAVÍTÁS: Nincs "isLast" feltétel. Bármelyik is hal meg utoljára, dobja a kulcsot!
        if (_parkingZombiesAlive <= 0)
        {
            GD.Print("Minden parkolós zombi meghalt! Kulcs spawnolása...");
            SpawnKeyPart(zombie.GlobalPosition, "KeyPart2");
        }
    };
}

    private void SpawnKeyPart(Vector2 pos, string keyName)
{
    if (TutorialKeyPartScene == null) return;

    // Létrehozzuk a tárgyat (Area2D / TutorialItem)
    var key = (TutorialItem)TutorialKeyPartScene.Instantiate();
    key.ItemName = keyName;
    key.GlobalPosition = pos;

    // --- ÚJ RÉSZ: KICSÉRÉLJÜK A KÉPET A FÖLDÖN LÉVŐ TÁRGYON ---
    var sprite = key.GetNodeOrNull<Sprite2D>("Sprite2D");
    if (sprite != null)
    {
        if (keyName == "KeyPart2")
        {
            // FONTOS: Ide ugyanazt az elérési utat másold be, amit az InventoryManager.cs-ben is 
            // használtál a KeyPart2 képéhez! (pl. "res://scenes/masodik_kulcs.png")
            sprite.Texture = GD.Load<Texture2D>("res://kepek/kulcs-kozepe-torott.png"); 
        }
        else if (keyName == "KeyPart3")
        {
            sprite.Texture = GD.Load<Texture2D>("res://kepek/kulcs-eleje-torott.png");
        }
    }

    AddChild(key);

    // Küldetés szövegének frissítése
    if (_questLabel != null) 
    {
        _questLabel.Text = "Küldetés: Vedd fel a második kulcsdarabot!";
    }
}

    // --- EREDETI FUNKCIÓK ---
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

    public async void StartZombieBusEvent()
    {
        if (_questLabel != null) _questLabel.Text = "Küldetés: Szerezd meg a kulcsot a buszról!";

        Camera2D camera = _player.GetNodeOrNull<Camera2D>("Camera2D");
        Node2D busNode = GetNodeOrNull<Node2D>("Sprite2D busz");

        if (camera != null && busNode != null)
        {
            Vector2 cameraGlobalPos = camera.GlobalPosition;
            _player.RemoveChild(camera);
            AddChild(camera);
            camera.GlobalPosition = cameraGlobalPos;

            await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
            _busAnim.Play("ZombieBusArrival");

            float timer = 0;
            float duration = 8.5f; 
            while (timer < duration)
            {
                camera.GlobalPosition = camera.GlobalPosition.Lerp(busNode.GlobalPosition, 0.1f);
                await ToSignal(GetTree(), "process_frame");
                timer += (float)GetProcessDeltaTime();
            }
        }

        SpawnTenZombies();
        await ToSignal(GetTree().CreateTimer(3.0f), "timeout");

        if (camera != null)
        {
            var tweenBack = CreateTween();
            tweenBack.TweenProperty(camera, "global_position", _player.GlobalPosition, 1.5f).SetTrans(Tween.TransitionType.Cubic);
            await ToSignal(tweenBack, "finished");

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
        _tutorialZombiesAlive = 10;

        for (int i = 0; i < 10; i++)
        {
            var zombie = (Node2D)ZombieNormalScene.Instantiate();
            float randomX = (float)GD.RandRange(-150, 150);
            float randomY = (float)GD.RandRange(-15, 15);
            zombie.GlobalPosition = busDoorPos + new Vector2(randomX, randomY);
            AddChild(zombie);
            zombie.TreeExited += OnTutorialZombieDied;
        }
    }

    private void OnTutorialZombieDied()
    {
        _tutorialZombiesAlive--;
        if (_tutorialZombiesAlive <= 0) SpawnFirstKeyPart();
    }

    private void SpawnFirstKeyPart()
    {
        if (TutorialKeyPartScene == null) return;
        var keyPart = (Area2D)TutorialKeyPartScene.Instantiate();
        keyPart.GlobalPosition = new Vector2(-463f, 493f);
        if (keyPart is TutorialItem itemScript) itemScript.ItemName = "KeyPart1";
        AddChild(keyPart);
        if (_questLabel != null) _questLabel.Text = "Küldetés: Vedd fel a kulcsdarabot a földről!";
    }

    public void OnKeyPartCollected(string name)
{
    if (name == "KeyPart1")
    {
        if (_questLabel != null) 
            _questLabel.Text = "Küldetés: Menj a parkolóhoz a következő darabért!";
    }
    else if (name == "KeyPart2")
    {
        if (_questLabel != null) 
            _questLabel.Text = "Küldetés: Keresd a köveknél az utolsó kulcsdarabot!";
       
    }
    else if (name == "KeyPart3")
    {
        // ÚJ: A harmadik kulcs felvételekor
        if (_questLabel != null) _questLabel.Text = "Küldetés: Javítsd meg a kulcsot!";
    }

}

    public void UpdateQuestText(string text)
{
    if (_questLabel != null) _questLabel.Text = text;
}

private void OnDoorTriggerEntered(Node2D body)
{
    if (body is BasePlayer player)
    {
        // Ha már megvan az összerakott kulcs
        if (player.Inventory != null && InventoryManager.Items.Contains("UniversityKey"))
        {
            var door = GetNodeOrNull<StaticBody2D>("UniversityDoor");
            if (door != null)
            {
                // Eltüntetjük az ajtót (kinyílik)
                door.QueueFree(); 
                
                GD.Print("Egyetem ajtaja kinyitva!");
                UpdateQuestText("Küldetés: Lépj be az egyetemre!");
            }
        }
        // Ha csak a darabok vannak meg
        else if (player.Inventory != null && InventoryManager.Items.Contains("KeyPart3") && !InventoryManager.Items.Contains("UniversityKey"))
        {
            UpdateQuestText("Küldetés: Előbb rakd össze a kulcsot az Inventory-ban!");
        }
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
        var tween = CreateTween();
        tween.TweenProperty(vehicle, "position:x", targetX, 8.0f);
        tween.Finished += () => vehicle.QueueFree();
        if (!fromRight && vehicle.HasNode("Sprite2D")) vehicle.GetNode<Sprite2D>("Sprite2D").FlipH = true;
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
            else if (_pauseMenu.Visible) OnResumePressed();
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
    private void OnLoadPressed() { if (_player != null) SaveSystem.Load(_player); OnResumePressed(); }
    private void OnMainMenuPressed() { OnResumePressed(); GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn"); }
}