using Godot;

// Az inventoryban tárolható egyedi tárgyakat reprezentáló osztály
public partial class InventoryItem
{
    // A tárgy neve, amely alapján azonosítjuk (pl. "Kulcs", "Biztosíték")
    public string Name { get; set; }
    // A tárgyhoz tartozó ikon textúrája a UI-hoz
    public Texture2D Icon { get; set; }

    // Konstruktor a tárgy nevének és ikonjának beállításához
    public InventoryItem(string name, Texture2D icon)
    {
        Name = name;
        Icon = icon;
    }
}
