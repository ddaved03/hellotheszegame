using Godot;
using System;

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

    [ExportGroup("Menus")]
    [Export] public Control UpgradeMenuNode; 
    [Export] public Button BtnSpeed;
    [Export] public Button BtnDamage;
    [Export] public Button BtnAtkSpeed;
    [Export] public Control InventoryNode; 

    // --- ÚJ INVENTORY UI KAPCSOLATOK ---
    [ExportGroup("Inventory UI Stats")]
    [Export] public Label StatHPLabel;
    [Export] public Label StatAtkLabel;
    [Export] public Label StatSpeedLabel;
    [Export] public Label LvlSpeedLabel;
    [Export] public Label LvlAtkLabel;
    [Export] public Label LvlAtkSpeedLabel;

    private bool _isInventoryOpen = false;
    private AnimatedSprite2D _animSprite;
    private Timer _blinkTimer;
    private float _idleTime = 0.0f; 
    private string _currentDirAnim = "idle_front";

    public override void _Ready()
    {
        AddToGroup("Player");
        CurrentHealth = MaxHealth;
        UpdateUI();
        
        if (BtnSpeed != null) BtnSpeed.Pressed += () => ApplyUpgrade("speed");
        if (BtnDamage != null) BtnDamage.Pressed += () => ApplyUpgrade("damage");
        if (BtnAtkSpeed != null) BtnAtkSpeed.Pressed += () => ApplyUpgrade("atk_speed");
        if (UpgradeMenuNode != null) UpgradeMenuNode.Visible = false;

        _animSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        
        _blinkTimer = new Timer();
        _blinkTimer.OneShot = true;
        AddChild(_blinkTimer);
        _blinkTimer.Timeout += OnBlinkTimerTimeout;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.IsActionJustPressed("inventory"))
        {
            ToggleInventory();
            return;
        }

        if (GetTree().Paused) return;

        if (Input.IsActionJustPressed("heal")) UsePotion();

        Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        Velocity = direction != Vector2.Zero ? direction * Speed : Velocity.MoveToward(Vector2.Zero, Speed);
        
        if (_animSprite != null)
        {
            if (direction != Vector2.Zero)
            {
                _idleTime = 0.0f;
                _blinkTimer.Stop(); 

                if (direction.Y < 0) _currentDirAnim = "idle_back";
                else if (direction.Y > 0) _currentDirAnim = "idle_front";
                else if (direction.X != 0) _currentDirAnim = "idle_side";

                if (_animSprite.Animation != "blink") 
                {
                    _animSprite.Play(_currentDirAnim);
                }
                
                if (direction.X != 0) _animSprite.FlipH = direction.X < 0;
            }
            else
            {
                _idleTime += (float)delta;

                if (!_animSprite.IsPlaying())
                {
                    _animSprite.Play(_currentDirAnim);
                }

                if (_idleTime >= 5.0f && _currentDirAnim == "idle_front" && _blinkTimer.IsStopped())
                {
                    StartRandomBlinkTimer();
                }
            }
        }

        var attackArea = GetNodeOrNull<Area2D>("AttackArea");
        if (attackArea != null)
        {
            Vector2 mousePos = GetGlobalMousePosition();
            Vector2 toMouse = (mousePos - GlobalPosition).Normalized();
            attackArea.Position = toMouse * 50.0f;
            attackArea.Rotation = toMouse.Angle();
        }

        MoveAndSlide();
        if (Input.IsActionJustPressed("attack") && !_isInventoryOpen) Attack();
    }

    private void ToggleInventory()
    {
        _isInventoryOpen = !_isInventoryOpen;
        
        if (InventoryNode != null)
        {
            InventoryNode.Visible = _isInventoryOpen;
            if (_isInventoryOpen) UpdateInventoryStatsUI(); // Frissítés nyitáskor
        }

        GetTree().Paused = _isInventoryOpen;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    // ÚJ: Dinamikus statisztika frissítő függvény
    private void UpdateInventoryStatsUI()
    {
        if (StatHPLabel != null) StatHPLabel.Text = $"HP: {CurrentHealth} / {MaxHealth}";
        if (StatAtkLabel != null) StatAtkLabel.Text = $"Attack: {AttackDamage}";
        if (StatSpeedLabel != null) StatSpeedLabel.Text = $"Speed: {Mathf.Round(Speed)}";

        // Szintek kiszámítása az alapértékekhez képest
        int speedLvl = (int)((Speed - 300) / 40);
        int damageLvl = (AttackDamage - 20) / 10;
        int atkSpeedLvl = (int)((0.5f - AttackCooldown) / 0.05f); // Példa számítás

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

        if (CurrentHealth <= 0) GetTree().ReloadCurrentScene();
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
        
        // --- AUTOMATIKUS ÉLETERŐ NÖVEKEDÉS ---
        MaxHealth += 20; // Szintenként 20-szal nő a max HP
        CurrentHealth = MaxHealth; // Szintlépéskor teljesen meggyógyul

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
        var lvlLabel = GetNodeOrNull<Label>("/root/World/CanvasLayer/Control/Label");
        var potLabel = GetNodeOrNull<Label>("/root/World/CanvasLayer/Control/PotionLabel");

        if (hudHP != null) { hudHP.MaxValue = MaxHealth; hudHP.Value = CurrentHealth; }
        if (hudXP != null) { hudXP.MaxValue = MaxXP; hudXP.Value = CurrentXP; }
        if (lvlLabel != null) { lvlLabel.Text = "LVL " + Level; }
        if (potLabel != null) { potLabel.Text = "x" + PotionsCount + "/" + MaxPotionSlots; }
        
        // Ha nyitva van az inventory, akkor az ottani statokat is frissítjük
        if (_isInventoryOpen) UpdateInventoryStatsUI();
    }

    public void RefreshUI() { UpdateUI(); }

    private void Attack()
    {
        var attackArea = GetNodeOrNull<Area2D>("AttackArea");
        if (attackArea == null) return;

        AudioManager.Instance?.PlayPlayerAttack(GlobalPosition);

        foreach (var body in attackArea.GetOverlappingBodies())
        {
            if (body != this && body.HasMethod("TakeDamage"))
                body.Call("TakeDamage", AttackDamage);
        }
    }
}