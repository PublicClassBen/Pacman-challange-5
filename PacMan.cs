using Godot;
using System;

public partial class PacMan : CharacterBody2D
{
	public const float Speed = 150.0f;
	private AnimatedSprite2D AnimationPlayer;

    public override void _Ready()
    {
        AnimationPlayer = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }

	public override void _PhysicsProcess(double delta)
	{
		GetInput();
		MoveAndSlide();
	}

	public void GetInput()
    {

        Vector2 inputDirection = Input.GetVector("left", "right", "up", "down");
        Velocity = inputDirection * Speed;
    }
}
