using Godot;

public partial class PacMan : CharacterBody2D
{
    [Signal] public delegate void PowerPelletEatenEventHandler();
    [Signal] public delegate void PelletConsumedEventHandler(Vector2I cell, int points);
    private Vector2 _direction = Vector2.Right;
    private Vector2 _requestedDirection = Vector2.Right;
    private Vector2 _targetPosition;
    private bool _hasTarget;
    [Export] public float Speed = 80f;
    [Export] public TileMapLayer Map { get; set; }

    public Vector2I CurrentDirection => (Vector2I)_direction;

    private AnimatedSprite2D AnimationPlayer;

    public override void _Ready()
    {
        AnimationPlayer = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        if (Map?.TileSet == null)
        {
            GD.PushError("Assign PacMan's Map property to the maze TileMapLayer.");
            SetPhysicsProcess(false);
            return;
        }

        // Align the spawn with the tile centers used for all subsequent turns.
        Vector2I cell = Map.LocalToMap(Map.ToLocal(GlobalPosition));
        GlobalPosition = Map.ToGlobal(Map.MapToLocal(cell));
    }

    public override void _PhysicsProcess(double delta)
    {
        GetInput();

        float distanceLeft = Mathf.Max(Speed, 0f) * (float)delta;
        bool moved = false;

        // Finish each grid step exactly, even when a frame crosses a tile center.
        while (distanceLeft > 0f)
        {
            if (!_hasTarget)
            {
                if (CanMove(_requestedDirection))
                    _direction = _requestedDirection;

                if (!CanMove(_direction))
                    break;

                _targetPosition = GlobalPosition + GetGridStep(_direction);
                _hasTarget = true;
            }

            Vector2 toTarget = _targetPosition - GlobalPosition;
            float distance = toTarget.Length();
            float step = Mathf.Min(distanceLeft, distance);
            var collision = MoveAndCollide(toTarget.Normalized() * step);
            moved |= step > 0f && (collision == null || collision.GetTravel().LengthSquared() > 0f);
            distanceLeft -= step;

            if (collision != null)
                break;

            if (step >= distance)
            {
                _hasTarget = false;
                GlobalPosition = MazeTunnel.WrapPosition(Map, GlobalPosition);
                ConsumeDot();
            }
        }

        Velocity = moved ? GetGridStep(_direction).Normalized() * Speed : Vector2.Zero;
        UpdateAnimation(moved);
    }

    internal void PlayDeath()
    {
        Velocity = Vector2.Zero;
        AnimationPlayer.ProcessMode = ProcessModeEnum.Always;
        AnimationPlayer.SpriteFrames.SetAnimationLoopMode("die", SpriteFrames.LoopMode.None);
        AnimationPlayer.Play("die");
    }

    private void GetInput()
    {
        if (Input.IsActionJustPressed("right"))
            _requestedDirection = Vector2.Right;
        else if (Input.IsActionJustPressed("left"))
            _requestedDirection = Vector2.Left;
        else if (Input.IsActionJustPressed("up"))
            _requestedDirection = Vector2.Up;
        else if (Input.IsActionJustPressed("down"))
            _requestedDirection = Vector2.Down;
    }

    private Vector2 GetGridStep(Vector2 direction)
    {
        Vector2 localStep = direction * (Vector2)Map.TileSet.TileSize;
        return Map.ToGlobal(localStep) - Map.ToGlobal(Vector2.Zero);
    }

    private bool CanMove(Vector2 direction)
    {
        return !TestMove(GlobalTransform, GetGridStep(direction));
    }

    private void UpdateAnimation(bool moving)
    {
        if (!moving)
        {
            AnimationPlayer.Pause();
            return;
        }

        if (_direction == Vector2.Right)
            AnimationPlayer.Play("chomp_right");
        else if (_direction == Vector2.Left)
            AnimationPlayer.Play("chomp_left");
        else if (_direction == Vector2.Up)
            AnimationPlayer.Play("chomp_up");
        else
            AnimationPlayer.Play("chomp_down");
    }

    private void ConsumeDot()
    {
        // Convert Pac-Man's world position into a map cell.
        Vector2I cell = Map.LocalToMap(Map.ToLocal(GlobalPosition));

        if (Map.GetCellSourceId(cell) != 0)
            return;

        Vector2I atlas = Map.GetCellAtlasCoords(cell);
        bool powerPellet = atlas == new Vector2I(8, 1);
        if (atlas == new Vector2I(8, 2) || powerPellet)
        {
            Map.SetCell(cell, 0, new Vector2I(0, 0));
            EmitSignal(SignalName.PelletConsumed, cell, powerPellet ? 50 : 10);
            if (powerPellet)
                EmitSignal(SignalName.PowerPelletEaten);
        }
    }
}
