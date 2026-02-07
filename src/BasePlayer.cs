using Godot;
using System;

public partial class BasePlayer : CharacterBody2D
{
    [Export] public float Speed = 300.0f;
    [Export] public int MaxHealth = 100;
    [Export] public int CurrentHealth;
    [Export] public int AttackDamage = 20;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Mozgás
        Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        if (direction != Vector2.Zero)
        {
            Velocity = direction * Speed;
            if (direction.X != 0)
            {
                GetNode<Sprite2D>("Sprite2D").FlipH = direction.X < 0;
            }
        }
        else
        {
            Velocity = Velocity.MoveToward(Vector2.Zero, Speed);
        }

        MoveAndSlide();

        // Támadás
        if (Input.IsActionJustPressed("attack"))
        {
            Attack();
        }
    }

    private void Attack()
    {
        GD.Print("Játékos lendíti a fegyvert!");
        var attackArea = GetNode<Area2D>("AttackArea");
        var targets = attackArea.GetOverlappingBodies();

        foreach (var body in targets)
        {
            if (body.HasMethod("TakeDamage") && body != this)
            {
                body.Call("TakeDamage", AttackDamage);
            }
        }
    }

    // Ezt hívja meg a zombi, ha megüt
    public void TakeDamage(int amount)
    {
        CurrentHealth -= amount;
        GD.Print($"Játékos megütve! Életerő: {CurrentHealth}/{MaxHealth}");

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        GD.Print("A játékos meghalt! Game Over.");
        // Itt később újraindíthatjuk a szintet: GetTree().ReloadCurrentScene();
    }
}