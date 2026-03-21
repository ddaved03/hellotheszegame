using Godot;

public partial class MainMenuController : Control
{
    private Panel _settingsPanel;
    private Button _loadButton;

    public override void _Ready()
    {
        // Gombokra kötjük az eseményeket
        var newGameBtn = GetNode<Button>("CenterContainer/VBoxContainer/NewGameButton");
        newGameBtn.Pressed += OnNewGamePressed;

        // Load gomb csak akkor engedélyezett, ha van mentés
        _loadButton = GetNode<Button>("CenterContainer/VBoxContainer/LoadButton");
        _loadButton.Pressed += OnLoadPressed;
        if (!SaveSystem.HasSave())
        {
            _loadButton.Disabled = true;
        }

        GetNode<Button>("CenterContainer/VBoxContainer/SettingsButton").Pressed += OnSettingsPressed;
        GetNode<Button>("CenterContainer/VBoxContainer/QuitButton").Pressed += OnQuitPressed;

        _settingsPanel = GetNode<Panel>("SettingsPanel");
        _settingsPanel.Visible = false;

        GetNode<Button>("SettingsPanel/CloseSettingsButton").Pressed += () => 
        {
            _settingsPanel.Visible = false;
        };
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
            GD.Print("Nincsen mit betölteni!");
            return;
        }

        SaveSystem.LoadRequested = true;
        GetTree().ChangeSceneToFile("res://scenes/World.tscn");
    }

    private void OnSettingsPressed()
    {
        // Toggle settings panel
        _settingsPanel.Visible = !_settingsPanel.Visible;
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
