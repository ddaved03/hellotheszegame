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

    [Export] public Control UpgradeMenuNode; 
    [Export] public Button BtnSpeed;
    [Export] public Button BtnDamage;
    [Export] public Button BtnAtkSpeed;

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

        // Az AnimatedSprite2D node megkeresése
        _animSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        
        _blinkTimer = new Timer();
        _blinkTimer.OneShot = true;
        AddChild(_blinkTimer);
        _blinkTimer.Timeout += OnBlinkTimerTimeout;
    }

    public override void _PhysicsProcess(double delta)
    {
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

                // Irány meghatározása az animációkhoz
                if (direction.Y < 0) _currentDirAnim = "idle_back";
                else if (direction.Y > 0) _currentDirAnim = "idle_front";
                else if (direction.X != 0) _currentDirAnim = "idle_side";

                if (_animSprite.Animation != "blink") // Ne szakítsuk félbe a pislogást mozgással, ha nem muszáj
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
        if (Input.IsActionJustPressed("attack")) Attack();
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

    // ÚJ: Javított TakeDamage a piros villanáshoz
    public async void TakeDamage(int amount)
    {
        CurrentHealth -= amount;
        UpdateUI();

        if (_animSprite != null)
        {
            _animSprite.SelfModulate = new Color(1, 0, 0); // Piros villanás
            await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
            if (IsInstanceValid(_animSprite)) 
                _animSprite.SelfModulate = new Color(1, 1, 1); // Vissza fehérre
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
    }

    private void Attack()
    {
        var attackArea = GetNodeOrNull<Area2D>("AttackArea");
        if (attackArea == null) return;
        foreach (var body in attackArea.GetOverlappingBodies())
        {
            if (body != this && body.HasMethod("TakeDamage"))
                body.Call("TakeDamage", AttackDamage);
        }
    }
}