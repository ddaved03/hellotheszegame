using Godot;
using System;

public partial class Zombie : CharacterBody2D
{
	[Export] public float Speed = 60.0f;
	[Export] public int MaxHealth = 50;
	public int CurrentHealth;
	[Export] public int Damage = 15;
	[Export] public float AttackCooldown = 1.5f;
	[Export] public Texture2D FrontTexture;
	[Export] public Texture2D BackTexture;
	[Export] public float AttackOffsetDistance = 35.0f;

	// Tárgyak, amiket a zombi eldobhat
	[Export] public PackedScene XpOrbScene;
	[Export] public PackedScene PotionScene; 

	private BasePlayer _playerTarget;
	private float _attackTimer = 0.0f;
	private Sprite2D _sprite;
	private Area2D _performAttack;
	private Timer _ambientTimer;

	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		UpdateUI();
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		_performAttack = GetNodeOrNull<Area2D>("PerformAttack");

		if (FrontTexture == null && _sprite != null)
		{
			FrontTexture = _sprite.Texture;
		}

		_ambientTimer = new Timer
		{
			OneShot = true,
			Autostart = false
		};
		AddChild(_ambientTimer);
		_ambientTimer.Timeout += OnAmbientTimerTimeout;
		ScheduleAmbient();
		
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

			UpdateFacing(direction);
			UpdateAttackArea(direction);

			// Tényleges támadás ellenőrzése a PerformAttack terület segítségével
			if (_performAttack != null && _performAttack.OverlapsBody(_playerTarget) && _attackTimer >= AttackCooldown)
			{
				AudioManager.Instance?.PlayZombieAttack(GlobalPosition);
				_playerTarget.TakeDamage(Damage);
				_attackTimer = 0.0f;
			}
		}
		else
		{
			Velocity = Vector2.Zero;
		}
	}

	private void UpdateFacing(Vector2 direction)
	{
		if (_sprite == null)
		{
			return;
		}

		if (Mathf.Abs(direction.Y) > Mathf.Abs(direction.X) && direction.Y < 0 && BackTexture != null)
		{
			_sprite.Texture = BackTexture;
		}
		else if (FrontTexture != null)
		{
			_sprite.Texture = FrontTexture;
		}

		if (Mathf.Abs(direction.X) > 0.01f)
		{
			_sprite.FlipH = direction.X < 0;
		}
	}

	private void UpdateAttackArea(Vector2 direction)
	{
		if (_performAttack == null)
		{
			return;
		}

		Vector2 attackDir = direction;
		if (attackDir == Vector2.Zero)
		{
			attackDir = Vector2.Right;
		}

		attackDir = attackDir.Normalized();
		_performAttack.Position = attackDir * AttackOffsetDistance;
	}

	public void TakeDamage(int amount)
	{
		AudioManager.Instance?.PlayZombieHit(GlobalPosition);
		CurrentHealth -= amount;
		UpdateUI();
		if (CurrentHealth <= 0) Die();
	}

	private void OnAmbientTimerTimeout()
	{
		if (!IsQueuedForDeletion() && IsInsideTree() && !GetTree().Paused)
		{
			AudioManager.Instance?.PlayZombieAmbient(GlobalPosition);
		}

		ScheduleAmbient();
	}

	private void ScheduleAmbient()
	{
		if (_ambientTimer == null)
		{
			return;
		}

		_ambientTimer.Start((float)GD.RandRange(2.2f, 6.0f));
	}

	private void Die()
	{
		AudioManager.Instance?.PlayZombieDeath(GlobalPosition);
		GD.Print("Zombi meghalt!");

		if (XpOrbScene != null)
		{
			var orb = (Node2D)XpOrbScene.Instantiate();
			orb.GlobalPosition = GlobalPosition;
			GetTree().Root.AddChild(orb);
			AudioManager.Instance?.PlayDropXp(GlobalPosition);
		}
		else
		{
			GD.Print("HIBA: XpOrbScene nincs behúzva az Inspectorban!");
		}

		if (GD.Randf() <= 0.25f)
		{
			if (PotionScene != null)
			{
				var potion = (Node2D)PotionScene.Instantiate();
				potion.GlobalPosition = GlobalPosition + new Vector2(10, 10);
				GetTree().Root.AddChild(potion);
				AudioManager.Instance?.PlayDropPotion(potion.GlobalPosition);
			}
			else
			{
				GD.Print("HIBA: PotionScene nincs behúzva az Inspectorban!");
			}
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
