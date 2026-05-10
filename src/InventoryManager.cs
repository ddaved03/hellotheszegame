using Godot;
using System.Collections.Generic;

public partial class InventoryManager : Control
{
    // A felvett tárgyak neveinek listája
    public static List<string> Items = new List<string>();
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

public override void _Ready()
{
    // Induláskor mindig kiürítjük a közös hátizsákot, nehogy benne maradjon valami az előző játékból
    Items.Clear();
}

    public void UpdateUI()
    {
        var grid = GetTree().Root.FindChild("GridContainer", true, false) as GridContainer;

        if (grid == null)
        {
            GD.PrintErr("!!! HIBA: Nem találom a GridContainer-t az UI-ban!");
            return;
        }

        for (int i = 0; i < grid.GetChildCount(); i++)
        {
            var slot = grid.GetChild(i);
            var iconDisplay = slot.GetNodeOrNull<TextureRect>("Icon");

            if (iconDisplay != null)
            {
                if (i < Items.Count)
                {
                    iconDisplay.Texture = GetTextureForItem(Items[i]);
                    iconDisplay.Visible = true;
                }
                else
                {
                    iconDisplay.Texture = null;
                    iconDisplay.Visible = false;
                }
            }
        }
    }

    // --- ÚJ FÜGGVÉNY: CRAFTOLÁS ---
public void CraftUniversityKey()
{
    // --- EZ A SOR FOGJA LEBUKTATNI A HIBÁT ---
    GD.Print("Gomb megnyomva! A gomb szerint ezek a tárgyak vannak a zsebben: [" + string.Join("], [", Items) + "]");

    if (Items.Contains("KeyPart1") && Items.Contains("KeyPart2") && Items.Contains("KeyPart3"))
    {
        Items.Remove("KeyPart1");
        Items.Remove("KeyPart2");
        Items.Remove("KeyPart3");
        
        AddItem("UniversityKey");
        
        GD.Print("Kulcs sikeresen lekraftolva!");

        var worldController = GetTree().Root.FindChild("World", true, false) as WorldController;
        if (worldController != null)
        {
            worldController.UpdateQuestText("Küldetés: Nyisd ki az egyetem ajtaját a kulccsal!");
        }
    }
    else
    {
        GD.Print("Nincs meg minden darab a craftoláshoz!");
    }
}

    // Ez a függvény rendeli hozzá a nevekhez a képeket
    private Texture2D GetTextureForItem(string name)
    {
        if (name == "KeyPart1") return GD.Load<Texture2D>("res://kepek/kulcs-alja-torott.png");
        if (name == "KeyPart2") return GD.Load<Texture2D>("res://kepek/kulcs-kozepe-torott.png");
        if (name == "KeyPart3") return GD.Load<Texture2D>("res://kepek/kulcs-eleje-torott.png");
        
        // --- ÚJ: A KÉSZ KULCS KÉPE ---
        // IDE ÍRD BE A KÉSZ, ÖSSZERAKOTT KULCSOD KÉPÉNEK AZ ELÉRÉSI ÚTJÁT!
        if (name == "UniversityKey") return GD.Load<Texture2D>("res://kepek/kulcs-egybe.png"); 
        
        return null;
    }
}