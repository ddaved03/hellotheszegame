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
    // Megkeressük a GridContainer-t a teljes jelenetfában
    // A FindChild helyett próbáljuk meg így, ez stabilabb:
    var grid = GetTree().Root.FindChild("GridContainer", true, false) as GridContainer;

    if (grid == null)
    {
        GD.PrintErr("!!! HIBA: Nem találom a GridContainer-t az UI-ban!");
        return;
    }

    GD.Print("GridContainer megtalálva, slotok száma: " + grid.GetChildCount());

    for (int i = 0; i < grid.GetChildCount(); i++)
    {
        var slot = grid.GetChild(i);
        // Itt fontos: a te fádban a slot alatt 'Icon' néven van a TextureRect?
        var iconDisplay = slot.GetNodeOrNull<TextureRect>("Icon");

        if (iconDisplay != null)
        {
            if (i < Items.Count)
            {
                iconDisplay.Texture = GetTextureForItem(Items[i]);
                iconDisplay.Visible = true;
                GD.Print($"Slot{i+1} frissítve: {Items[i]}");
            }
            else
            {
                iconDisplay.Texture = null;
                iconDisplay.Visible = false;
            }
        }
    }
}

    // Ez a függvény rendeli hozzá a nevekhez a képeket
    private Texture2D GetTextureForItem(string name)
{
    if (name == "KeyPart1") return GD.Load<Texture2D>("res://kepek/kulcs-alja-torott.png");
    if (name == "KeyPart2") return GD.Load<Texture2D>("res://kepek/kulcs-kozepe-torott.png"); // Legyen egy másik ikonja
    return null;
}
}