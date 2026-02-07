using Godot;
using System;

public partial class Zombie : CharacterBody2D
{
	[Export] public float Speed = 60.0f;
	[Export] public int Health = 50;
	[Export] public int Damage = 15; // Ennyit von le a játékostól
	[Export] public float AttackRange = 45.0f;
	[Export] public float AttackCooldown = 1.5f;

	private BasePlayer _playerTarget; 
	private float _attackTimer = 0.0f;

	public enum State { Idle, Chase, Attack }
	public State CurrentState = State.Idle;

	public override void _Ready()
	{
		var detectionArea = GetNode<Area2D>("DetectionArea");
		detectionArea.BodyEntered += OnDetectionAreaBodyEntered;
		detectionArea.BodyExited += OnDetectionAreaBodyExited;
	}

	public override void _PhysicsProcess(double delta)
	{
		_attackTimer += (float)delta;
		Vector2 velocity = Vector2.Zero;

		switch (CurrentState)
		{
			case State.Idle:
				break;

			case State.Chase:
				if (_playerTarget != null)
				{
					Vector2 direction = (_playerTarget.GlobalPosition - GlobalPosition).Normalized();
					velocity = direction * Speed;

					if (GlobalPosition.DistanceTo(_playerTarget.GlobalPosition) < AttackRange)
						CurrentState = State.Attack;
				}
				break;

			case State.Attack:
				if (_playerTarget != null)
				{
					// Ha a játékos elszalad
					if (GlobalPosition.DistanceTo(_playerTarget.GlobalPosition) > AttackRange)
					{
						CurrentState = State.Chase;
					}
					// Támadás cooldown alapján
					else if (_attackTimer >= AttackCooldown)
					{
						PerformAttack();
						_attackTimer = 0.0f;
					}
				}
				break;
		}

		if (velocity.X != 0)
			GetNode<Sprite2D>("Sprite2D").FlipH = velocity.X < 0;

		Velocity = velocity;
		MoveAndSlide();
	}

	private void PerformAttack()
	{
		if (_playerTarget != null)
		{
			GD.Print("Zombi harap!");
			_playerTarget.TakeDamage(Damage);
		}
	}

	public void TakeDamage(int amount)
	{
		Health -= amount;
		GD.Print($"Zombi sebződött! Maradék HP: {Health}");
		if (Health <= 0) QueueFree();
	}

	private void OnDetectionAreaBodyEntered(Node2D body)
	{
		if (body is BasePlayer player)
		{
			_playerTarget = player;
			CurrentState = State.Chase;
		}
	}

	private void OnDetectionAreaBodyExited(Node2D body)
	{
		if (body is BasePlayer player && _playerTarget == player)
		{
			_playerTarget = null;
			CurrentState = State.Idle;
		}
	}
}
