using Godot;
using System;

// Egy felvehető tapasztalati pont (XP Orb) objektum
public partial class XpOrb : Area2D
{
    // A felvételkor kapott tapasztalati pontok száma
    [Export] public int XpAmount = 25;

    public override void _Ready()
    {
        // Ha valaki hozzáér, ellenőrizzük, hogy a Player-e
        BodyEntered += (body) => 
        {
            if (body is BasePlayer player)
            {
                player.GainXP(XpAmount);
                AudioManager.Instance?.PlayPickupXp(GlobalPosition);
                QueueFree(); // Felvétel után eltűnik
            }
        };
    }

    public override void _Process(double delta)
    {
        // Lebegő mozgás szinusz függvénnyel
        Position += new Vector2(0, (float)Math.Sin(Time.GetTicksMsec() * 0.005f) * 0.15f);
    }
}