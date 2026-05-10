using Godot;
using System;

public partial class ElevatorDoor : StaticBody2D
{
    private GroundFloorController _controller;

    public override void _Ready()
    {
        var area = GetNodeOrNull<Area2D>("DetectionArea");
        _controller = GetTree().Root.FindChild("GroundFloor", true, false) as GroundFloorController;
        
        if (area != null)
        {
            area.BodyEntered += OnBodyEntered;
            GD.Print("SIKER: Lift ajtó eseménykezelő csatlakoztatva!");
        }
        else
        {
            GD.PrintErr("HIBA: Nem találom a DetectionArea-t az ElevatorDoor alatt!");
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is BasePlayer)
        {
            if (_controller != null)
            {
                _controller.TryUseElevator();
            }
            else
            {
                GD.PrintErr("HIBA: Nem találom a GroundFloorController-t az ElevatorDoor alatt!");
            }
        }
    }
}
