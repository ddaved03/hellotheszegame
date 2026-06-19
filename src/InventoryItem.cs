using Godot;

public partial class InventoryItem
{
    public string Name { get; set; }
    public Texture2D Icon { get; set; }

    public InventoryItem(string name, Texture2D icon)
    {
        Name = name;
        Icon = icon;
    }
}
