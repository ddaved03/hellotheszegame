using Godot;

public partial class WorldController : Node2D
{
    [Export] public NodePath PlayerPath;
    [Export] public NodePath PauseMenuPath;

    private BasePlayer _player;
    private Control _pauseMenu;

    public override void _Ready()
    {
        if (PlayerPath != null)
        {
            _player = GetNodeOrNull<BasePlayer>(PlayerPath);
        }

        if (PauseMenuPath != null)
        {
            _pauseMenu = GetNodeOrNull<Control>(PauseMenuPath);
        }

        if (_pauseMenu != null)
        {
            _pauseMenu.Visible = false;
            _pauseMenu.ProcessMode = ProcessModeEnum.WhenPaused;

            _pauseMenu.GetNode<Button>("VBoxContainer/ResumeButton").Pressed += OnResumePressed;
            _pauseMenu.GetNode<Button>("VBoxContainer/SaveButton").Pressed += OnSavePressed;
            _pauseMenu.GetNode<Button>("VBoxContainer/LoadButton").Pressed += OnLoadPressed;
            _pauseMenu.GetNode<Button>("VBoxContainer/MainMenuButton").Pressed += OnMainMenuPressed;
        }

        if (SaveSystem.LoadRequested)
        {
            SaveSystem.LoadRequested = false;
            if (_player != null)
            {
                SaveSystem.Load(_player);
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pause"))
        {
            if (_pauseMenu == null)
            {
                return;
            }

            bool openPauseMenu = !GetTree().Paused;
            if (openPauseMenu)
            {
                GetTree().Paused = true;
                _pauseMenu.Visible = true;
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
            else if (_pauseMenu.Visible)
            {
                OnResumePressed();
            }

            GetViewport().SetInputAsHandled();
        }
    }

    private void OnResumePressed()
    {
        if (_pauseMenu != null)
        {
            _pauseMenu.Visible = false;
        }

        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void OnSavePressed()
    {
        if (_player != null)
        {
            SaveSystem.Save(_player);
        }
    }

    private void OnLoadPressed()
    {
        if (_player != null)
        {
            SaveSystem.Load(_player);
        }

        OnResumePressed();
    }

    private void OnMainMenuPressed()
    {
        OnResumePressed();
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}
