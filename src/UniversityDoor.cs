using Godot;
using System;

public partial class UniversityDoor : StaticBody2D
{
    private bool _questTriggered = false;

    public override void _Ready()
    {
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
        if (body is BasePlayer)
        {
            // 1. ELŐSZÖR megnézzük, nálunk van-e már a kész kulcs!
            if (InventoryManager.Items.Contains("UniversityKey"))
            {
                GD.Print("Ajtó kinyitva!");
                AudioManager.Instance?.PlayDoor(GlobalPosition);
                var worldScript = GetTree().Root.FindChild("World", true, false) as WorldController;
                if (worldScript != null) 
                {
                    worldScript.UpdateQuestText("Küldetés: Lépj be az egyetemre!");
                }
                
                // Ez a sor tünteti el a láthatatlan falat, hogy be tudj menni!
                QueueFree(); 
            }
            // 2. Ha nincs kulcs, és még nem indult el a buszos esemény:
            else if (!_questTriggered)
            {
                _questTriggered = true;
                UpdateQuest();
            }
        }
    }

    private void UpdateQuest()
    {
        var label = GetTree().Root.FindChild("QuestLabel", true, false) as Label;
        if (label != null)
        {
            label.Text = "Küldetés: Szerezd meg a kulcsot a buszról!";
        }

        var worldScript = GetTree().Root.FindChild("World", true, false) as WorldController;
        if (worldScript != null)
        {
            worldScript.StartZombieBusEvent();
        }
    }
}