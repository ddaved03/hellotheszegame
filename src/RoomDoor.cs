using Godot;

public partial class RoomDoor : StaticBody2D
{
    [Export] public StringName DoorId = "";

    private GroundFloorController _controller;

    public override void _Ready()
    {
        var area = GetNodeOrNull<Area2D>("DetectionArea");
        _controller = GetTree().Root.FindChild("GroundFloor", true, false) as GroundFloorController;

        if (area != null)
        {
            area.BodyEntered += OnBodyEntered;
            area.BodyExited += OnBodyExited;
        }
        else
        {
            GD.PrintErr($"HIBA: Nem találom a DetectionArea-t a RoomDoor alatt! ({Name})");
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is BasePlayer)
        {
            _controller?.NotifyRoomDoorEntered(DoorId, body.GlobalPosition);
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is BasePlayer)
        {
            _controller?.NotifyRoomDoorExited(DoorId);
        }
    }
}