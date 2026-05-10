using Godot;

public partial class TutorialItem : Area2D
{
    [Export] public string ItemName; // Pl: "KeyPart1", "KeyPart2"

public override void _Ready()
{
    GD.Print("TutorialItem inicializálva!");

    // Próbáljuk megkeresni a gyereket relatív útvonallal
    // A "." jelenti magát a node-ot, a "Sprite2D" a gyereket
    var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");

    if (sprite == null)
    {
        // Ha név alapján nem megy, megkeressük típus alapján (ez a legbiztosabb)
        foreach (var child in GetChildren())
        {
            if (child is Sprite2D s)
            {
                sprite = s;
                break;
            }
        }
    }

    if (sprite != null)
    {
        GD.Print("Siker! Sprite megtalálva, indul a lebegés.");
        
        // Elmentjük az eredeti Y pozíciót
        float startY = sprite.Position.Y;
        
        var tween = CreateTween().SetLoops();
        // A lebegés: 1.2 másodperc alatt fel, majd le
        tween.TweenProperty(sprite, "position:y", startY - 10.0f, 1.2f).SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(sprite, "position:y", startY, 1.2f).SetTrans(Tween.TransitionType.Sine);
    }
    else
    {
        GD.PrintErr("KRITIKUS HIBA: Továbbra sem találom a Sprite2D-t!");
    }

    BodyEntered += (body) => {
        GD.Print("Valami hozzáért a kulcshoz: " + body.Name);
        // Fontos: a body-nak BasePlayer típusúnak kell lennie
        if (body is BasePlayer player) {
            // Ellenőrizzük, hogy az Inventory már létrejött-e a CallDeferred miatt
            if (player.Inventory != null)
            {
                bool success = player.Inventory.AddItem(ItemName); 
                
                if (success) {
                    var world = GetTree().Root.FindChild("World", true, false) as WorldController;
                    if (world != null) world.OnKeyPartCollected(ItemName);

                        var groundFloor = GetTree().Root.FindChild("GroundFloor", true, false) as GroundFloorController;
                        if (groundFloor != null) groundFloor.OnKeyPartCollected(ItemName);

                    // Play pickup sound based on item name
                    if (AudioManager.Instance != null)
                    {
                        if (ItemName != null && ItemName.ToLower().Contains("key"))
                        {
                            AudioManager.Instance?.PlayKeyPickup(GlobalPosition);
                        }
                        else if (ItemName != null && ItemName.ToLower().Contains("potion"))
                        {
                            AudioManager.Instance?.PlayPickupPotion(GlobalPosition);
                        }
                        else if (ItemName != null && (ItemName.ToLower().Contains("xp") || ItemName.ToLower().Contains("xporb")))
                        {
                            AudioManager.Instance?.PlayPickupXp(GlobalPosition);
                        }
                    }

                    QueueFree(); // Eltüntetjük a földről
                }
            }
            else {
            GD.PrintErr("Hiba: A Player Inventory-ja még mindig null!");
            }
        }
    };
}
}