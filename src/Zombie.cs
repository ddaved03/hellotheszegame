using Godot;
using System;

public partial class Zombie : CharacterBody2D
{
	[Export] public float Speed = 60.0f;
	[Export] public int MaxHealth = 50;
	public int CurrentHealth;
	[Export] public int Damage = 15;
	[Export] public float AttackCooldown = 1.5f;

	// Tárgyak, amiket a zombi eldobhat
	[Export] public PackedScene XpOrbScene;
	[Export] public PackedScene PotionScene; 

	private BasePlayer _playerTarget;
	private float _attackTimer = 0.0f;

	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		UpdateUI();
		
		// Csatlakozunk a DetectionArea-hoz a követéshez
		var detArea = GetNodeOrNull<Area2D>("DetectionArea");
		if (detArea != null)
		{
			detArea.BodyEntered += (body) => { if (body is BasePlayer p) _playerTarget = p; };
			detArea.BodyExited += (body) => { if (body == _playerTarget) _playerTarget = null; };
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Biztonsági ellenőrzés: ha megállt a játék vagy törlődik a zombi, ne fusson a fizika
		if (GetTree().Paused || IsQueuedForDeletion() || !IsInsideTree()) return;

		_attackTimer += (float)delta;

		if (_playerTarget != null)
		{
			Vector2 direction = (_playerTarget.GlobalPosition - GlobalPosition).Normalized();
			Velocity = direction * Speed;
			MoveAndSlide();

			// --- IRÁNYÍTÁS: Sprite és Támadó négyzet kezelése (A te működő kódod alapján) ---
			var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
			var performAttack = GetNodeOrNull<Area2D>("PerformAttack");

			if (Velocity.X != 0)
			{
				bool facingLeft = Velocity.X < 0;
				if (sprite != null) sprite.FlipH = facingLeft;

				// A támadó területet (piros négyzet) eltoljuk a mozgás irányába
				if (performAttack != null)
				{
					performAttack.Position = new Vector2(facingLeft ? -35 : 35, 0);
				}
			}

			// Tényleges támadás ellenőrzése a PerformAttack terület segítségével
			if (performAttack != null && performAttack.OverlapsBody(_playerTarget) && _attackTimer >= AttackCooldown)
			{
				_playerTarget.TakeDamage(Damage);
				_attackTimer = 0.0f;
			}
		}
		else
		{
			Velocity = Vector2.Zero;
		}
	}

	public void TakeDamage(int amount)
	{
		CurrentHealth -= amount;
		UpdateUI();
		if (CurrentHealth <= 0) Die();
	}

	private void Die()
{
	GD.Print("Zombi meghalt!"); // Látnod kell a konzolon

	if (XpOrbScene != null)
	{
		var orb = (Node2D)XpOrbScene.Instantiate();
		orb.GlobalPosition = GlobalPosition;
		GetTree().Root.AddChild(orb);
	}
	else { GD.Print("HIBA: XpOrbScene nincs behúzva az Inspectorban!"); }

	// Potion dobás ellenőrzése
	if (GD.Randf() <= 0.25f && PotionScene != null)
	{
		GD.Print("Potion létrehozása..."); 
		var potion = (Node2D)PotionScene.Instantiate();
		potion.GlobalPosition = GlobalPosition + new Vector2(10, 10);
		GetTree().Root.AddChild(potion);
		GD.Print("Potion sikeresen kidobva!");
	}
	else 
	{ 
		GD.Print("HIBA: PotionScene nincs behúzva az Inspectorban!"); 
	}

	QueueFree();
}

	private void UpdateUI()
	{
		var healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
		if (healthBar != null)
		{
			healthBar.MaxValue = MaxHealth;
			healthBar.Value = CurrentHealth;
		}
	}
}
