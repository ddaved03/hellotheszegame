using Godot;
using System.Threading.Tasks;

// A C100-as terem dialógusainak és kvíz kérdéseinek vezérlője
public partial class C100DialogueController : Node2D
{
    private const int DialogueSlideCount = 3;
    // Helytelen válasz esetén kapott sebzés mértéke
    private const int WrongAnswerDamage = 20;

    // A kvíz kérdéseinek tömbje
    private static readonly string[] QuizQuestions =
    {
        "Az NT \u00fctemez\u0151je a kernel m\u00f3dban, egy darab \u00fctemez\u0151 modulban tal\u00e1lhat\u00f3.",
        "A t\u00e1rol\u00f3 m\u00e9ret\u00e9t megn\u00f6velve a FAT m\u00e9rete nem ar\u00e1nyosan n\u00f6vekszik.",
        "A CISC architekt\u00far\u00e1t nagy utas\u00edt\u00e1ssz\u00e1m jellemzi.",
        "A RAID0 eset\u00e9ben az \u00edr\u00e1si \u00e9s olvas\u00e1si sebess\u00e9g is n\u00f6vekszik.",
        "A NAND technol\u00f3gia csak laponk\u00e9nt c\u00edmezhet\u0151.",
        "Az LCD kijelz\u0151 m\u0171k\u00f6d\u00e9se a f\u00e9ny polariz\u00e1ci\u00f3j\u00e1n alapul.",
        "A gyors\u00edt\u00f3 t\u00e1r gyorsabb, mint az egyik hozz\u00e1 kapcsol\u00f3d\u00f3, gyors\u00edtand\u00f3 t\u00e1r.",
        "A Bubble Jet a Canon technol\u00f3gi\u00e1ja.",
        "Kettes sz\u00e1mrendszerben egy t\u00f6rt \u00e9rt\u00e9ke nem minden esetben fejezhet\u0151 ki pontosan.",
        "A f\u00fcggetlen folyamatok jellemz\u0151en aszinkron m\u0171k\u00f6d\u00e9s\u0171ek.",
        "A PAE 32 c\u00edmbit helyett 48 c\u00edmbitet haszn\u00e1l.",
        "Az oper\u00e1ci\u00f3s rendszer k\u00f6zvetlen\u00fcl kommunik\u00e1l a hardverrel.",
        "Egy Windows NT sz\u00e1lat ETHREAD reprezent\u00e1l.",
        "A NOR technol\u00f3gia b\u00e1jtonk\u00e9nti el\u00e9r\u00e9s\u0171.",
        "A 16-os sz\u00e1mrendszerben 3 helyi\u00e9rt\u00e9ken 4096 \u00e9rt\u00e9k \u00e1br\u00e1zolhat\u00f3.",
        "Az NT virtu\u00e1lis t\u00e1rkezel\u00e9se lapszervez\u00e9st haszn\u00e1l.",
        "A m\u00e1trixnyomtat\u00f3 legel\u0151ny\u00f6sebb tulajdons\u00e1ga a t\u00f6bb p\u00e9ld\u00e1ny egyidej\u0171 nyomtat\u00e1sa.",
        "A FAT egy l\u00e1ncolt lista k\u00f6zponti l\u00e1ncelem t\u00e1bl\u00e1val.",
        "A MAR \u00edrni vagy olvasni k\u00edv\u00e1nt mem\u00f3ria c\u00edm\u00e9t tartalmazza.",
        "A RAID1 t\u00fckr\u00f6z\u00e9st jelent.",
        "Az FCFS preempt\u00edv algoritmus.",
        "A szab\u00e1lyz\u00e1s z\u00e1rt hat\u00e1sl\u00e1nc\u00fa folyamat.",
        "A deklarat\u00edv programoz\u00e1s k\u00e9t f\u0151 ir\u00e1nya a logikai \u00e9s a funkcion\u00e1lis programoz\u00e1s.",
        "A PAE 32 c\u00edmbit helyett 36-ot haszn\u00e1l.",
        "Az NT \u00fctemez\u00e9se sz\u00e1l alap\u00fa, priorit\u00e1svez\u00e9relt \u00e9s preempt\u00edv."
    };

    private static readonly bool[] QuizAnswers =
    {
        false,
        false,
        true,
        true,
        true,
        true,
        true,
        true,
        true,
        true,
        false,
        false,
        true,
        true,
        true,
        true,
        true,
        true,
        true,
        true,
        false,
        true,
        true,
        true,
        true
    };

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
    private CanvasLayer _quizCanvas;
    private Label _quizProgressLabel;
    private Label _quizQuestionLabel;
    private Label _quizFeedbackLabel;
    private ProgressBar _quizHealthBar;
    private Label _quizHealthLabel;
    private bool _isDialogueOpen;
    private bool _isQuizOpen;
    private bool _isEndingDialogueOpen;
    private bool _hasShownDialogue;
    private bool _introCutsceneRunning = true;
    private int _dialogueSlideIndex;
    private int _quizQuestionIndex;
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
            if (_isQuizOpen || _isEndingDialogueOpen)
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            TogglePauseMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (_isEndingDialogueOpen && keyEvent.PhysicalKeycode == Key.Enter)
            {
                ShowGameCompleteMessage();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.PhysicalKeycode == Key.O && !_isQuizOpen && _hasShownDialogue && IsPaaldaFullyVisible())
            {
                RestartDialogue();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_isDialogueOpen && keyEvent.PhysicalKeycode == Key.Q)
            {
                CloseDialogue();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_isDialogueOpen && keyEvent.PhysicalKeycode == Key.Enter)
            {
                ShowNextDialogueSlide();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (!_isDialogueOpen)
        {
            return;
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

        ResetDialogueText();

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

    private void RestartDialogue()
    {
        if (_dialogueCanvas == null)
        {
            return;
        }

        ResetDialogueText();
        _isDialogueOpen = true;
        _dialogueCanvas.Visible = true;
        GetTree().Paused = true;
    }

    private void ResetDialogueText()
    {
        _dialogueSlideIndex = 0;
        ShowDialogueSlide();
    }

    private void ShowNextDialogueSlide()
    {
        if (_dialogueSlideIndex < DialogueSlideCount - 1)
        {
            _dialogueSlideIndex++;
            ShowDialogueSlide();
            return;
        }

        CloseDialogue();
        StartQuiz();
    }

    private void ShowDialogueSlide()
    {
        if (_dialogueLabel != null)
        {
            _dialogueLabel.Text = GetDialogueSlideText(_dialogueSlideIndex);
        }
    }

    private string GetDialogueSlideText(int slideIndex)
    {
        return slideIndex switch
        {
            0 => "Dave\n\nK\u00e9szen \u00e1llsz a megm\u00e9rettet\u00e9sre?",
            1 => $"{GetPlayerDisplayName()}\n\nPersze, hogyne!",
            2 => "Dave\n\nJ\u00f3lvan akkor, n\u00e9zz\u00fck mit tudsz!",
            _ => string.Empty
        };
    }

    private string GetPlayerDisplayName()
    {
        return string.IsNullOrWhiteSpace(SaveSystem.PlayerName) ? "Player" : SaveSystem.PlayerName;
    }

    private void StartQuiz()
    {
        _quizQuestionIndex = 0;
        EnsureQuizUi();
        _isQuizOpen = true;

        if (_quizCanvas != null)
        {
            _quizCanvas.Visible = true;
        }

        ShowQuizQuestion();
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void EnsureQuizUi()
    {
        if (_quizCanvas != null)
        {
            return;
        }

        _quizCanvas = new CanvasLayer
        {
            Name = "QuizCanvas",
            Layer = 110,
            ProcessMode = ProcessModeEnum.Always
        };
        AddChild(_quizCanvas);

        var root = new Control
        {
            Name = "QuizRoot",
            ProcessMode = ProcessModeEnum.Always
        };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _quizCanvas.AddChild(root);

        var shade = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.58f)
        };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(shade);

        var panel = new Panel
        {
            CustomMinimumSize = new Vector2(900f, 480f)
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.OffsetLeft = -450f;
        panel.OffsetTop = -240f;
        panel.OffsetRight = 450f;
        panel.OffsetBottom = 240f;
        root.AddChild(panel);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 42);
        margin.AddThemeConstantOverride("margin_top", 34);
        margin.AddThemeConstantOverride("margin_right", 42);
        margin.AddThemeConstantOverride("margin_bottom", 34);
        panel.AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 24);
        margin.AddChild(layout);

        _quizProgressLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _quizProgressLabel.AddThemeFontSizeOverride("font_size", 28);
        layout.AddChild(_quizProgressLabel);

        var healthRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        healthRow.AddThemeConstantOverride("separation", 16);
        layout.AddChild(healthRow);

        var hpTitle = new Label
        {
            Text = "HP"
        };
        hpTitle.AddThemeFontSizeOverride("font_size", 24);
        healthRow.AddChild(hpTitle);

        _quizHealthBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(420f, 30f),
            ShowPercentage = false
        };
        _quizHealthBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.03f, 0.03f, 0.95f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        });
        _quizHealthBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = new Color(0.85f, 0.12f, 0.12f, 1f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        });
        healthRow.AddChild(_quizHealthBar);

        _quizHealthLabel = new Label
        {
            CustomMinimumSize = new Vector2(130f, 0f),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _quizHealthLabel.AddThemeFontSizeOverride("font_size", 24);
        healthRow.AddChild(_quizHealthLabel);

        _quizQuestionLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _quizQuestionLabel.AddThemeFontSizeOverride("font_size", 32);
        layout.AddChild(_quizQuestionLabel);

        var buttonRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        buttonRow.AddThemeConstantOverride("separation", 40);
        layout.AddChild(buttonRow);

        var trueButton = CreateQuizButton("Igaz");
        trueButton.Pressed += () => AnswerQuizQuestion(true);
        buttonRow.AddChild(trueButton);

        var falseButton = CreateQuizButton("Hamis");
        falseButton.Pressed += () => AnswerQuizQuestion(false);
        buttonRow.AddChild(falseButton);

        _quizFeedbackLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _quizFeedbackLabel.AddThemeFontSizeOverride("font_size", 24);
        layout.AddChild(_quizFeedbackLabel);
    }

    private Button CreateQuizButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(220f, 72f)
        };
        button.AddThemeFontSizeOverride("font_size", 30);
        return button;
    }

    private void ShowQuizQuestion()
    {
        if (_quizQuestionIndex >= QuizQuestions.Length)
        {
            FinishQuiz();
            return;
        }

        if (_quizProgressLabel != null)
        {
            _quizProgressLabel.Text = $"{_quizQuestionIndex + 1}. k\u00e9rd\u00e9s / {QuizQuestions.Length}";
        }

        if (_quizQuestionLabel != null)
        {
            _quizQuestionLabel.Text = QuizQuestions[_quizQuestionIndex];
        }

        if (_quizFeedbackLabel != null)
        {
            _quizFeedbackLabel.Text = string.Empty;
        }

        UpdateQuizHealthDisplay();
    }

    private void AnswerQuizQuestion(bool answer)
    {
        if (!_isQuizOpen || _quizQuestionIndex >= QuizQuestions.Length)
        {
            return;
        }

        if (answer == QuizAnswers[_quizQuestionIndex])
        {
            _quizQuestionIndex++;
            ShowQuizQuestion();
            return;
        }

        _player?.TakeDamage(WrongAnswerDamage);
        UpdateQuizHealthDisplay();

        if (_quizFeedbackLabel != null)
        {
            _quizFeedbackLabel.Text = $"-{WrongAnswerDamage} HP";
        }
    }

    private void UpdateQuizHealthDisplay()
    {
        if (_player == null)
        {
            return;
        }

        int currentHealth = Mathf.Max(0, _player.CurrentHealth);

        if (_quizHealthBar != null)
        {
            _quizHealthBar.MaxValue = _player.MaxHealth;
            _quizHealthBar.Value = currentHealth;
        }

        if (_quizHealthLabel != null)
        {
            _quizHealthLabel.Text = $"{currentHealth} / {_player.MaxHealth}";
        }
    }

    private void FinishQuiz()
    {
        _isQuizOpen = false;

        if (_quizCanvas != null)
        {
            _quizCanvas.Visible = false;
        }

        StartEndingDialogue();
    }

    private void StartEndingDialogue()
    {
        _ = FadeOutPaalda();
        _isEndingDialogueOpen = true;

        if (_dialogueLabel != null)
        {
            _dialogueLabel.Text = "Dave\n\nGratul\u00e1lok, fiam!";
        }

        if (_dialogueCanvas != null)
        {
            _dialogueCanvas.Visible = true;
        }

        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private async Task FadeOutPaalda()
    {
        if (_paalda == null || !IsInstanceValid(_paalda))
        {
            return;
        }

        _paalda.SetPhysicsProcess(false);
        var tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(_paalda, "modulate", new Color(1f, 1f, 1f, 0f), 1.4f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);

        await ToSignal(tween, Tween.SignalName.Finished);

        if (_paalda != null && IsInstanceValid(_paalda))
        {
            _paalda.QueueFree();
        }
    }

    private async void ShowGameCompleteMessage()
    {
        _isEndingDialogueOpen = false;

        if (_dialogueLabel != null)
        {
            _dialogueLabel.Text = "Sz\u00e9p volt! Kivitted a j\u00e1t\u00e9kot!";
        }

        if (_dialogueCanvas != null)
        {
            _dialogueCanvas.Visible = true;
        }

        await ToSignal(GetTree().CreateTimer(3.0f, true), SceneTreeTimer.SignalName.Timeout);
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
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
