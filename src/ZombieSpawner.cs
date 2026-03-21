using Godot;
using System;

public partial class ZombieSpawner : Node2D
{
	[Export] public PackedScene ZombieNormalScene;
	[Export] public PackedScene ZombieSmallScene;
	[Export] public PackedScene ZombieBigScene;
	
	// Milyen messze a spawnertől szülessenek a zombik (véletlenszerűen)
	[Export] public float SpawnRadius = 500.0f;

	private readonly RandomNumberGenerator _rng = new();

	private enum ZombieType
	{
		Small,
		Normal,
		Big
	}

	public override void _Ready()
	{
		_rng.Randomize();

		// Összekötjük a Timer "timeout" jelét a zombi lerakással
		var timer = GetNode<Timer>("Timer");
		timer.Timeout += OnTimerTimeout;
	}

	private void OnTimerTimeout()
	{
		if (ZombieNormalScene == null || ZombieSmallScene == null || ZombieBigScene == null)
		{
			GD.Print("HIBA: A három zombi scene közül legalább egy nincs behúzva a Spawner Inspectorában!");
			return;
		}

		// 1. Véletlenszerű zombi típus kiválasztása
		ZombieType zombieType = (ZombieType)_rng.RandiRange(0, 2);
		PackedScene selectedScene = zombieType switch
		{
			ZombieType.Small => ZombieSmallScene,
			ZombieType.Big => ZombieBigScene,
			_ => ZombieNormalScene
		};

		var zombie = (Node2D)selectedScene.Instantiate();

		// 2. Véletlenszerű pozíció kiszámítása a körvonalon
		float angle = (float)GD.RandRange(0, Math.PI * 2);
		Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * SpawnRadius;
		
		zombie.GlobalPosition = GlobalPosition + offset;

		// 3. Log, melyik típusból jött létre
		switch (zombieType)
		{
			case ZombieType.Small:
				GD.Print("Kicsi zombi megjelent!");
				break;
			case ZombieType.Big:
				GD.Print("Nagy zombi megjelent!");
				break;
			default:
				GD.Print("Normál zombi megjelent!");
				break;
		}

		// 4. Hozzáadás a pályához
		GetParent().AddChild(zombie);
	}
}
