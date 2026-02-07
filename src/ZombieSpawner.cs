using Godot;
using System;

public partial class ZombieSpawner : Node2D
{
	// Ide kell behúznod a Zombie.tscn-t az Inspectorban!
	[Export] public PackedScene ZombieScene;
	
	// Milyen messze a spawnertől szülessenek a zombik (véletlenszerűen)
	[Export] public float SpawnRadius = 500.0f;

	public override void _Ready()
	{
		// Összekötjük a Timer "timeout" jelét a zombi lerakással
		var timer = GetNode<Timer>("Timer");
		timer.Timeout += OnTimerTimeout;
	}

	private void OnTimerTimeout()
	{
		if (ZombieScene == null)
		{
			GD.Print("HIBA: Nincs behúzva a ZombieScene a Spawner Inspectorában!");
			return;
		}

		// 1. Zombi példányosítása
		var zombie = (Node2D)ZombieScene.Instantiate();

		// 2. Véletlenszerű pozíció kiszámítása a körvonalon
		float angle = (float)GD.RandRange(0, Math.PI * 2);
		Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * SpawnRadius;
		
		zombie.GlobalPosition = GlobalPosition + offset;

		// 3. Hozzáadás a pályához
		GetParent().AddChild(zombie);
		
		GD.Print("Új zombi megjelent!");
	}
}
