using Godot;

public partial class C100DialogueController : Node2D
{
    private Sprite2D _paaldaSprite;
    private CanvasLayer _dialogueCanvas;
    private Label _dialogueLabel;
    private Control _pauseMenu;
    private bool _isDialogueOpen;
    private bool _hasShownDialogue;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _paaldaSprite = GetNodeOrNull<Sprite2D>("Paalda/Sprite2D");
        _dialogueCanvas = GetNodeOrNull<CanvasLayer>("DialogueCanvas");
        _dialogueLabel = GetNodeOrNull<Label>("DialogueCanvas/DialogueRoot/TextBox/DialogueText");
        _pauseMenu = GetNodeOrNull<Control>("PauseCanvas/PauseMenu");

        if (_dialogueCanvas != null)
        {
            _dialogueCanvas.Visible = false;
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
    }

    public override void _Process(double delta)
    {
        if (_hasShownDialogue || _isDialogueOpen)
        {
            return;
        }

        if (IsPaaldaFullyVisible())
        {
            OpenDialogue();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pause"))
        {
            TogglePauseMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_isDialogueOpen)
        {
            return;
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.PhysicalKeycode == Key.Q)
        {
            CloseDialogue();
            GetViewport().SetInputAsHandled();
        }
    }

    private bool IsPaaldaFullyVisible()
    {
        if (_paaldaSprite == null || !IsInstanceValid(_paaldaSprite))
        {
            return false;
        }

        var spriteRect = _paaldaSprite.GetRect();
        var transform = _paaldaSprite.GetGlobalTransformWithCanvas();
        var viewportSize = GetViewport().GetVisibleRect().Size;

        Vector2[] corners =
        {
            spriteRect.Position,
            new Vector2(spriteRect.End.X, spriteRect.Position.Y),
            spriteRect.End,
            new Vector2(spriteRect.Position.X, spriteRect.End.Y)
        };

        const float margin = 2.0f;
        foreach (var corner in corners)
        {
            var screenPoint = transform * corner;
            if (screenPoint.X < margin || screenPoint.Y < margin || screenPoint.X > viewportSize.X - margin || screenPoint.Y > viewportSize.Y - margin)
            {
                return false;
            }
        }

        return true;
    }

    private void OpenDialogue()
    {
        if (_dialogueCanvas == null)
        {
            return;
        }

        if (_dialogueLabel != null)
        {
            _dialogueLabel.Text = "\u25b8 Szia, m\u00e1r v\u00e1rtalak!";
        }

        _hasShownDialogue = true;
        _isDialogueOpen = true;
        _dialogueCanvas.Visible = true;
        GetTree().Paused = true;
    }

    private void CloseDialogue()
    {
        _isDialogueOpen = false;

        if (_dialogueCanvas != null)
        {
            _dialogueCanvas.Visible = false;
        }

        GetTree().Paused = false;
    }

    private void TogglePauseMenu()
    {
        if (_pauseMenu == null)
        {
            return;
        }

        if (GetTree().Paused && !_pauseMenu.Visible)
        {
            return;
        }

        if (_pauseMenu.Visible)
        {
            OnResumePressed();
            return;
        }

        _pauseMenu.Visible = true;
        GetTree().Paused = true;
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
        var player = GetTree().GetFirstNodeInGroup("Player") as BasePlayer;
        if (player != null)
        {
            SaveSystem.Save(player);
        }
    }

    private void OnLoadPressed()
    {
        var player = GetTree().GetFirstNodeInGroup("Player") as BasePlayer;
        if (player != null)
        {
            SaveSystem.Load(player);
        }

        OnResumePressed();
    }

    private void OnMainMenuPressed()
    {
        OnResumePressed();
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}
