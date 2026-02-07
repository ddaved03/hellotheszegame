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

    [Export] public Control UpgradeMenuNode; 
    [Export] public Button BtnSpeed;
    [Export] public Button BtnDamage;
    [Export] public Button BtnAtkSpeed;

    [Export] public Texture2D FrontSprite; 
    [Export] public Texture2D BackSprite;  

    public override void _Ready()
    {
        AddToGroup("Player");
        CurrentHealth = MaxHealth;
        UpdateUI();
        SetupUpgradeButtons();
        if (UpgradeMenuNode != null) UpgradeMenuNode.Visible = false;
    }

    private void SetupUpgradeButtons()
    {
        if (BtnSpeed != null) BtnSpeed.Pressed += () => ApplyUpgrade("speed");
        if (BtnDamage != null) BtnDamage.Pressed += () => ApplyUpgrade("damage");
        if (BtnAtkSpeed != null) BtnAtkSpeed.Pressed += () => ApplyUpgrade("atk_speed");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GetTree().Paused) return;

        Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        Velocity = direction != Vector2.Zero ? direction * Speed : Velocity.MoveToward(Vector2.Zero, Speed);
        
        var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        var attackArea = GetNodeOrNull<Area2D>("AttackArea");

        // Karakter irányának kezelése (W-re hátat fordít)
        if (sprite != null)
        {
            if (direction.Y < 0 && BackSprite != null) sprite.Texture = BackSprite;
            else if (direction.Y > 0 || direction.X != 0) if (FrontSprite != null) sprite.Texture = FrontSprite;
            if (direction.X != 0) sprite.FlipH = direction.X < 0;
        }

        // --- VISSZATETT FUNKCIÓ: Támadás az egér irányába ---
        if (attackArea != null)
        {
            Vector2 mousePos = GetGlobalMousePosition();
            Vector2 toMouse = (mousePos - GlobalPosition).Normalized();
            attackArea.Position = toMouse * 50.0f; // 50 pixelre a karaktertől az egér felé
            attackArea.Rotation = toMouse.Angle(); // Az Area is elfordul az egér felé
        }

        MoveAndSlide();
        if (Input.IsActionJustPressed("attack")) Attack();
    }

    public void GainXP(int amount)
    {
        CurrentXP += amount;
        if (CurrentXP >= MaxXP) LevelUp();
        UpdateUI();
    }

    private void LevelUp()
    {
        Level++;
        CurrentXP = 0;
        MaxXP = (int)(MaxXP * 1.5);
        GetTree().Paused = true; 
        if (UpgradeMenuNode != null)
        {
            UpgradeMenuNode.Visible = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
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

    public void TakeDamage(int amount)
    {
        CurrentHealth -= amount;
        UpdateUI();
        if (CurrentHealth <= 0) GetTree().ReloadCurrentScene();
    }

    private void UpdateUI()
    {
        var hudHP = GetNodeOrNull<ProgressBar>("/root/World/CanvasLayer/Control/HealthBar"); 
        var hudXP = GetNodeOrNull<ProgressBar>("/root/World/CanvasLayer/Control/ProgressBar");
        var lvlLabel = GetNodeOrNull<Label>("/root/World/CanvasLayer/Control/Label");

        if (hudHP != null) { hudHP.MaxValue = MaxHealth; hudHP.Value = CurrentHealth; }
        if (hudXP != null) { hudXP.MaxValue = MaxXP; hudXP.Value = CurrentXP; }
        if (lvlLabel != null) { lvlLabel.Text = "LVL " + Level; }
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