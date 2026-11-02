using Godot;
public partial class Ghost : CharacterBody2D
{
	private Vector2 _direction = Vector2.Right;
	private Vector2 _requestedDirection = Vector2.Right;
	private Vector2 _targetPosition;
	private bool _hasTarget;
	[Export] public float Speed = 70f;
	[Export] public TileMapLayer Map { get; set; }

	[Export] private Ghost_Type ghostType;

	private AnimatedSprite2D AnimationPlayer;

	private CharacterBody2D pacMan;
	private Marker2D blinkyScatterMarker;
	private Marker2D InkyScatterMarker;
	private Marker2D PinkyScatterMarker;
	private Marker2D ClydeScatterMarker;

	private CharacterBody2D blinky;

	public override void _Ready()
	{
		blinky = GetNode<CharacterBody2D>("../Red");
		blinkyScatterMarker = GetNode<Marker2D>("../BlinkyScatter");
		InkyScatterMarker = GetNode<Marker2D>("../InkyScatter");
		PinkyScatterMarker = GetNode<Marker2D>("../PinkyScatter");
		ClydeScatterMarker = GetNode<Marker2D>("../ClydeScatter");
		pacMan = GetNode<CharacterBody2D>("../PacMan");
		AnimationPlayer = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		setGhostAnimationDirection(Directional_Movement.DOWN);
		if (Map?.TileSet == null)
		{
			GD.PushError("Assign Ghost's Map property to the maze TileMapLayer.");
			SetPhysicsProcess(false);
			return;
		}

		// Align the spawn with the tile centers used for all subsequent turns.
		Vector2I cell = Map.LocalToMap(Map.ToLocal(GlobalPosition));
		GlobalPosition = Map.ToGlobal(Map.MapToLocal(cell));
	}

	public override void _PhysicsProcess(double delta)
	{
		float distanceLeft = Mathf.Max(Speed, 0f) * (float)delta;
		bool moved = false;

		// Finish each grid step exactly, even when a frame crosses a tile center.
		while (distanceLeft > 0f)
		{
			if (!_hasTarget)
			{
				GetInput();
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
			{
				// Return to the tile center where this step started.
				GlobalPosition = _targetPosition - GetGridStep(_direction);

				// Allow the AI to choose another direction next frame.
				_hasTarget = false;

				break;
			}

			if (step >= distance)
			{
				_hasTarget = false;
			}
		}

		Velocity = moved ? GetGridStep(_direction).Normalized() * Speed : Vector2.Zero;
		UpdateAnimation(moved);
	}

	private void GetInput()
	{
		//for now just figure out chase
		chase();
		//scatter();
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
			setGhostAnimationDirection(Directional_Movement.RIGHT);

		else if (_direction == Vector2.Left)
			setGhostAnimationDirection(Directional_Movement.LEFT);

		else if (_direction == Vector2.Up)
			setGhostAnimationDirection(Directional_Movement.UP);

		else
			setGhostAnimationDirection(Directional_Movement.DOWN);
	}

	private void setGhostAnimationDirection(Directional_Movement direction)
	{
		switch (ghostType)
		{
			case Ghost_Type.RED:
				switch (direction)
				{
					case Directional_Movement.UP:
						AnimationPlayer.Play("red_ghost_up");

						break;
					case Directional_Movement.DOWN:
						AnimationPlayer.Play("red_ghost_down");

						break;
					case Directional_Movement.LEFT:
						AnimationPlayer.Play("red_ghost_left");

						break;
					case Directional_Movement.RIGHT:
						AnimationPlayer.Play("red_ghost_right");
						break;
				}
				break;
			case Ghost_Type.PINK:
				switch (direction)
				{
					case Directional_Movement.UP:
						AnimationPlayer.Play("pink_ghost_up");

						break;
					case Directional_Movement.DOWN:
						AnimationPlayer.Play("pink_ghost_down");

						break;
					case Directional_Movement.LEFT:
						AnimationPlayer.Play("pink_ghost_left");

						break;
					case Directional_Movement.RIGHT:
						AnimationPlayer.Play("pink_ghost_right");
						break;
				}
				break;
			case Ghost_Type.YELLOW:
				switch (direction)
				{
					case Directional_Movement.UP:
						AnimationPlayer.Play("yellow_ghost_up");

						break;
					case Directional_Movement.DOWN:
						AnimationPlayer.Play("yellow_ghost_down");

						break;
					case Directional_Movement.LEFT:
						AnimationPlayer.Play("yellow_ghost_left");

						break;
					case Directional_Movement.RIGHT:
						AnimationPlayer.Play("yellow_ghost_right");
						break;
				}
				break;
			case Ghost_Type.BLUE:
				switch (direction)
				{
					case Directional_Movement.UP:
						AnimationPlayer.Play("blue_ghost_up");

						break;
					case Directional_Movement.DOWN:
						AnimationPlayer.Play("blue_ghost_down");

						break;
					case Directional_Movement.LEFT:
						AnimationPlayer.Play("blue_ghost_left");

						break;
					case Directional_Movement.RIGHT:
						AnimationPlayer.Play("blue_ghost_right");
						break;
				}
				break;
			default:
				GD.PushError("Assign Ghost Type property to the Ghost Object.");
				break;
		}
	}

	//rules: Ghosts are not allowed to reverse their direction of travel (But they can when they change modes)

	/*
		Ghosts are forced to reverse direction by the system anytime the mode changes from:
		chase-to-scatter, chase-to-frightened, scatter-to-chase, and scatter-to-frightened.
		Ghosts do not reverse direction when changing back from frightened to chase or scatter modes.
	*/

	//ghost is in chase mode to find and capture Pac-Man by hunting him down in the maze.
	private void chase()
	{
		switch (ghostType)
		{
			case Ghost_Type.RED:
				blinkyChase();
				break;
			case Ghost_Type.PINK:
				pinkyChase();
				break;
			case Ghost_Type.YELLOW:
				clydeChase();
				break;
			case Ghost_Type.BLUE:
				inkyChase();
				break;
			default:
				GD.PushError("Assign Ghost Type property to the Ghost Object.");
				break;
		}

	}
	private void inkyChase()
	{
		//Get the current cell of pacman
		Vector2I pacManCell = Map.LocalToMap(Map.ToLocal(pacMan.GlobalPosition));
		Vector2I ghostCell = Map.LocalToMap(Map.ToLocal(GlobalPosition));
		Vector2I blinkyCell = Map.LocalToMap(Map.ToLocal(blinky.GlobalPosition));
		Vector2I pacManDirection = ((PacMan)pacMan).CurrentDirection;

		Vector2I ahead = pacManCell + pacManDirection * 2;

		Vector2I offset = ahead - blinkyCell;
		Vector2I targetCell = ahead + offset;

		Vector2I[] directions =
			{
			Vector2I.Up,
			Vector2I.Left,
			Vector2I.Down,
			Vector2I.Right
		};

		int bestScore = int.MaxValue;
		Vector2 bestDirection = _direction;
		foreach (Vector2I direction in directions)
		{
			Vector2 movementDirection = (Vector2)direction;

			//not allowed to turn around
			if (movementDirection == -_direction)
				continue;
			//blocked by wall
			if (!CanMove(movementDirection))
				continue;

			Vector2I nextCell = ghostCell + direction;
			int dx = nextCell.X - targetCell.X;
			int dy = nextCell.Y - targetCell.Y;
			int score = dx * dx + dy * dy;

			if (score < bestScore)
			{
				bestScore = score;
				bestDirection = movementDirection;
			}
		}
		if (bestScore == int.MaxValue && CanMove(-_direction))
		{
			bestDirection = -_direction;
		}
		_requestedDirection = bestDirection;
	}

	private void pinkyChase()
	{
		//Get the current cell of pacman
		Vector2I pacManCell = Map.LocalToMap(Map.ToLocal(pacMan.GlobalPosition));
		Vector2I ghostCell = Map.LocalToMap(Map.ToLocal(GlobalPosition));
		Vector2I targetCell = pacManCell + ((PacMan)pacMan).CurrentDirection * 4;

		Vector2I[] directions =
			{
			Vector2I.Up,
			Vector2I.Left,
			Vector2I.Down,
			Vector2I.Right
		};

		int bestScore = int.MaxValue;
		Vector2 bestDirection = _direction;
		foreach (Vector2I direction in directions)
		{
			Vector2 movementDirection = (Vector2)direction;

			//not allowed to turn around
			if (movementDirection == -_direction)
				continue;
			//blocked by wall
			if (!CanMove(movementDirection))
				continue;

			Vector2I nextCell = ghostCell + direction;
			int dx = nextCell.X - targetCell.X;
			int dy = nextCell.Y - targetCell.Y;
			int score = dx * dx + dy * dy;

			if (score < bestScore)
			{
				bestScore = score;
				bestDirection = movementDirection;
			}
		}
		if (bestScore == int.MaxValue && CanMove(-_direction))
		{
			bestDirection = -_direction;
		}
		_requestedDirection = bestDirection;
	}
	private void clydeChase()
	{
		//Get the current cell of pacman
		Vector2I pacManCell = Map.LocalToMap(Map.ToLocal(pacMan.GlobalPosition));
		Vector2I ghostCell = Map.LocalToMap(Map.ToLocal(GlobalPosition));

		if (isWithinEightTiles(ghostCell, pacManCell))
		{
			scatter(ClydeScatterMarker);
		}
		else
		{

			Vector2I[] directions =
			{
			Vector2I.Up,
			Vector2I.Left,
			Vector2I.Down,
			Vector2I.Right
		};

			int bestScore = int.MaxValue;
			Vector2 bestDirection = _direction;
			foreach (Vector2I direction in directions)
			{
				Vector2 movementDirection = (Vector2)direction;

				//not allowed to turn around
				if (movementDirection == -_direction)
					continue;
				//blocked by wall
				if (!CanMove(movementDirection))
					continue;

				Vector2I nextCell = ghostCell + direction;
				int dx = nextCell.X - pacManCell.X;
				int dy = nextCell.Y - pacManCell.Y;
				int score = dx * dx + dy * dy;

				if (score < bestScore)
				{
					bestScore = score;
					bestDirection = movementDirection;
				}
			}
			if (bestScore == int.MaxValue && CanMove(-_direction))
			{
				bestDirection = -_direction;
			}
			_requestedDirection = bestDirection;
		}
	}

	private bool isWithinEightTiles(Vector2I ghostCell, Vector2I pacManCell)
	{
		int dx = pacManCell.X - ghostCell.X;
		int dy = pacManCell.Y - ghostCell.Y;

		int distanceSquared = dx * dx + dy * dy;

		return distanceSquared < 64;
	}

	private void blinkyChase()
	{
		//Get the current cell of pacman
		Vector2I pacManCell = Map.LocalToMap(Map.ToLocal(pacMan.GlobalPosition));
		Vector2I ghostCell = Map.LocalToMap(Map.ToLocal(GlobalPosition));

		Vector2I[] directions =
		{
			Vector2I.Up,
			Vector2I.Left,
			Vector2I.Down,
			Vector2I.Right
		};

		int bestScore = int.MaxValue;
		Vector2 bestDirection = _direction;
		foreach (Vector2I direction in directions)
		{
			Vector2 movementDirection = (Vector2)direction;

			//not allowed to turn around
			if (movementDirection == -_direction)
				continue;
			//blocked by wall
			if (!CanMove(movementDirection))
				continue;

			Vector2I nextCell = ghostCell + direction;
			int dx = nextCell.X - pacManCell.X;
			int dy = nextCell.Y - pacManCell.Y;
			int score = dx * dx + dy * dy;

			if (score < bestScore)
			{
				bestScore = score;
				bestDirection = movementDirection;
			}
		}
		_requestedDirection = bestDirection;
	}

	private void scatter(Marker2D scatterMarker)
	{
		//Get the current cell of marker
		Vector2I BlinkyScatterPosition = Map.LocalToMap(Map.ToLocal(scatterMarker.GlobalPosition));
		Vector2I ghostCell = Map.LocalToMap(Map.ToLocal(GlobalPosition));

		Vector2I[] directions =
		{
			Vector2I.Up,
			Vector2I.Left,
			Vector2I.Down,
			Vector2I.Right
		};

		int bestScore = int.MaxValue;
		Vector2 bestDirection = _direction;
		foreach (Vector2I direction in directions)
		{
			Vector2 movementDirection = (Vector2)direction;

			//not allowed to turn around
			if (movementDirection == -_direction)
				continue;
			//blocked by wall
			if (!CanMove(movementDirection))
				continue;

			Vector2I nextCell = ghostCell + direction;
			int dx = nextCell.X - BlinkyScatterPosition.X;
			int dy = nextCell.Y - BlinkyScatterPosition.Y;
			int score = dx * dx + dy * dy;

			if (score < bestScore)
			{
				bestScore = score;
				bestDirection = movementDirection;
			}
		}
		_requestedDirection = bestDirection;
	}
	// give up chase and head to their respective home corners.
	private void scatter()
	{
		switch (ghostType)
		{
			case Ghost_Type.RED:
				scatter(blinkyScatterMarker);
				break;
			case Ghost_Type.PINK:
				scatter(PinkyScatterMarker);
				break;
			case Ghost_Type.YELLOW:
				scatter(ClydeScatterMarker);
				break;
			case Ghost_Type.BLUE:
				scatter(InkyScatterMarker);
				break;
			default:
				GD.PushError("Assign Ghost Type property to the Ghost Object.");
				break;
		}
	}
	// After Pac-Man eats one of the four energizers.
	//Ghosts use a pseudo-random number generator (PRNG) to pick a way to turn at each intersection when frightened.
	private void frightened()
	{

	}
}
