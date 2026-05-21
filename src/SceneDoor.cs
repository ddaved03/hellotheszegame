using Godot;

public partial class SceneDoor : Area2D
{
    [Export(PropertyHint.File, "*.tscn")]
    public string TargetScene { get; set; } = "";

    private bool _isChangingScene;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_isChangingScene || body is not BasePlayer || string.IsNullOrWhiteSpace(TargetScene))
        {
            return;
        }

        _isChangingScene = true;
        GetTree().ChangeSceneToFile(TargetScene);
    }
}
