using Godot;
using System;
using System.Collections.Generic;

public partial class MainMenuController : Control
{
    private static readonly Texture2D MenuBackgroundTexture = GD.Load<Texture2D>("res://src/level-hattér.png");
    private Panel _settingsPanel;
    private Panel _loadMenuPanel;
    private VBoxContainer _saveFilesContainer;
    
    private Panel _renamePanel;
    private LineEdit _renameInput;
    private string _currentRenamingFile;
    private Panel _newGamePanel;
    private LineEdit _newGameNameInput;
    private HSlider _masterVolumeSlider;
    private HSlider _musicVolumeSlider;

    public override void _Ready()
    {
        var newGameBtn = GetNode<Button>("CenterContainer/VBoxContainer/NewGameButton");
        newGameBtn.Pressed += OnNewGamePressed;

        var loadButton = GetNode<Button>("CenterContainer/VBoxContainer/LoadButton");
        loadButton.Pressed += OnLoadMenuPressed; 
        
        if (!SaveSystem.HasAnySave()) loadButton.Disabled = true;

        GetNode<Button>("CenterContainer/VBoxContainer/SettingsButton").Pressed += OnSettingsPressed;
        GetNode<Button>("CenterContainer/VBoxContainer/QuitButton").Pressed += OnQuitPressed;

        _settingsPanel = GetNode<Panel>("SettingsPanel");
        _settingsPanel.Visible = false;
        GetNode<Button>("SettingsPanel/CenterContainer/VBoxContainer/CloseSettingsButton").Pressed += () => _settingsPanel.Visible = false;

        SetupAudioSettingsControls();

        CreateLoadMenuUI();
        CreateNewGamePanel();
    }

    private void SetupAudioSettingsControls()
    {
        var settingsVBox = GetNode<VBoxContainer>("SettingsPanel/CenterContainer/VBoxContainer");
        var closeButton = GetNode<Button>("SettingsPanel/CenterContainer/VBoxContainer/CloseSettingsButton");

        var audioTitle = new Label();
        audioTitle.Text = "Hang be\u00e1ll\u00edt\u00e1sok";
        audioTitle.HorizontalAlignment = HorizontalAlignment.Center;
        settingsVBox.AddChild(audioTitle);

        var masterRow = new VBoxContainer();
        masterRow.AddThemeConstantOverride("separation", 4);
        settingsVBox.AddChild(masterRow);

        var masterLabel = new Label();
        masterLabel.Text = "F\u0151hanger\u0151";
        masterRow.AddChild(masterLabel);

        _masterVolumeSlider = new HSlider();
        _masterVolumeSlider.MinValue = 0;
        _masterVolumeSlider.MaxValue = 1;
        _masterVolumeSlider.Step = 0.01;
        _masterVolumeSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
        masterRow.AddChild(_masterVolumeSlider);

        var musicRow = new VBoxContainer();
        musicRow.AddThemeConstantOverride("separation", 4);
        settingsVBox.AddChild(musicRow);

        var musicLabel = new Label();
        musicLabel.Text = "Zene hanger\u0151";
        musicRow.AddChild(musicLabel);

        _musicVolumeSlider = new HSlider();
        _musicVolumeSlider.MinValue = 0;
        _musicVolumeSlider.MaxValue = 1;
        _musicVolumeSlider.Step = 0.01;
        _musicVolumeSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _musicVolumeSlider.ValueChanged += OnMusicVolumeChanged;
        musicRow.AddChild(_musicVolumeSlider);

        settingsVBox.MoveChild(closeButton, settingsVBox.GetChildCount() - 1);

        _masterVolumeSlider.Value = AudioManager.Instance?.MasterVolume ?? 1f;
        _musicVolumeSlider.Value = AudioManager.Instance?.MusicVolume ?? 1f;
    }

    private void OnMasterVolumeChanged(double value)
    {
        AudioManager.Instance?.SetMasterVolume((float)value);
    }

    private void OnMusicVolumeChanged(double value)
    {
        AudioManager.Instance?.SetMusicVolume((float)value);
    }

    private void CreateNewGamePanel()
    {
        _newGamePanel = new Panel();
        _newGamePanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _newGamePanel.Visible = false;
        AddChild(_newGamePanel);

        var background = new TextureRect();
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        background.Texture = MenuBackgroundTexture;
        background.StretchMode = TextureRect.StretchModeEnum.Scale;
        _newGamePanel.AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _newGamePanel.AddChild(center);

        var dialogPanel = new Panel();
        dialogPanel.CustomMinimumSize = new Vector2(720, 500);
        center.AddChild(dialogPanel);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        dialogPanel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.CustomMinimumSize = new Vector2(664, 456);
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        var title = new Label();
        title.Text = "Új játék - Add meg a neved";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Set("theme_override_colors/font_color", new Color(1,1,1,1));
        vbox.AddChild(title);

        _newGameNameInput = new LineEdit();
        _newGameNameInput.PlaceholderText = "Név...";
        _newGameNameInput.CustomMinimumSize = new Vector2(400, 40);
        _newGameNameInput.Set("theme_override_colors/font_color", new Color(1,1,1,1));
        _newGameNameInput.Set("theme_override_colors/font_placeholder_color", new Color(1,1,1,0.65f));
        vbox.AddChild(_newGameNameInput);

        var info = new Label();
        info.Text = "WASD: mozgás, Bal egér: támadás, E: inventory, O: ajtó nyitás\n\nTörténet: ";
        info.Text += "Egy átlagos egyetemi napnak indul, de amikor a főhős megérkezik a kampuszra, valami nagyon nincs rendben. Az épület környékét zombik lepték el, a bejárathoz vezető kulcs darabokra tört, és a túléléshez össze kell gyűjteni minden részét. A játékosnak át kell verekednie magát az udvaron, meg kell találnia a kulcsdarabokat, majd bejutnia az egyetemre. Odabent sötét folyosók, elromlott lift, földrengés és újabb veszélyek várják. A cél: eljutni a C100-as terembe, ahol az utolsó próbatétel nem fegyverrel, hanem tudással dől el.";
        info.Set("autowrap_mode", 2);
        info.Set("theme_override_colors/font_color", new Color(1,1,1,1));
        info.CustomMinimumSize = new Vector2(640, 270);
        info.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(info);

        var hbox = new HBoxContainer();
        vbox.AddChild(hbox);

        var startBtn = new Button();
        startBtn.Text = "Kezdés";
        startBtn.Set("theme_override_colors/font_color", new Color(1,1,1,1));
        startBtn.Pressed += ConfirmNewGame;
        startBtn.Disabled = true;
        startBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(startBtn);

        var cancelBtn = new Button();
        cancelBtn.Text = "Mégse";
        cancelBtn.Set("theme_override_colors/font_color", new Color(1,1,1,1));
        cancelBtn.Pressed += () => _newGamePanel.Visible = false;
        cancelBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(cancelBtn);

        // Név nélkül nem indítható új játék.
        _newGameNameInput.TextChanged += (string newText) => { startBtn.Disabled = string.IsNullOrWhiteSpace(newText); };
    }

    private void CreateLoadMenuUI()
    {
        _loadMenuPanel = new Panel();
        _loadMenuPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect); 
        _loadMenuPanel.Visible = false;
        AddChild(_loadMenuPanel);

        var background = new TextureRect();
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        background.Texture = MenuBackgroundTexture;
        background.StretchMode = TextureRect.StretchModeEnum.Scale;
        _loadMenuPanel.AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _loadMenuPanel.AddChild(center);

        var vbox = new VBoxContainer();
        center.AddChild(vbox);

        var title = new Label();
        title.Text = "Mentett játékok betöltése";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(450, 400);
        vbox.AddChild(scroll);

        _saveFilesContainer = new VBoxContainer();
        _saveFilesContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_saveFilesContainer);

        var closeBtn = new Button();
        closeBtn.Text = "Vissza a menübe";
        closeBtn.Pressed += () => _loadMenuPanel.Visible = false;
        vbox.AddChild(closeBtn);

        _renamePanel = new Panel();
        _renamePanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _renamePanel.Visible = false;
        AddChild(_renamePanel);

        var renameBackground = new TextureRect();
        renameBackground.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        renameBackground.Texture = MenuBackgroundTexture;
        renameBackground.StretchMode = TextureRect.StretchModeEnum.Scale;
        _renamePanel.AddChild(renameBackground);

        var renCenter = new CenterContainer();
        renCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _renamePanel.AddChild(renCenter);

        var renVbox = new VBoxContainer();
        renCenter.AddChild(renVbox);

        var renLabel = new Label();
        renLabel.Text = "Új név megadása:";
        renVbox.AddChild(renLabel);

        _renameInput = new LineEdit();
        _renameInput.CustomMinimumSize = new Vector2(250, 40);
        renVbox.AddChild(_renameInput);

        var renHbox = new HBoxContainer();
        renVbox.AddChild(renHbox);

        var renOkBtn = new Button();
        renOkBtn.Text = "Mentés";
        renOkBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        renOkBtn.Pressed += ConfirmRename;
        renHbox.AddChild(renOkBtn);

        var renCancelBtn = new Button();
        renCancelBtn.Text = "Mégse";
        renCancelBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        renCancelBtn.Pressed += () => _renamePanel.Visible = false;
        renHbox.AddChild(renCancelBtn);
    }

    private void OnLoadMenuPressed()
    {
        AudioManager.Instance?.PlayUiClick();
        _settingsPanel.Visible = false; 
        _loadMenuPanel.Visible = true;

        foreach (Node child in _saveFilesContainer.GetChildren())
        {
            child.QueueFree();
        }

        List<string> saves = SaveSystem.GetSaveFiles();
        
        saves.Sort((a, b) => SaveSystem.GetSaveDate(b).CompareTo(SaveSystem.GetSaveDate(a)));

        foreach (string saveFile in saves)
        {
            var row = new HBoxContainer();
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _saveFilesContainer.AddChild(row);

            Button loadBtn = new Button();
            loadBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            DateTime date = SaveSystem.GetSaveDate(saveFile);
            string displayName = saveFile.Replace(".json", "");
            loadBtn.Text = $"{displayName}  ({date:yyyy.MM.dd. HH:mm})";
            loadBtn.Pressed += () => LoadSelectedGame(saveFile);
            row.AddChild(loadBtn);

            Button renameBtn = new Button();
            renameBtn.Text = "Átnevez";
            renameBtn.Pressed += () => OpenRenamePanel(saveFile);
            row.AddChild(renameBtn);

            Button deleteBtn = new Button();
            deleteBtn.Text = "Töröl";
            deleteBtn.SelfModulate = new Color(1, 0.5f, 0.5f);
            deleteBtn.Pressed += () => DeleteGame(saveFile);
            row.AddChild(deleteBtn);
        }
    }

    private void OpenRenamePanel(string fileName)
    {
        AudioManager.Instance?.PlayUiClick();
        _currentRenamingFile = fileName;
        _renameInput.Text = fileName.Replace(".json", "");
        _renamePanel.Visible = true;
    }

    private void ConfirmRename()
    {
        AudioManager.Instance?.PlayUiClick();
        string newName = _renameInput.Text.Trim();
        if (!string.IsNullOrEmpty(newName))
        {
            SaveSystem.RenameSave(_currentRenamingFile, newName);
            _renamePanel.Visible = false;
            OnLoadMenuPressed();
        }
    }

    private void DeleteGame(string fileName)
    {
        AudioManager.Instance?.PlayUiClick();
        SaveSystem.DeleteSave(fileName);
        
        if (!SaveSystem.HasAnySave())
        {
            GetNode<Button>("CenterContainer/VBoxContainer/LoadButton").Disabled = true;
            _loadMenuPanel.Visible = false;
        }
        else
        {
            OnLoadMenuPressed();
        }
    }

    private void LoadSelectedGame(string fileName)
    {
        AudioManager.Instance?.PlayUiClick();
        SaveSystem.CurrentSaveFileName = fileName; 
        SaveSystem.LoadRequested = true;
        GetTree().ChangeSceneToFile(SaveSystem.GetSavedScenePath(fileName));
    }

    private void OnNewGamePressed()
    {
        AudioManager.Instance?.PlayUiClick();
        _newGamePanel.Visible = true;
    }

    private void ConfirmNewGame()
    {
        AudioManager.Instance?.PlayUiClick();
        string name = _newGameNameInput.Text.Trim();
        if (string.IsNullOrEmpty(name)) name = "Player";

        SaveSystem.PlayerName = name;
        SaveSystem.FirstRun = true;
        SaveSystem.LoadRequested = false;
        SaveSystem.SetNewSaveFile();
        InventoryManager.Items.Clear();

        GetTree().ChangeSceneToFile("res://scenes/World.tscn");
    }

    private void OnSettingsPressed()
    {
        AudioManager.Instance?.PlayUiClick();
        _loadMenuPanel.Visible = false; 
        _settingsPanel.Visible = !_settingsPanel.Visible;
    }

    private void OnQuitPressed()
    {
        AudioManager.Instance?.PlayUiClick();
        GetTree().Quit();
    }
}
