using Godot;
using System;

[GlobalClass]

public partial class FirstNode : Node
{
	[Export]

	int health = 100;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GD.Print("FirstNode is ready with health: " + health);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
