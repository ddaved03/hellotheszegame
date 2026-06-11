using Godot;
using System;
using System.Collections.Generic; // Szükséges a List-hez

public partial class BasePlayer : CharacterBody2D
{
    [Export] public float Speed = 300.0f;
    [Export] public int MaxHealth = 100;
    public int CurrentHealth;
    [Export] public int AttackDamage = 20;
    [Export] public float AttackCooldown = 0.5f;

    [Export] public int CurrentXP = 0;
    [Export] public int MaxXP = 100;
    [Export] public int Level = 1;

    [Export] public int PotionsCount = 0;
    public int MaxPotionSlots = 3; 
    [Export] public string InitialIdleAnimation = "idle_front";
    [Export] public bool InitialFlipH = false;

    [ExportGroup("Menus")]
    [Export] public Control UpgradeMenuNode; 
    [Export] public Button BtnSpeed;
    [Export] public Button BtnDamage;
    [Export] public Button BtnAtkSpeed;
    [Export] public Control InventoryNode; 

    // --- ÚJ INVENTORY UI KAPCSOLATOK ---
    [ExportGroup("Inventory UI Stats")]
    [Export] public Label StatHPLabel;
    [Export] public Label StatManaLabel;
    [Export] public Label StatAtkLabel;
    [Export] public Label StatSpeedLabel;
    [Export] public Label LvlSpeedLabel;
    [Export] public Label LvlAtkLabel;
    [Export] public Label LvlAtkSpeedLabel;

    // --- INVENTORY MANAGER KAPCSOLAT ---
    public InventoryManager Inventory; 

    private bool _isInventoryOpen = false;
    private AnimatedSprite2D _animSprite;
    private Timer _blinkTimer;
    private Timer _footstepTimer;
    private float _idleTime = 0.0f; 
    private string _currentDirAnim = "idle_front";

    // --- MANA RENDSZER ---
    public float MaxMana = 100f;            // Maximális mana
    public float CurrentMana = 100f;        // Jelenlegi mana
    public float ManaRegenRate = 20f;       // Mennyi mana töltődik vissza másodpercenként
    public float ManaCost = 5f;            // Mennyibe kerül egy mágikus ütés/képesség

    private float _timeSinceLastAction = 0f; // Méri az időt az utolsó mana-használat óta
    public float ManaRegenDelay = 1.0f;      // Mennyit kell várni használat után, hogy elkezdjen tölteni

    public override void _Ready()
    {
        AddToGroup("Player");
        CurrentHealth = MaxHealth;
        
        // Inventory Manager inicializálása
        Inventory = new InventoryManager();
        CallDeferred("add_child", Inventory);

        UpdateUI();
        
        if (BtnSpeed != null) BtnSpeed.Pressed += () => ApplyUpgrade("speed");
        if (BtnDamage != null) BtnDamage.Pressed += () => ApplyUpgrade("damage");
        if (BtnAtkSpeed != null) BtnAtkSpeed.Pressed += () => ApplyUpgrade("atk_speed");
        if (UpgradeMenuNode != null) UpgradeMenuNode.Visible = false;

        _animSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_animSprite != null)
        {
            if (_animSprite.SpriteFrames.HasAnimation(InitialIdleAnimation))
            {
                _currentDirAnim = InitialIdleAnimation;
            }

            _animSprite.FlipH = InitialFlipH;
            _animSprite.Play(_currentDirAnim);
        }
        
        _blinkTimer = new Timer();
        _blinkTimer.OneShot = true;
        AddChild(_blinkTimer);
        _blinkTimer.Timeout += OnBlinkTimerTimeout;

        // Footstep timer
        _footstepTimer = new Timer
        {
            OneShot = false,
            WaitTime = 0.45f,
            Autostart = false
        };
        AddChild(_footstepTimer);
        _footstepTimer.Timeout += () => { if (!GetTree().Paused) AudioManager.Instance?.PlayFootstep(GlobalPosition); };
    }

    public override void _PhysicsProcess(double delta)
{
    // 1. Inventory megnyitás/bezárás (ez megállítja a játékot, így az elején kell lennie)
    if (Input.IsActionJustPressed("inventory"))
    {
        ToggleInventory();
        return;
    }

    // 2. Ha szünetel a játék (pl. Inventory vagy Upgrade menü miatt), ne fusson a többi kód
    if (GetTree().Paused) return;

    // 3. MANA VISSZATÖLTŐDÉS KEZELÉSE
    _timeSinceLastAction += (float)delta;
    if (_timeSinceLastAction >= ManaRegenDelay && CurrentMana < MaxMana)
    {
        CurrentMana += ManaRegenRate * (float)delta;
        if (CurrentMana > MaxMana) CurrentMana = MaxMana;
        UpdateUI(); // Frissítjük a kék sávot és az Inventory-t is
    }

    // 4. Gyógyítás (Potions)
    if (Input.IsActionJustPressed("heal")) UsePotion();

    // 5. Mozgás és irány kiszámítása
    Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
    Velocity = direction != Vector2.Zero ? direction * Speed : Velocity.MoveToward(Vector2.Zero, Speed);
    
    // 6. ANIMÁCIÓK KEZELÉSE
    if (_animSprite != null)
    {
        if (direction != Vector2.Zero)
        {
            // MOZGÁS KÖZBEN
            _idleTime = 0.0f;
            _blinkTimer.Stop(); 
            if (_footstepTimer != null && _footstepTimer.IsStopped()) _footstepTimer.Start();

            // Irány meghatározása (Prioritás: Fel, Le, majd Oldal)
            string baseDir = "";
            if (direction.Y < 0) baseDir = "back";
            else if (direction.Y > 0) baseDir = "front";
            else if (direction.X != 0) baseDir = "side";

            _currentDirAnim = "idle_" + baseDir; 
            string walkAnimName = "walk_" + baseDir;

            // Csak akkor váltunk, ha tényleg más animációra van szükség (megszakítja a pislogást is)
            if (_animSprite.Animation != walkAnimName)
            {
                if (_animSprite.SpriteFrames.HasAnimation(walkAnimName))
                    _animSprite.Play(walkAnimName);
                else
                    _animSprite.Play(_currentDirAnim);
            }
            
            // Tükrözés (Csak oldalra nézésnél: A = tükröz, D = alap)
            if (baseDir == "side")
            {
                _animSprite.FlipH = direction.X < 0;
            }
            else
            {
                _animSprite.FlipH = false;
            }
        }
        else
        {
            // ÁLLÓ HELYZETBEN
            if (_footstepTimer != null && !_footstepTimer.IsStopped()) _footstepTimer.Stop();
            _idleTime += (float)delta;

            // Alapállapot lejátszása, ha nem épp pislog (blink)
            if (_animSprite.Animation != "blink")
            {
                _animSprite.Play(_currentDirAnim);
            }

            // Ha a pislogás animáció épp véget ért, váltson vissza idle-re
            if (_animSprite.Animation == "blink" && !_animSprite.IsPlaying())
            {
                _animSprite.Play(_currentDirAnim);
            }

            // Automatikus pislogás indítása 5 mp állás után (csak ha előre néz)
            if (_idleTime >= 5.0f && _currentDirAnim == "idle_front" && _blinkTimer.IsStopped())
            {
                StartRandomBlinkTimer();
            }
        }
    }

    // 7. Támadási terület (AttackArea) forgatása az egér irányába
    var attackArea = GetNodeOrNull<Area2D>("AttackArea");
    if (attackArea != null)
    {
        Vector2 mousePos = GetGlobalMousePosition();
        Vector2 toMouse = (mousePos - GlobalPosition).Normalized();
        attackArea.Position = toMouse * 50.0f;
        attackArea.Rotation = toMouse.Angle();
    }

    // 8. Fizikai mozgás végrehajtása (ütközésekkel)
    MoveAndSlide();

    // 9. Támadás (Mana költség ellenőrzéssel az Attack függvényben)
    if (Input.IsActionJustPressed("attack") && !_isInventoryOpen) Attack();
}

    private void ToggleInventory()
    {
        _isInventoryOpen = !_isInventoryOpen;
        
        if (InventoryNode != null)
        {
            InventoryNode.Visible = _isInventoryOpen;
            if (_isInventoryOpen) UpdateInventoryStatsUI();
        }

        GetTree().Paused = _isInventoryOpen;

        if (_isInventoryOpen)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        else
        {
            Input.MouseMode = Input.MouseModeEnum.Visible; 
        }
    }

    private void UpdateInventoryStatsUI()
    {
        if (StatHPLabel != null) StatHPLabel.Text = $"HP: {CurrentHealth} / {MaxHealth}";
        if (StatManaLabel != null) StatManaLabel.Text = $"Mana: {Mathf.RoundToInt(CurrentMana)} / {MaxMana}";
        if (StatAtkLabel != null) StatAtkLabel.Text = $"Attack: {AttackDamage}";
        if (StatSpeedLabel != null) StatSpeedLabel.Text = $"Speed: {Mathf.Round(Speed)}";

        int speedLvl = (int)((Speed - 300) / 40);
        int damageLvl = (AttackDamage - 20) / 10;
        int atkSpeedLvl = (int)((0.5f - AttackCooldown) / 0.05f);

        if (LvlSpeedLabel != null) LvlSpeedLabel.Text = $"Speed Lvl: {speedLvl}";
        if (LvlAtkLabel != null) LvlAtkLabel.Text = $"Dmg Lvl: {damageLvl}";
        if (LvlAtkSpeedLabel != null) LvlAtkSpeedLabel.Text = $"AtkSpd Lvl: {atkSpeedLvl}";
    }

    private void StartRandomBlinkTimer() 
    {
        _blinkTimer.Start((float)GD.RandRange(1.0, 3.0));
    }

    private void OnBlinkTimerTimeout()
    {
        if (_animSprite != null && !GetTree().Paused && Velocity == Vector2.Zero && _currentDirAnim == "idle_front") 
        {
            _animSprite.Play("blink");
        }
    }

    public async void TakeDamage(int amount)
    {
        CurrentHealth -= amount;
        UpdateUI();

        if (_animSprite != null)
        {
            _animSprite.SelfModulate = new Color(1, 0, 0); 
            await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
            if (IsInstanceValid(_animSprite)) 
                _animSprite.SelfModulate = new Color(1, 1, 1); 
        }

        if (CurrentHealth <= 0)
        {
            AudioManager.Instance?.RestartCurrentMusic();
            GetTree().ReloadCurrentScene();
        }
    }

    public void CollectPotion() { if (PotionsCount < MaxPotionSlots) { PotionsCount++; UpdateUI(); } }
    
    public void UsePotion()
    {
        if (PotionsCount > 0 && CurrentHealth < MaxHealth)
        {
            CurrentHealth = Mathf.Min(CurrentHealth + 20, MaxHealth);
            PotionsCount--;
            UpdateUI();
        }
    }

    public void GainXP(int amount) { CurrentXP += amount; if (CurrentXP >= MaxXP) LevelUp(); UpdateUI(); }
    
    private void LevelUp()
    {
        Level++;
        CurrentXP = 0;
        MaxXP = (int)(MaxXP * 1.5);
        
        MaxHealth += 20;
        CurrentHealth = MaxHealth;

        // --- ÚJ: MANA SZINTLÉPÉS ---
        MaxMana += 10; 
        CurrentMana = MaxMana; // Feltöltjük a manát is szintlépéskor!
        // -----------------------------

        AudioManager.Instance?.PlayLevelUp();
        if (Level % 5 == 0) MaxPotionSlots++;
        
        GetTree().Paused = true; 
        if (UpgradeMenuNode != null) { UpgradeMenuNode.Visible = true; Input.MouseMode = Input.MouseModeEnum.Visible; }
        UpdateUI();
    }

    private void ApplyUpgrade(string type)
    {
        if (type == "speed") Speed += 40.0f;
        if (type == "damage") AttackDamage += 10;
        if (type == "atk_speed") AttackCooldown *= 0.85f;
        if (UpgradeMenuNode != null) UpgradeMenuNode.Visible = false;
        GetTree().Paused = false; 
        UpdateUI();
    }

    private void UpdateUI()
    {
        var hudHP = GetNodeOrNull<ProgressBar>("/root/World/CanvasLayer/Control/HealthBar");
        var hudXP = GetNodeOrNull<ProgressBar>("/root/World/CanvasLayer/Control/ProgressBar");
        var potLabel = GetNodeOrNull<Label>("/root/World/CanvasLayer/Control/PotionLabel");
        var lvlLabel = GetNodeOrNull<Label>("/root/World/CanvasLayer/Control/Label");

        // --- ÚJ: MANA SÁV FRISSÍTÉSE ---
        var hudMana = GetNodeOrNull<ProgressBar>("/root/World/CanvasLayer/Control/ManaBar");

        // Ha bármelyik elem nem található, megpróbáljuk újra megtalálni őket a gyökeres Control alatt (ez hasznos lehet, ha a jelenetstruktúra változik)
        if (hudHP == null || hudXP == null || potLabel == null || lvlLabel == null || hudMana == null)
        {
            var control = GetTree().Root.FindChild("Control", true, false) as Control;
            if (control != null)
            {
                hudHP = hudHP ?? control.GetNodeOrNull<ProgressBar>("HealthBar");
                hudXP = hudXP ?? control.GetNodeOrNull<ProgressBar>("ProgressBar");
                potLabel = potLabel ?? control.GetNodeOrNull<Label>("PotionLabel");
                lvlLabel = lvlLabel ?? control.GetNodeOrNull<Label>("Label");
                hudMana = hudMana ?? control.GetNodeOrNull<ProgressBar>("ManaBar");
            }
        }

        if (hudMana != null) { hudMana.MaxValue = MaxMana; hudMana.Value = CurrentMana; }
        if (hudHP != null) { hudHP.MaxValue = MaxHealth; hudHP.Value = CurrentHealth; }
        if (hudXP != null) { hudXP.MaxValue = MaxXP; hudXP.Value = CurrentXP; }
        if (lvlLabel != null) { lvlLabel.Text = "LVL " + Level; }
        if (potLabel != null) { potLabel.Text = "x" + PotionsCount + "/" + MaxPotionSlots; }
        
        if (_isInventoryOpen) UpdateInventoryStatsUI();
    }

    public void RefreshUI() { UpdateUI(); }

    private void Attack()
    {
        // --- ÚJ: MANA ELLENŐRZÉSE ÜTÉS ELŐTT ---
        if (CurrentMana < ManaCost)
        {
            GD.Print("Nincs elég mana az ütéshez!");
            return; // Megszakítjuk a támadást, a lenti kód nem fut le!
        }

        CurrentMana -= ManaCost;       // Levonjuk az árat
        _timeSinceLastAction = 0f;     // Nullázzuk az órát (megáll a visszatöltés 1 mp-re)
        UpdateUI();                    // Frissítjük a sávot
        // ----------------------------------------

        var attackArea = GetNodeOrNull<Area2D>("AttackArea");
        if (attackArea == null) return;

        AudioManager.Instance?.PlayPlayerAttack(GlobalPosition);

        foreach (var body in attackArea.GetOverlappingBodies())
        {
            if (body != this && body.HasMethod("TakeDamage"))
                body.Call("TakeDamage", AttackDamage);
        }
    }

    // --- FLASHLIGHT  ---
    public void EquipFlashlight()
    {
        if (GetNodeOrNull<PointLight2D>("FlashlightLight") != null) return;

        var light = new PointLight2D();
        light.Name = "FlashlightLight";
        // Egyszerű kör alakú textúrát generálunk a lámpához, ahol a közepén erős fény van, és a szélek felé fokozatosan halványul.
        light.Texture = CreateFlashlightTexture(100, 100);
        light.Color = new Color(1.0f, 0.97f, 0.9f, 1.0f);
        light.Energy = 0.9f;
        light.TextureScale = 0.55f;
        light.Offset = new Vector2(0, -14);
        AddChild(light);
        // Itt frissítjük a GroundFloorController-t is, hogy újra kiszámolja a sötétség fókuszát a lámpa miatt
        var groundFloor = GetTree().Root.FindChild("GroundFloor", true, false) as Godot.Node;
        if (groundFloor is Godot.Node)
        {
            var gfc = groundFloor as Godot.Node;
            // Mivel a GroundFloorController-ben van egy UpdateDarknessFocus függvény, amit a lámpa hatásának frissítésére használunk, itt is meghívjuk, hogy az új lámpa hatása azonnal érvényesüljön.
            if (gfc.HasMethod("UpdateDarknessFocus")) gfc.Call("UpdateDarknessFocus");
        }
    }

    public void UnequipFlashlight()
    {
        var light = GetNodeOrNull<PointLight2D>("FlashlightLight");
        if (light == null) return;

        light.QueueFree();

        var groundFloor = GetTree().Root.FindChild("GroundFloor", true, false) as Godot.Node;
        if (groundFloor != null && groundFloor.HasMethod("UpdateDarknessFocus"))
        {
            groundFloor.Call("UpdateDarknessFocus");
        }
    }

    private Texture2D CreateFlashlightTexture(int width, int height)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float maxDistance = Mathf.Min(width, height) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = new Vector2(x, y).DistanceTo(center);
                float normalized = Mathf.Clamp(1.0f - (distance / maxDistance), 0.0f, 1.0f);
                float alpha = Mathf.Pow(normalized, 2.2f);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
