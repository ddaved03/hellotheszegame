using Godot;
using System.Collections.Generic;

public partial class PaaldaPatrol : CharacterBody2D
{
    [Export] public float Speed = 120.0f;
    [Export] public float ArriveDistance = 8.0f;
    [Export] public float PushRadius = 62.0f;
    [Export] public float PushSpeed = 210.0f;
    [Export] public Texture2D FrontTexture;
    [Export] public Texture2D BackTexture;
    [Export] public Texture2D SideTexture;
    [Export] public Texture2D DiagonalFrontTexture;
    [Export] public Texture2D DiagonalBackTexture;

    private readonly List<Vector2> _patrolPoints = new();
    private int _currentPointIndex;
    private BasePlayer _player;
    private Sprite2D _sprite;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (FrontTexture == null)
        {
            FrontTexture = _sprite?.Texture;
        }

        var pointsRoot = GetNodeOrNull<Node2D>("PatrolPoints");
        if (pointsRoot == null)
        {
            return;
        }

        foreach (var child in pointsRoot.GetChildren())
        {
            if (child is Node2D point)
            {
                _patrolPoints.Add(point.GlobalPosition);
            }
        }

        _currentPointIndex = 0;
        _player = GetTree().GetFirstNodeInGroup("Player") as BasePlayer;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_patrolPoints.Count < 2)
        {
            Velocity = Vector2.Zero;
            return;
        }

        var target = _patrolPoints[_currentPointIndex];
        var toTarget = target - GlobalPosition;

        if (toTarget.Length() <= ArriveDistance)
        {
            AdvancePoint();
            target = _patrolPoints[_currentPointIndex];
            toTarget = target - GlobalPosition;
        }

        Velocity = toTarget.Normalized() * Speed;
        UpdateFacing();
        MoveAndSlide();
        if (GetSlideCollisionCount() > 0)
        {
            AdvancePoint();
        }

        PushPlayerAway((float)delta);
    }

    private void AdvancePoint()
    {
        _currentPointIndex = (_currentPointIndex + 1) % _patrolPoints.Count;
    }

    private void UpdateFacing()
    {
        if (_sprite == null)
        {
            return;
        }

        var absX = Mathf.Abs(Velocity.X);
        var absY = Mathf.Abs(Velocity.Y);
        var isDiagonal = absX > 20.0f && absY > 20.0f;

        if (isDiagonal && Velocity.Y > 0.01f && DiagonalFrontTexture != null)
        {
            _sprite.Texture = DiagonalFrontTexture;
            _sprite.FlipH = Velocity.X < -0.01f;
        }
        else if (isDiagonal && Velocity.Y < -0.01f && DiagonalBackTexture != null)
        {
            _sprite.Texture = DiagonalBackTexture;
            _sprite.FlipH = Velocity.X < -0.01f;
        }
        else if ((absX >= absY || isDiagonal) && SideTexture != null)
        {
            _sprite.Texture = SideTexture;
            _sprite.FlipH = Velocity.X < -0.01f;
        }
        else if (Velocity.Y < -0.01f && BackTexture != null)
        {
            _sprite.Texture = BackTexture;
            _sprite.FlipH = false;
        }
        else if (FrontTexture != null)
        {
            _sprite.Texture = FrontTexture;
            _sprite.FlipH = false;
        }
    }

    private void PushPlayerAway(float delta)
    {
        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("Player") as BasePlayer;
            return;
        }

        var offset = _player.GlobalPosition - GlobalPosition;
        if (offset.Length() > PushRadius)
        {
            return;
        }

        var pushDirection = offset == Vector2.Zero ? Velocity.Normalized() : offset.Normalized();
        _player.MoveAndCollide(pushDirection * PushSpeed * delta);
    }
}
