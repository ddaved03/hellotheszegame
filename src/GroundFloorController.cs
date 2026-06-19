using Godot;
using System;
using System.Collections.Generic;

public partial class GroundFloorController : Node2D
{
    [Export] public NodePath PlayerPath;
    [Export] public NodePath PauseMenuPath;
    [Export] public NodePath QuestLabelPath;
    [Export] public NodePath AnimationPlayerPath;
    // --- JÁTÉKOS, HUD ÉS EGYÉB FONTOS NODE-OK ---
    private BasePlayer _player;
    private Control _pauseMenu;
    private PauseLoadMenu _pauseLoadMenu;
    private Label _questLabel;
    private ColorRect _darknessOverlay;
    private ShaderMaterial _darknessMaterial;
    private AnimationPlayer _sceneAnimPlayer;
    // --- FÖLDSZINTI ESEMÉNYEK, ELLENSÉGEK ÉS ÁLLAPOTKÖVETÉS ---
    private PackedScene _zombieNormalScene;
    private PackedScene _zombieSmallScene;
    private PackedScene _zombieBigScene;

    private int _spawnsWithoutAssignment = 0;
    private readonly List<RoomPortal> _roomPortals = new List<RoomPortal>();
    private RoomPortal _activeRoomPortal;
    private string _nearbyRoomDoorId;
    // Ezek a változók segítenek nyomon követni, hogy a földrengés esemény már megtörtént-e, hogy a liftet megtalálták-e, és hogy a játékos közel van-e a lifthez.
    private bool _earthquakeTriggered = false;
    private bool _elevatorFound = false;
    private Vector2 _elevatorPosition = Vector2.Zero;
    private const float ElevatorDetectionRadius = 200f;
    private bool _earthquakeSequenceRunning = false;
    private bool _flashlightPickupSpawned = false;
    private Area2D _flashlightPickup;
    private bool _elevatorTransitionRunning = false;
    // --- EGYÉB SEGÉDFÜGGVÉNYEK ÉS OSZTÁLYOK ---
    private sealed class RoomPortal
    {
        public string Name;
        public Vector2 OutsidePosition;
        public Vector2 InsidePosition;
        public Rect2 RoomBounds;
        public int SmallZombieCount;
        public string RewardItemName;
        public string RewardTexturePath;
        public bool IsInside;
        public bool ZombiesSpawned;
        public bool RewardSpawned;
        public bool Cleared;
        public int AliveZombies;
    }
    public override void _Ready()
    {
        GD.Print("\n--- FÖLDSZINT KONTROLLER INDUL ---");

        if (PlayerPath != null) _player = GetNodeOrNull<BasePlayer>(PlayerPath);
        if (QuestLabelPath != null) _questLabel = GetNodeOrNull<Label>(QuestLabelPath);
        _darknessOverlay = GetNodeOrNull<ColorRect>("CanvasLayer/DarknessOverlay");

        // DEBUG: Ellenőrizd, hogy a DarknessOverlay létezik-e
        if (_darknessOverlay != null)
        {
            GD.Print("[GroundFloor] SIKER: DarknessOverlay megtalálva!");
        }
        else
        {
            GD.PrintErr("[GroundFloor] HIBA: DarknessOverlay nem található! Alternatív hely ellenőrzése...");

            _darknessOverlay = GetNodeOrNull<ColorRect>("DarknessOverlay");

            if (_darknessOverlay != null)
            {
                GD.Print("[GroundFloor] SIKER: DarknessOverlay megtalálva a gyökérben.");
            }
        }

        _zombieNormalScene = GD.Load<PackedScene>("res://scenes/Zombie.tscn");
        _zombieSmallScene = GD.Load<PackedScene>("res://scenes/ZombieSmall.tscn");
        _zombieBigScene = GD.Load<PackedScene>("res://scenes/ZombieBig.tscn");

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
                    CreatePauseLoadMenu();

                    var pauseInput = new PauseInputHandler();
                    AddChild(pauseInput);
                    pauseInput.PausePressed += OnPausePressed;
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
            // Fontos: a DarknessOverlay-t át kell helyezni egy CanvasLayer alá, hogy mindig a játék mögött legyen, de a HUD előtt.
            var sceneCanvas = GetNodeOrNull<CanvasLayer>("CanvasLayer");
            var oldParent = _darknessOverlay.GetParent();
            if (oldParent != null && oldParent != sceneCanvas && sceneCanvas != null)
            {
                try { oldParent.RemoveChild(_darknessOverlay); } catch { }
                sceneCanvas.AddChild(_darknessOverlay);
            }
            if (sceneCanvas != null)
            {
                try { sceneCanvas.Layer = 0; } catch { }
            }

            _darknessOverlay.Visible = false;
            _darknessOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            _darknessOverlay.Color = new Color(0f, 0f, 0f, 0f);
            SetupDarknessOverlayShader();
        }

        // Fontos: a HUD-nak is egy CanvasLayer alatt kell lennie, hogy a sötétség mögé kerüljön, de a játék elé
        var hudControl = GetNodeOrNull<Control>("CanvasLayer/Control");
        if (hudControl != null)
        {
            CanvasLayer hudLayer = GetNodeOrNull<CanvasLayer>("HUDLayer");
            if (hudLayer == null)
            {
                hudLayer = new CanvasLayer();
                hudLayer.Name = "HUDLayer";
                hudLayer.Layer = 1; // Ez biztosítja, hogy a HUD a DarknessOverlay fölött legyen
                AddChild(hudLayer);
            }

            var hudParent = hudControl.GetParent();
            if (hudParent != null && hudParent != hudLayer)
            {
                try { hudParent.RemoveChild(hudControl); } catch { }
                hudLayer.AddChild(hudControl);
                GD.Print("SIKER: HUD átmozgatva a HUDLayer CanvasLayer alá!");
            }
        }

        SetupRoomPortals();

        if (AnimationPlayerPath != null)
        {
            _sceneAnimPlayer = GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath);
            if (_sceneAnimPlayer != null) GD.Print("SIKER: AnimationPlayer hozzárendelve az inspectorból.");
        }

        SpawnFlashlightPickupIfNeeded();

        var elevatorTriggerNode = GetNodeOrNull<Area2D>("ElevatorDoor/DetectionArea");
        if (elevatorTriggerNode != null)
        {
            _elevatorPosition = elevatorTriggerNode.GlobalPosition;
        }
        else if (_player != null)
        {
            _elevatorPosition = _player.GlobalPosition + new Vector2(300, 0);
        }

        // Földrengés trigger bekötése
        var quakeArea = GetNodeOrNull<Area2D>("EarthquakeTrigger");
        if (quakeArea != null)
        {
            quakeArea.BodyEntered += OnEarthquakeTriggerBodyEntered;
            GD.Print("SIKER: Földrengés aktiváló terület csatlakoztatva.");
        }

        // Kezdeti küldetés beállítása a földszinten
        InitQuestState();

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
        SaveSystem.GroundFloorStateData state = SaveSystem.LastLoadedData?.GroundFloorState;
        if (state == null)
        {
            bool earthquakeAlreadyHappened =
                InventoryManager.Items.Contains("Fuse") ||
                InventoryManager.Items.Contains("Cable");

            if (earthquakeAlreadyHappened && _darknessOverlay != null)
            {
                _earthquakeTriggered = true;
                _darknessOverlay.Visible = true;
                _darknessOverlay.Color = new Color(0f, 0f, 0f, 0.88f);
            }

            if (InventoryManager.Items.Contains("Flashlight"))
            {
                RemoveSpawnedFlashlightPickup();
                _player?.EquipFlashlight();
            }

            SpawnFlashlightPickupIfNeeded();
            InitQuestState();
            UpdateDarknessFocus();
            return;
        }

        _earthquakeTriggered = state.EarthquakeTriggered;
        _elevatorFound = state.ElevatorFound;
        _questInitialized = state.QuestInitialized;

        if (_darknessOverlay != null)
        {
            _darknessOverlay.Visible = state.DarknessVisible;
            _darknessOverlay.Color = new Color(0f, 0f, 0f, state.DarknessAlpha);
        }

        if (InventoryManager.Items.Contains("Flashlight"))
        {
            _flashlightPickupSpawned = true;
            RemoveSpawnedFlashlightPickup();
            _player?.EquipFlashlight();
        }
        else
        {
            _flashlightPickupSpawned = false;
            SpawnFlashlightPickupIfNeeded();
        }

        if (state.Rooms != null)
        {
            foreach (SaveSystem.RoomStateData roomState in state.Rooms)
            {
                RoomPortal portal = GetPortalByName(roomState.Name);
                if (portal == null) continue;

                portal.IsInside = roomState.IsInside;
                portal.RewardSpawned = roomState.RewardSpawned;
                portal.Cleared = roomState.Cleared;
                portal.AliveZombies = 0;

                if (roomState.ZombiesSpawned && !roomState.Cleared && roomState.AliveZombies > 0)
                {
                    SpawnRoomZombies(portal, roomState.AliveZombies);
                }
                else
                {
                    portal.ZombiesSpawned = roomState.ZombiesSpawned;
                }

                if (roomState.RewardSpawned &&
                    portal.RewardItemName != null &&
                    !InventoryManager.Items.Contains(portal.RewardItemName))
                {
                    SpawnRoomReward(portal, portal.InsidePosition);
                }
            }
        }

        if (_questLabel != null && !string.IsNullOrEmpty(state.QuestText))
        {
            _questLabel.Text = state.QuestText;
        }

        UpdateDarknessFocus();
    }

    private SaveSystem.GroundFloorStateData CaptureGroundFloorState()
    {
        var state = new SaveSystem.GroundFloorStateData
        {
            DarknessVisible = _darknessOverlay?.Visible ?? false,
            DarknessAlpha = _darknessOverlay?.Color.A ?? 0f,
            EarthquakeTriggered = _earthquakeTriggered,
            ElevatorFound = _elevatorFound,
            FlashlightPickupSpawned = _flashlightPickupSpawned,
            QuestInitialized = _questInitialized,
            QuestText = _questLabel?.Text
        };

        foreach (RoomPortal portal in _roomPortals)
        {
            state.Rooms.Add(new SaveSystem.RoomStateData
            {
                Name = portal.Name,
                IsInside = portal.IsInside,
                ZombiesSpawned = portal.ZombiesSpawned,
                RewardSpawned = portal.RewardSpawned,
                Cleared = portal.Cleared,
                AliveZombies = portal.AliveZombies
            });
        }

        return state;
    }
    // --- FÖLDSZINTI ESEMÉNYEK, KÜLDETÉSEK ÉS ÁLLAPOTKÖVETÉS FOLYTATÁSA ---
    private void SpawnFlashlightPickupIfNeeded()
    {
        if (_flashlightPickupSpawned) return;
        if (InventoryManager.Items.Contains("Flashlight")) return;

        var flashScene = GD.Load<PackedScene>("res://scenes/FlashlightPickup.tscn");
        if (flashScene == null) return;

        var flashInst = (Area2D)flashScene.Instantiate();
        if (flashInst is TutorialItem ti) ti.ItemName = "Flashlight";

        flashInst.GlobalPosition = _player != null ? _player.GlobalPosition + new Vector2(48f, 0f) : new Vector2(360f, 180f);
        flashInst.Scale = new Vector2(0.35f, 0.35f);
        flashInst.ZIndex = 200;
        AddChild(flashInst);
        _flashlightPickup = flashInst;
        _flashlightPickupSpawned = true;
    }

    private void RemoveSpawnedFlashlightPickup()
    {
        if (_flashlightPickup != null && IsInstanceValid(_flashlightPickup))
        {
            _flashlightPickup.QueueFree();
        }

        _flashlightPickup = null;
    }

    public void UpdateQuestText(string text)
    {
        if (_questLabel != null) _questLabel.Text = text;
    }

    private bool _questInitialized = false;

    public void InitQuestState()
    {
        if (!_questInitialized)
        {
            UpdateQuestText("Küldetés: Keresd meg a liftet!");
            _questInitialized = true;
        }
    }

    private void UpdateQuestState()
    {
        // Ha nincs még inicializálva, akkor inicializáljuk
        if (!_questInitialized)
        {
            InitQuestState();
            return;
        }

        if (_player == null) return;

        // Ellenőrizd, hogy a játékos közel van-e a lifthez
        float distanceToElevator = _player.GlobalPosition.DistanceTo(_elevatorPosition);
        bool playerNearElevator = distanceToElevator < ElevatorDetectionRadius;

        bool hasCable = InventoryManager.Items.Contains("Cable");
        bool hasFuse = InventoryManager.Items.Contains("Fuse");

        // Ha a játékos még nem találta meg a liftet
        if (!_elevatorFound)
        {
            if (playerNearElevator)
            {
                _elevatorFound = true;  // Megtalálta a liftet
            }
            else
            {
                // Még mindig keres a liftet
                if (_questLabel != null && _questLabel.Text != "Küldetés: Keresd meg a liftet!")
                {
                    UpdateQuestText("Küldetés: Keresd meg a liftet!");
                }
                return;
            }
        }

        // Ha már megtalálta a liftet
        if (_elevatorFound)
        {
            if (!hasCable || !hasFuse)
            {
                // Hiányzik valami
                if (_questLabel != null && !_questLabel.Text.Contains("Elromlott"))
                {
                    UpdateQuestText("Küldetés: Elromlott a lift! Keress valamit a megjavításához!");
                }
            }
            else
            {
                // Van minden, használható a lift
                if (_questLabel != null && !_questLabel.Text.Contains("Tudsz"))
                {
                    UpdateQuestText("Küldetés: Most már tudod használni a liftet a következő szintre!");
                }
            }
        }
    }

    // --- LIFT HASZNÁLATA ÉS FÖLDSZINTI ESEMÉNYEK FOLYTATÁSA ---
    public void TryUseElevator()
    {
        if (_elevatorTransitionRunning) return;

        bool hasCable = InventoryManager.Items.Contains("Cable");
        bool hasFuse = InventoryManager.Items.Contains("Fuse");

        if (!hasCable || !hasFuse)
        {
            UpdateQuestText("Küldetés: A lift elromlott! Keress valamit a megjavításához!");
            GD.Print("[GroundFloor] Lift még nem használható: hiányzik a Cable vagy a Fuse.");
            return;
        }

        _elevatorTransitionRunning = true;
        UpdateQuestText("Küldetés: A lift működik. Haladás a következő szintre...");
        GD.Print("[GroundFloor] Lift használható, átváltok a következő szintre.");
        CallDeferred(nameof(GoToNextLevel));
    }

    private void GoToNextLevel()
    {
        GetTree().ChangeSceneToFile("res://scenes/C100.tscn");
    }
    // --- FÖLDSZINTI ESEMÉNYEK, KÜLDETÉSEK ÉS ÁLLAPOTKÖVETÉS FOLYTATÁSA VÉGE ---
    private async void StartEarthquakeSequence()
    {
        if (_earthquakeSequenceRunning) return;
        _earthquakeSequenceRunning = true;

        Camera2D camera = _player != null ? _player.GetNodeOrNull<Camera2D>("Camera2D") : null;
        Vector2? originalCameraOffset = camera != null ? camera.Offset : null;
        AudioManager.Instance?.PlayEarthquake(_player != null ? _player.GlobalPosition : GlobalPosition);

        // Először sötétítsük el a képernyőt, hogy a játékos tudja, hogy valami nagy dolog történik. 
        {
            _darknessOverlay.Visible = true;
            _darknessOverlay.Color = new Color(0f, 0f, 0f, 0.88f);
        }

        // Ha a játékosnak van lámpája, kapcsoljuk fel, hogy lásson a sötétben, és hogy a lámpa fénye azonnal megjelenjen a sötétség shader alatt.
        if (_player != null && InventoryManager.Items.Contains("Flashlight"))
        {
            _player.EquipFlashlight();
        }

        UpdateDarknessFocus();

        if (camera != null)
        {
            ShakeCamera(camera, originalCameraOffset ?? Vector2.Zero);
        }

        // Várjunk egy kicsit a rengés után, hogy a játékos feldolgozhassa a helyzetet, mielőtt a kövek elkezdenek hullani.
        var rockScene = GD.Load<PackedScene>("res://scenes/Rock.tscn");
        if (rockScene == null)
        {
            GD.PrintErr("Nem találom a Rock.tscn scene-t — nem spawnolok köveket.");
            await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
            GetTree().ChangeSceneToFile("res://scenes/C100.tscn");
            return;
        }

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

        int cols = 18;
        int rows = 6;
        float cellW = (right - left) / (cols - 1);
        float cellH = 40f;

        float corridorHalf = 64f;

        var rockStaticPacked = GD.Load<PackedScene>("res://scenes/RockStatic.tscn");

        var landingRoot = GetNodeOrNull<Node2D>("RockLandingPoints");

        // Először próbáljunk meg marker alapján spawnolni, ha vannak ilyenek
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
                tween.Finished += () =>
       {
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
            for (int c = 0; c < cols; c++)
            {
                float x = left + c * cellW;

                float pathY = elevatorPos.Y;

                for (int r = 0; r < rows; r++)
                {
                    float y = top + r * cellH;

                    if (Mathf.Abs(y - pathY) <= corridorHalf)
                        continue;

                    var rock = (RigidBody2D)rockScene.Instantiate();

                    Vector2 targetPos = new Vector2(
                        x + (float)GD.RandRange(-6, 6),
                        y + (float)GD.RandRange(-6, 6)
                    );
                    float spawnAbove = (float)GD.RandRange(160, 280);

                    Vector2 spawnPos = targetPos + new Vector2(0, -spawnAbove);

                    rock.GlobalPosition = spawnPos;

                    AddChild(rock);

                    var fallTime = (float)GD.RandRange(0.45, 0.95);

                    var tween = CreateTween();

                    tween.TweenProperty(rock, "global_position", targetPos, fallTime)
                         .SetTrans(Tween.TransitionType.Quad)
                         .SetEase(Tween.EaseType.In);
                    tween.Finished += () =>
            {
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

        // Várunk egy rövid ideig, hogy a kövek leessenek és a rengés befejeződjön
        await ToSignal(GetTree().CreateTimer(2.0f), "timeout");

        // Keep the player on GroundFloor; the earthquake only creates the blackout and obstacle event.
        if (camera != null)
        {
            camera.Offset = originalCameraOffset ?? Vector2.Zero;
        }
        _earthquakeSequenceRunning = false;
        return;
    }
    // Ez a függvény felelős a kamera megrázásáért a földrengés alatt, hogy még intenzívebbé tegye az élményt.
    private async void ShakeCamera(Camera2D camera, Vector2 baseOffset)
    {
        if (camera == null) return;

        const int shakeFrames = 24;
        const float shakeStrength = 8.0f;

        for (int i = 0; i < shakeFrames; i++)
        {
            if (!IsInstanceValid(camera)) return;

            float offsetX = (float)GD.RandRange(-shakeStrength, shakeStrength);
            float offsetY = (float)GD.RandRange(-shakeStrength, shakeStrength);
            camera.Offset = baseOffset + new Vector2(offsetX, offsetY);
            await ToSignal(GetTree().CreateTimer(0.04f), "timeout");
        }

        if (IsInstanceValid(camera))
        {
            camera.Offset = baseOffset;
        }
    }

    // --- PAUSE MENÜ GOMBOK ÉS BILLENTYŰZET ---
    public override void _Input(InputEvent @event)
    {
        if (GetTree().Paused)
        {
            return;
        }

        // Interakció ajtókkal és lifttel
        if (@event.IsActionPressed("interact"))
        {
            GD.Print("[GroundFloor] Interact gomb megnyomva! _elevatorPosition=" + _elevatorPosition);

            // először ellenőrizzük, hogy a játékos közel van-e a lifthez,
            if (_player != null)
            {
                float distToElevator = _player.GlobalPosition.DistanceTo(_elevatorPosition);
                GD.Print($"[GroundFloor] Játékos pozíciója: {_player.GlobalPosition}, Elevator: {_elevatorPosition}, Távolság: {distToElevator}");

                if (distToElevator < 150f)
                {
                    GD.Print("[GroundFloor] Lift-hez közel - próbálom használni");
                    TryUseElevator();
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }

            // Ajtók nyitása
            if (TryUseRoomPortal())
            {
                GD.Print("[GroundFloor] Ajtó nyitása sikeres!");
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    private void OnPausePressed()
    {
        GD.Print("ESC gomb megnyomva!");

        if (_pauseMenu == null)
        {
            GD.PrintErr("Nem tudom kinyitni a menüt, mert a _pauseMenu nulla! (Nézd meg az Inspectort!)");
            return;
        }

        if (_pauseLoadMenu != null && _pauseLoadMenu.Visible)
        {
            _pauseLoadMenu.Close();
            return;
        }

        bool openPauseMenu = !GetTree().Paused;
        if (openPauseMenu)
        {
            _pauseMenu.Visible = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            GetTree().Paused = true;
        }
        else if (_pauseMenu.Visible)
        {
            OnResumePressed();
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
        if (_player != null) SaveSystem.Save(_player, groundFloorState: CaptureGroundFloorState());
    }

    private void OnLoadPressed()
    {
        AudioManager.Instance?.PlayUiClick();
        if (_pauseLoadMenu == null) return;

        _pauseMenu.Visible = false;
        _pauseLoadMenu.Open();
    }

    private void CreatePauseLoadMenu()
    {
        var loadMenuLayer = new CanvasLayer
        {
            Name = "PauseLoadMenuLayer",
            Layer = 100,
            ProcessMode = ProcessModeEnum.WhenPaused
        };
        AddChild(loadMenuLayer);

        _pauseLoadMenu = new PauseLoadMenu { Name = "PauseLoadMenu" };
        loadMenuLayer.AddChild(_pauseLoadMenu);
        _pauseLoadMenu.Setup(LoadSelectedSave, () => _pauseMenu.Visible = true);
    }

    private void LoadSelectedSave(string fileName)
    {
        SaveSystem.CurrentSaveFileName = fileName;
        string targetScene = SaveSystem.GetSavedScenePath(fileName);
        SaveSystem.LoadRequested = true;
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(targetScene);
    }

    private void OnMainMenuPressed() { OnResumePressed(); GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn"); }

    public override void _Process(double delta)
    {
        if (GetTree().Paused)
        {
            return;
        }

        UpdateDarknessFocus();
        CheckEarthquakeApproach();
        UpdateQuestState();
    }

    private void SetupRoomPortals()
    {
        _roomPortals.Clear();

        if (TryReadPortalMarkersFromScene())
        {
            return;
        }

        _roomPortals.Add(new RoomPortal
        {
            Name = "TopRoom",
            OutsidePosition = new Vector2(1510f, 210f),
            InsidePosition = new Vector2(1510f, 110f),
            RoomBounds = new Rect2(1180f, 20f, 1400f, 250f),
            SmallZombieCount = 4
        });

        _roomPortals.Add(new RoomPortal
        {
            Name = "BottomLeftRoom",
            OutsidePosition = new Vector2(640f, 1400f),
            InsidePosition = new Vector2(640f, 1510f),
            RoomBounds = new Rect2(240f, 1360f, 1000f, 290f),
            SmallZombieCount = 5,
            RewardItemName = "Fuse",
            RewardTexturePath = "res://kepek/fuse.png"
        });

        _roomPortals.Add(new RoomPortal
        {
            Name = "BottomRightRoom",
            OutsidePosition = new Vector2(2190f, 1400f),
            InsidePosition = new Vector2(2190f, 1510f),
            RoomBounds = new Rect2(1450f, 1360f, 1180f, 290f),
            SmallZombieCount = 5,
            RewardItemName = "Cable",
            RewardTexturePath = "res://kepek/cable.png"
        });
    }

    private RoomPortal GetPortalByName(string portalName)
    {
        foreach (var portal in _roomPortals)
        {
            if (portal.Name == portalName) return portal;
        }

        return null;
    }

    public void NotifyRoomDoorEntered(string doorId, Vector2 doorPosition)
    {
        _nearbyRoomDoorId = doorId;
        var portal = GetPortalByName(doorId);
        if (portal != null)
        {
            _activeRoomPortal = portal;
        }
    }

    public void NotifyRoomDoorExited(string doorId)
    {
        if (_nearbyRoomDoorId == doorId)
        {
            _nearbyRoomDoorId = null;
        }
    }

    private bool TryReadPortalMarkersFromScene()
    {
        var markerRoot = GetNodeOrNull<Node2D>("RoomPortals");
        if (markerRoot == null) return false;

        foreach (var child in markerRoot.GetChildren())
        {
            if (child is not Marker2D marker) continue;

            switch (marker.Name)
            {
                case "TopRoomOutside":
                    AddPortalFromMarkerSet("TopRoom", markerRoot, "TopRoomOutside", "TopRoomInside", null, null, 4);
                    break;
                case "BottomLeftOutside":
                    AddPortalFromMarkerSet("BottomLeftRoom", markerRoot, "BottomLeftOutside", "BottomLeftInside", "Fuse", "res://kepek/fuse.png", 5);
                    break;
                case "BottomRightOutside":
                    AddPortalFromMarkerSet("BottomRightRoom", markerRoot, "BottomRightOutside", "BottomRightInside", "Cable", "res://kepek/cable.png", 5);
                    break;
            }
        }

        return _roomPortals.Count > 0;
    }

    private void AddPortalFromMarkerSet(string portalName, Node2D markerRoot, string outsideName, string insideName, string rewardItem, string rewardTexture, int smallZombieCount)
    {
        var outsideMarker = markerRoot.GetNodeOrNull<Marker2D>(outsideName);
        var insideMarker = markerRoot.GetNodeOrNull<Marker2D>(insideName);
        if (outsideMarker == null || insideMarker == null) return;

        _roomPortals.Add(new RoomPortal
        {
            Name = portalName,
            OutsidePosition = outsideMarker.GlobalPosition,
            InsidePosition = insideMarker.GlobalPosition,
            RoomBounds = new Rect2(Mathf.Min(outsideMarker.GlobalPosition.X, insideMarker.GlobalPosition.X) - 350f, Mathf.Min(outsideMarker.GlobalPosition.Y, insideMarker.GlobalPosition.Y) - 200f, 900f, 320f),
            SmallZombieCount = smallZombieCount,
            RewardItemName = rewardItem,
            RewardTexturePath = rewardTexture
        });
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

    public void UpdateDarknessFocus()
    {
        if (_darknessOverlay == null || _darknessMaterial == null || _player == null) return;
        Vector2 focusPos = GetViewport().GetCanvasTransform() * (_player.GlobalPosition + new Vector2(0, -10));
        _darknessMaterial.SetShaderParameter("focus_pos", focusPos);
        // Ha a játékosnál van lámpa, akkor egy kis körben láthatóvá tesszük a környezetet
        // Egyébként teljes sötétség marad
        var hasLightNode = _player.GetNodeOrNull<PointLight2D>("FlashlightLight") != null;
        if (hasLightNode)
        {
            // A látható kör méretének beállítása a zseblámpáhozv
            _darknessMaterial.SetShaderParameter("hole_radius", 90.0f);
            _darknessMaterial.SetShaderParameter("softness", 50.0f);
        }
        else
        {
            _darknessMaterial.SetShaderParameter("hole_radius", 0.0f);
            _darknessMaterial.SetShaderParameter("softness", 1.0f);
        }
    }

    private void CheckEarthquakeApproach()
    {
        if (_earthquakeTriggered || _earthquakeSequenceRunning) return;
        if (_player == null) return;

        foreach (var portal in _roomPortals)
        {
            // Csak akkor induljon el a földrengés, ha a játékos beljebb ment a szobábav
            float distanceFromDoor = _player.GlobalPosition.DistanceTo(portal.OutsidePosition);
            if (portal.RoomBounds.HasPoint(_player.GlobalPosition) && distanceFromDoor > 140f)
            {
                _earthquakeTriggered = true;
                GD.Print($"[GroundFloor] Játékos beljebb ment a {portal.Name} terembe — indítom a sötétedést és a földrengést.");
                StartEarthquakeSequence();
                return;
            }
        }
    }

    private void OnEarthquakeTriggerBodyEntered(Node body)
    {
        if (_earthquakeTriggered || _earthquakeSequenceRunning) return;
        if (body is BasePlayer)
        {
            _earthquakeTriggered = true;
            GD.Print("Játékos belépett a rengés trigger területre — indítom a rengést.");
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

    private RoomPortal GetClosestRoomPortal(Vector2 position)
    {
        RoomPortal closest = null;
        float closestDistance = float.MaxValue;

        foreach (var portal in _roomPortals)
        {
            float distance = Mathf.Min(position.DistanceTo(portal.OutsidePosition), position.DistanceTo(portal.InsidePosition));
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = portal;
            }
        }

        return closest;
    }

    private bool TryUseRoomPortal()
    {
        if (_player == null || _roomPortals.Count == 0) return false;

        RoomPortal portal = null;
        if (!string.IsNullOrEmpty(_nearbyRoomDoorId))
        {
            portal = GetPortalByName(_nearbyRoomDoorId);
        }

        portal ??= _activeRoomPortal ?? GetClosestRoomPortal(_player.GlobalPosition);
        if (portal == null) return false;

        const float interactDistance = 120f;
        float outsideDistance = _player.GlobalPosition.DistanceTo(portal.OutsidePosition);
        float insideDistance = _player.GlobalPosition.DistanceTo(portal.InsidePosition);

        if (!portal.IsInside && outsideDistance <= interactDistance)
        {
            EnterRoom(portal);
            return true;
        }

        if (portal.IsInside && insideDistance <= interactDistance)
        {
            ExitRoom(portal);
            return true;
        }

        return false;
    }

    private void EnterRoom(RoomPortal portal)
    {
        portal.IsInside = true;

        if (_player != null)
        {
            _player.GlobalPosition = portal.InsidePosition;
        }

        if (!portal.ZombiesSpawned)
        {
            SpawnRoomZombies(portal, portal.SmallZombieCount);
        }
    }

    private void SpawnRoomZombies(RoomPortal portal, int zombieCount)
    {
        if (_zombieSmallScene == null || zombieCount <= 0) return;

        portal.ZombiesSpawned = true;
        for (int i = 0; i < zombieCount; i++)
        {
            var zombie = (Node2D)_zombieSmallScene.Instantiate();
            if (zombie == null) continue;

            Vector2 spawnPos = portal.InsidePosition + new Vector2(
                (float)GD.RandRange(-200, 200),
                (float)GD.RandRange(-100, 100));
            zombie.GlobalPosition = spawnPos;
            zombie.ZIndex = 50;
            AddChild(zombie);

            portal.AliveZombies++;
            zombie.TreeExited += () =>
            {
                portal.AliveZombies--;

                if (portal.AliveZombies <= 0 && !portal.RewardSpawned && portal.RewardItemName != null)
                {
                    portal.RewardSpawned = true;
                    SpawnRoomReward(portal, spawnPos);
                }

                if (portal.AliveZombies <= 0)
                {
                    portal.Cleared = true;
                    UpdateQuestState();
                }
            };
        }

        UpdateQuestState();
    }

    private void ExitRoom(RoomPortal portal)
    {
        portal.IsInside = false;

        if (_player != null)
        {
            _player.GlobalPosition = portal.OutsidePosition;
        }
    }

    private void SpawnRoomReward(RoomPortal portal, Vector2 pos)
    {
        if (portal.RewardItemName == null || portal.RewardTexturePath == null) return;

        var reward = (TutorialItem)GD.Load<PackedScene>("res://scenes/TutorialKeyPart.tscn").Instantiate();
        if (reward != null)
        {
            reward.ItemName = portal.RewardItemName;
            reward.GlobalPosition = pos;

            var sprite = reward.GetNodeOrNull<Sprite2D>("Sprite2D");
            if (sprite != null)
            {
                sprite.Texture = GD.Load<Texture2D>(portal.RewardTexturePath);
            }

            reward.Scale = new Vector2(0.25f, 0.25f);
            AddChild(reward);

            GD.Print($"[GroundFloor] Spawned reward: {portal.RewardItemName} at {pos}");
        }
    }
}
