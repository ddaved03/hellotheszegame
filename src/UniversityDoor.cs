using Godot;
using System;

public partial class UniversityDoor : StaticBody2D
{
    private bool _questTriggered = false;

    public override void _Ready()
    {
        // Megpróbáljuk megkeresni a DetectionArea-t. 
        // Ha nem találja ezen a néven, hibát dobna, ezért teszünk bele egy ellenőrzést.
        var area = GetNodeOrNull<Area2D>("DetectionArea");
        
        if (area != null)
        {
            area.BodyEntered += OnBodyEntered;
        }
        else
        {
            GD.PrintErr("HIBA: Nem találom a DetectionArea-t az UniversityDoor alatt!");
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is BasePlayer && !_questTriggered)
        {
            _questTriggered = true;
            UpdateQuest();
        }
    }

    private void UpdateQuest()
    {
        // Itt a te általad megadott útvonalat használjuk
        var label = GetTree().Root.FindChild("QuestLabel", true, false) as Label;
        if (label != null)
        {
            label.Text = "Küldetés: Szerezd meg a kulcsot a buszról!";
        }

        // Megkeressük a World node-ot (amin a WorldController script van)
        var worldScript = GetTree().Root.FindChild("World", true, false) as WorldController;
        if (worldScript != null)
        {
            worldScript.StartZombieBusEvent();
        }
    }
}