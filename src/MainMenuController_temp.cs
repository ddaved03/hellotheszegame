using Godot;

public partial class MainMenuController : Control
{
    private Panel _settingsPanel;
    private Button _loadButton;

    public override void _Ready()
    {
        GetNode<Button>("CenterContainer/VBoxContainer/NewGameButton").Pressed += OnNewGamePressed;

        _loadButton = GetNode<Button>("CenterContainer/VBoxContainer/LoadButton");
        _loadButton.Pressed += OnLoadPressed;
        _loadButton.Disabled = !SaveSystem.HasSave();

        GetNode<Button>("CenterContainer/VBoxContainer/SettingsButton").Pressed += OnSettingsPressed;
        GetNode<Button>("CenterContainer/VBoxContainer/QuitButton").Pressed += OnQuitPressed;

        _settingsPanel = GetNode<Panel>("SettingsPanel");
        _settingsPanel.Visible = false;

        GetNode<Button>("SettingsPanel/CloseSettingsButton").Pressed += () => _settingsPanel.Visible = false;
    }

    private void OnNewGamePressed()
    {
        SaveSystem.LoadRequested = false;
        GetTree().ChangeSceneToFile("res://scenes/World.tscn");
    }

    private void OnLoadPressed()
    {
        if (!SaveSystem.HasSave())
        {
            GD.Print("Nincs mentes, amit be lehetne tolteni.");
            return;
        }

        SaveSystem.LoadRequested = true;
        GetTree().ChangeSceneToFile("res://scenes/World.tscn");
    }

    private void OnSettingsPressed()
    {
        _settingsPanel.Visible = !_settingsPanel.Visible;
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}

