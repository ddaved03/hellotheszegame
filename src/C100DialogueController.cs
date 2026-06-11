using Godot;
using System.Threading.Tasks;

public partial class C100DialogueController : Node2D
{
    private static readonly Vector2 PaaldaEntrancePosition = new(1152f, 1280f);
    private static readonly Vector2 PaaldaAislePosition = new(1152f, 285f);
    private static readonly Vector2 PaaldaDeskApproachPosition = new(540f, 285f);
    private static readonly Vector2 PaaldaDeskPosition = new(540f, 315f);
    private static readonly Vector2 PlayerDoorWaitPosition = new(1398f, 360f);

    private BasePlayer _player;
    private Camera2D _playerCamera;
    private PaaldaPatrol _paalda;
    private Sprite2D _paaldaSprite;
    private CanvasLayer _dialogueCanvas;
    private Label _dialogueLabel;
    private Control _pauseMenu;
    private bool _isDialogueOpen;
    private bool _hasShownDialogue;
    private bool _introCutsceneRunning = true;
    private string _playerCutsceneIdleAnimation = "idle_side";

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        AudioManager.Instance?.PlayC100Theme();

        _player = GetNodeOrNull<BasePlayer>("Player");
        _playerCamera = GetNodeOrNull<Camera2D>("Player/Camera2D");
        _paalda = GetNodeOrNull<PaaldaPatrol>("Paalda");
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

        CallDeferred(nameof(StartIntroCutscene));
    }

    public override void _Process(double delta)
    {
        if (_introCutsceneRunning)
        {
            return;
        }

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
        if (_introCutsceneRunning)
        {
            GetViewport().SetInputAsHandled();
            return;
        }

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

    private async void StartIntroCutscene()
    {
        if (_paalda == null)
        {
            _introCutsceneRunning = false;
            OpenDialogue();
            return;
        }

        SetPlayerControl(false);

        _paalda.SetPhysicsProcess(false);
        _paalda.GlobalPosition = PaaldaEntrancePosition;
        _paalda.FaceDirection(Vector2.Up);

        if (_playerCamera != null)
        {
            _playerCamera.GlobalPosition = PaaldaEntrancePosition;
        }

        await ToSignal(GetTree().CreateTimer(0.35f), SceneTreeTimer.SignalName.Timeout);

        var playerStartPosition = _player?.GlobalPosition ?? Vector2.Zero;
        var paaldaEntranceWalk = MovePaaldaTo(PaaldaAislePosition, 6.0f);

        await ToSignal(GetTree().CreateTimer(3.35f), SceneTreeTimer.SignalName.Timeout);
        await MovePlayerTo(PlayerDoorWaitPosition, 1.45f);
        await paaldaEntranceWalk;

        await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
        var paaldaDeskWalk = MovePaaldaTo(PaaldaDeskApproachPosition, 3.8f);
        await ToSignal(GetTree().CreateTimer(1.2f), SceneTreeTimer.SignalName.Timeout);
        await MovePlayerTo(playerStartPosition, 1.0f);
        await paaldaDeskWalk;

        await MovePaaldaTo(PaaldaDeskPosition, 0.7f);

        _paalda.FaceDirection(Vector2.Down);
        await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);

        await ReturnCameraToPlayer();

        SetPlayerControl(true);
        _introCutsceneRunning = false;
        OpenDialogue();
    }

    private async Task MovePaaldaTo(Vector2 targetPosition, float duration)
    {
        if (_paalda == null)
        {
            return;
        }

        Vector2 direction = targetPosition - _paalda.GlobalPosition;
        _paalda.FaceDirection(direction);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_paalda, "global_position", targetPosition, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);

        if (_playerCamera != null)
        {
            tween.TweenProperty(_playerCamera, "global_position", targetPosition, duration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
        }

        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async Task MovePlayerTo(Vector2 targetPosition, float duration)
    {
        if (_player == null)
        {
            return;
        }

        float elapsed = 0.0f;
        Vector2 startPosition = _player.GlobalPosition;

        while (elapsed < duration)
        {
            float delta = (float)GetProcessDeltaTime();
            elapsed += delta;

            float t = Mathf.Clamp(elapsed / duration, 0.0f, 1.0f);
            float easedT = t * t * (3.0f - 2.0f * t);
            Vector2 nextPosition = startPosition.Lerp(targetPosition, easedT);
            Vector2 direction = nextPosition - _player.GlobalPosition;

            PlayPlayerWalk(direction);
            _player.GlobalPosition = nextPosition;

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        _player.GlobalPosition = targetPosition;
        PlayPlayerIdle();
    }

    private void PlayPlayerWalk(Vector2 direction)
    {
        var sprite = _player?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (sprite == null || direction == Vector2.Zero)
        {
            return;
        }

        string baseDir;
        if (Mathf.Abs(direction.Y) > Mathf.Abs(direction.X))
        {
            baseDir = direction.Y < 0.0f ? "back" : "front";
        }
        else
        {
            baseDir = "side";
            sprite.FlipH = direction.X < 0.0f;
        }

        _playerCutsceneIdleAnimation = "idle_" + baseDir;

        if (baseDir != "side")
        {
            sprite.FlipH = false;
        }

        string walkAnim = "walk_" + baseDir;
        if (sprite.SpriteFrames.HasAnimation(walkAnim) && sprite.Animation != walkAnim)
        {
            sprite.Play(walkAnim);
        }
    }

    private void PlayPlayerIdle()
    {
        var sprite = _player?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (sprite == null)
        {
            return;
        }

        if (sprite.SpriteFrames.HasAnimation(_playerCutsceneIdleAnimation))
        {
            sprite.Play(_playerCutsceneIdleAnimation);
        }
    }

    private async Task ReturnCameraToPlayer()
    {
        if (_player == null || _playerCamera == null)
        {
            return;
        }

        var tween = CreateTween();
        tween.TweenProperty(_playerCamera, "global_position", _player.GlobalPosition, 0.75f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
        _playerCamera.Position = Vector2.Zero;
    }

    private void SetPlayerControl(bool enabled)
    {
        if (_player == null)
        {
            return;
        }

        _player.SetPhysicsProcess(enabled);
        _player.SetProcessInput(enabled);
        _player.SetProcessUnhandledInput(enabled);
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
