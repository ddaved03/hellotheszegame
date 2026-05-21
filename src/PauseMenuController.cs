using Godot;
using System;

public partial class PauseMenuController : Node
{
    [Export] public NodePath PlayerPath;
    [Export] public NodePath PauseMenuPath;

    private BasePlayer _player;
    private Control _pauseMenu;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _player = PlayerPath != null ? GetNodeOrNull<BasePlayer>(PlayerPath) : GetNodeOrNull<BasePlayer>("Player");
        _pauseMenu = PauseMenuPath != null ? GetNodeOrNull<Control>(PauseMenuPath) : GetNodeOrNull<Control>("CanvasLayer/PauseMenu");

        if (_pauseMenu == null)
        {
            GD.PrintErr("PauseMenuController: nincs PauseMenu node beállítva.");
            return;
        }

        _pauseMenu.Visible = false;
        _pauseMenu.ProcessMode = ProcessModeEnum.WhenPaused;
        _pauseMenu.GetNode<Button>("VBoxContainer/ResumeButton").Pressed += OnResumePressed;
        _pauseMenu.GetNode<Button>("VBoxContainer/SaveButton").Pressed += OnSavePressed;
        _pauseMenu.GetNode<Button>("VBoxContainer/LoadButton").Pressed += OnLoadPressed;
        _pauseMenu.GetNode<Button>("VBoxContainer/MainMenuButton").Pressed += OnMainMenuPressed;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("pause"))
        {
            return;
        }

        if (!GetTree().Paused)
        {
            OpenPauseMenu();
        }
        else if (_pauseMenu != null && _pauseMenu.Visible)
        {
            OnResumePressed();
        }

        GetViewport().SetInputAsHandled();
    }

    private void OpenPauseMenu()
    {
        GetTree().Paused = true;
        if (_pauseMenu != null)
        {
            _pauseMenu.Visible = true;
        }
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void OnResumePressed()
    {
        AudioManager.Instance?.PlayUiClick();
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
