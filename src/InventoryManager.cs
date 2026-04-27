using Godot;
using System.Collections.Generic;

public partial class InventoryManager : Node
{
    // A felvett tárgyak neveinek listája
    public List<string> Items = new List<string>();
    [Export] public int MaxSlots = 8;

    public bool AddItem(string itemName)
    {
        if (Items.Count < MaxSlots)
        {
            Items.Add(itemName);
            GD.Print($"Tárgy felvéve: {itemName}");
            UpdateUI();
            return true;
        }
        GD.Print("Az inventory megtelt!");
        return false;
    }

    public void UpdateUI()
    {
        // Megkeressük a GridContainer-t. 
        // Mivel a Player gyereke vagy, a fád alapján így érjük el a CanvasLayer-en keresztül:
        var grid = GetTree().Root.FindChild("GridContainer", true, false) as GridContainer;

        if (grid == null)
        {
            GD.PrintErr("Hiba: Nem találom a GridContainer-t az UI-ban!");
            return;
        }

        // Végigmegyünk a rács összes gyerekén (Slot1, Slot2...)
        for (int i = 0; i < grid.GetChildCount(); i++)
        {
            var slot = grid.GetChild(i);
            // Megkeressük a Slot alatti Icon node-ot
            var iconDisplay = slot.GetNodeOrNull<TextureRect>("Icon");

            if (iconDisplay != null)
            {
                // Ha van felvett tárgy ehhez a slot indexhez
                if (i < Items.Count)
                {
                    iconDisplay.Texture = GetTextureForItem(Items[i]);
                    iconDisplay.Visible = true; // Megmutatjuk az ikont
                }
                else
                {
                    // Ha nincs tárgy, kiürítjük a slotot
                    iconDisplay.Texture = null;
                    iconDisplay.Visible = false; // Elrejtjük az üres ikont
                }
            }
        }
    }

    // Ez a függvény rendeli hozzá a nevekhez a képeket
    private Texture2D GetTextureForItem(string name)
    {
        switch (name)
        {
            case "KeyPart1":
                // Ide írd a kulcs ikonod PONTOS elérési útját a FileSystem-ből!
                // Példa: "res://assets/items/key_icon.png"
                return GD.Load<Texture2D>("res://scenes/TutorialKeyPart.png"); 
            
            // Később ide jöhet a többi tárgy:
            // case "KeyPart2": return GD.Load<Texture2D>("...");

            default:
                return null;
        }
    }
}