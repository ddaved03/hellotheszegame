using Godot;
using System;
using System.Collections.Generic;

public partial class MainMenuController : Control
{
    private Panel _settingsPanel;
    private Panel _loadMenuPanel;
    private VBoxContainer _saveFilesContainer;
    
    private Panel _renamePanel;
    private LineEdit _renameInput;
    private string _currentRenamingFile;

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
        GetNode<Button>("SettingsPanel/CloseSettingsButton").Pressed += () => _settingsPanel.Visible = false;

        CreateLoadMenuUI();
    }

    private void CreateLoadMenuUI()
    {
        _loadMenuPanel = new Panel();
        _loadMenuPanel.SetAnchorsPreset(LayoutPreset.FullRect); 
        _loadMenuPanel.Visible = false;
        AddChild(_loadMenuPanel);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
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
        _renamePanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _renamePanel.SelfModulate = new Color(0, 0, 0, 0.8f);
        _renamePanel.Visible = false;
        AddChild(_renamePanel);

        var renCenter = new CenterContainer();
        renCenter.SetAnchorsPreset(LayoutPreset.FullRect);
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
        GetTree().ChangeSceneToFile("res://scenes/World.tscn");
    }

    private void OnNewGamePressed()
    {
        AudioManager.Instance?.PlayUiClick();
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