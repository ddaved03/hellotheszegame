using Godot;
using System;
using System.Collections.Generic;

public partial class PauseLoadMenu : Control
{
    private VBoxContainer _saveFilesContainer;
    private Action<string> _loadSelected;
    private Action _back;

    public void Setup(Action<string> loadSelected, Action back)
    {
        _loadSelected = loadSelected;
        _back = back;

        ProcessMode = ProcessModeEnum.WhenPaused;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
        ZIndex = 1000;
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.75f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -320f;
        panel.OffsetTop = -230f;
        panel.OffsetRight = 320f;
        panel.OffsetBottom = 230f;
        panel.GrowHorizontal = GrowDirection.Both;
        panel.GrowVertical = GrowDirection.Both;
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        panel.AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 12);
        margin.AddChild(layout);

        var title = new Label
        {
            Text = "Mentett játékok betöltése",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        layout.AddChild(title);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(560f, 320f),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        layout.AddChild(scroll);

        _saveFilesContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _saveFilesContainer.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_saveFilesContainer);

        var backButton = new Button { Text = "Vissza" };
        backButton.Pressed += Close;
        layout.AddChild(backButton);

        Visible = false;
    }

    public void Open()
    {
        RefreshSaveFiles();
        Visible = true;
    }

    public void Close()
    {
        Visible = false;
        _back?.Invoke();
    }

    private void RefreshSaveFiles()
    {
        foreach (Node child in _saveFilesContainer.GetChildren())
        {
            _saveFilesContainer.RemoveChild(child);
            child.QueueFree();
        }

        List<string> saves = SaveSystem.GetSaveFiles();
        saves.Sort((a, b) => SaveSystem.GetSaveDate(b).CompareTo(SaveSystem.GetSaveDate(a)));

        if (saves.Count == 0)
        {
            _saveFilesContainer.AddChild(new Label
            {
                Text = "Nincs betölthető mentés.",
                HorizontalAlignment = HorizontalAlignment.Center
            });
            return;
        }

        foreach (string saveFile in saves)
        {
            DateTime date = SaveSystem.GetSaveDate(saveFile);
            var loadButton = new Button
            {
                Text = $"{saveFile.Replace(".json", "")}  ({date:yyyy.MM.dd. HH:mm})",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            loadButton.Pressed += () => _loadSelected?.Invoke(saveFile);
            _saveFilesContainer.AddChild(loadButton);
        }
    }
}
