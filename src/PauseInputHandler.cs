using Godot;
using System;

public partial class PauseInputHandler : Node
{
    public event Action PausePressed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("pause")) return;

        PausePressed?.Invoke();
        GetViewport().SetInputAsHandled();
    }
}
