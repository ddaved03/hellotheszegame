using Godot;

// Ez egy sima osztály, nem kell mögé a ": Node" vagy ": Sprite2D", 
// mert ez csak adatokat tárol (név és kép).
public partial class InventoryItem
{
    public string Name { get; set; }
    public Texture2D Icon { get; set; }

    // Egy egyszerű konstruktor, hogy könnyebb legyen létrehozni
    public InventoryItem(string name, Texture2D icon)
    {
        Name = name;
        Icon = icon;
    }
}