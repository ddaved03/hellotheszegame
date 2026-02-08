using Godot;
using System;

public partial class Potion : Area2D
{
    private float _startSlotY;
    private float _timePassed = 0.0f;

    public override void _Ready()
    {
        // Elmentjük a kezdőpozíciót, hogy ahhoz képest lebegjen
        _startSlotY = Position.Y;

        BodyEntered += (body) => 
        {
            if (body is BasePlayer player)
            {
                player.CollectPotion();
                QueueFree();
            }
        };
    }

    public override void _Process(double delta)
    {
        // Ha a játék megáll (szintlépés), a lebegés is álljon meg
        if (GetTree().Paused) return;

        _timePassed += (float)delta;

        // Szinusz hullám alapú lebegés (mint az XP-nél)
        // 5.0f a sebesség, 10.0f a kilengés mértéke
        Vector2 newPos = Position;
        newPos.Y = _startSlotY + (Mathf.Sin(_timePassed * 5.0f) * 10.0f);
        Position = newPos;
    }
}