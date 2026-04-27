using Godot;

public partial class TutorialItem : Area2D
{
    [Export] public string ItemName; // Pl: "KeyPart1", "KeyPart2"

    public override void _Ready()
{
    // --- LEBEGÉS ANIMÁCIÓ ---
    // Létrehozunk egy lebegést: 10 pixelt mozog fel-le
    var tween = CreateTween().SetLoops(); // Végtelen ismétlés
    tween.TweenProperty(GetNode("Sprite2D"), "position:y", -10.0f, 1.0f).AsRelative().SetTrans(Tween.TransitionType.Sine);
    tween.TweenProperty(GetNode("Sprite2D"), "position:y", 10.0f, 1.0f).AsRelative().SetTrans(Tween.TransitionType.Sine);

    BodyEntered += (body) => {
        if (body is BasePlayer player) {
            // Itt hívjuk meg az inventory-ba rakást (lásd lentebb)
            bool success = player.Inventory.AddItem(ItemName); 
            
            if (success) {
                var world = GetTree().Root.FindChild("World", true, false) as WorldController;
                if (world != null) world.OnKeyPartCollected(ItemName);
                QueueFree(); 
            }
        }
    };
}
}