using Godot;
using System;

public partial class Zombie : CharacterBody2D
{
	[Export] public float Speed = 60.0f;
	[Export] public int MaxHealth = 50;
	public int CurrentHealth;
	[Export] public int Damage = 15;
	[Export] public float AttackCooldown = 1.5f;
	[Export] public PackedScene XpOrbScene; // Ne felejtsd el behúzni az XpOrb.tscn-t!

	private BasePlayer _playerTarget;
	private float _attackTimer = 0.0f;

	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		UpdateUI();
		
		// Csatlakozunk a DetectionArea-hoz
		var detArea = GetNodeOrNull<Area2D>("DetectionArea");
		if (detArea != null)
		{
			detArea.BodyEntered += (body) => { if (body is BasePlayer p) _playerTarget = p; };
			detArea.BodyExited += (body) => { if (body == _playerTarget) _playerTarget = null; };
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Ha megállt a játék, nem mozog
		if (GetTree().Paused || IsQueuedForDeletion()) return;

		_attackTimer += (float)delta;

		if (_playerTarget != null)
		{
			Vector2 direction = (_playerTarget.GlobalPosition - GlobalPosition).Normalized();
			Velocity = direction * Speed;
			MoveAndSlide();

			// --- IRÁNYÍTÁS: Sprite és Támadó négyzet kezelése ---
			var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
			var performAttack = GetNodeOrNull<Area2D>("PerformAttack");

			if (Velocity.X != 0)
			{
				bool facingLeft = Velocity.X < 0;
				if (sprite != null) sprite.FlipH = facingLeft;

				// A támadó területet (piros négyzet az ábrádon) eltoljuk a mozgás irányába
				if (performAttack != null)
				{
					// Ha balra néz, -35 pixel, ha jobbra, +35 pixel
					performAttack.Position = new Vector2(facingLeft ? -35 : 35, 0);
				}
			}

			// Tényleges támadás, ha a célpont benne van a támadó területben
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
		if (XpOrbScene != null)
		{
			var orb = (Node2D)XpOrbScene.Instantiate();
			orb.GlobalPosition = GlobalPosition;
			GetTree().Root.AddChild(orb);
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
