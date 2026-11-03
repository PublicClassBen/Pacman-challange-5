using Godot;
using System.Collections.Generic;
public partial class Ghost : CharacterBody2D
{
	private Vector2 _direction = Vector2.Right;
	private Vector2 _requestedDirection = Vector2.Right;
	private Vector2 _targetPosition;
	private bool _hasTarget;
	[Export] public float Speed = 70f;
	[Export] public TileMapLayer Map { get; set; }
	[Export] public bool StartInHouse { get; set; }
	[Export] public Marker2D HouseCenter { get; set; }
	[Export] public Marker2D HouseExit { get; set; }

	[Export] public float FrightenedSpeed = 45f;
	[Export] public float EyesSpeed = 140f;
	[Export] public double RecoverySeconds = 0.75;

	private enum HouseState { Waiting, Exiting, Active, Entering, Recovering }
	private bool _isFrightened;
	private bool _returningEyes;
	private bool _reversePending;
	private bool _atHouseExit;
	private double _recoveryLeft;
	private readonly Queue<Vector2I> _returnPath = new();
	private static readonly Vector2I[] GridDirections = { Vector2I.Up, Vector2I.Left, Vector2I.Down, Vector2I.Right };
	internal bool CanBeEaten => _isFrightened && !_returningEyes && _houseState == HouseState.Active;
	private float MovementSpeed => Mathf.Max(_returningEyes ? EyesSpeed : _isFrightened ? FrightenedSpeed : Speed, 0f);
	private HouseState _houseState = HouseState.Active;
	private bool _alignedWithDoor;
	private bool _clearedDoor;
	private Vector2 _mazeEntryPosition;

	[Export] private Ghost_Type ghostType;

	private AnimatedSprite2D AnimationPlayer;

	private CharacterBody2D pacMan;
	private Marker2D blinkyScatterMarker;
	private Marker2D InkyScatterMarker;
	private Marker2D PinkyScatterMarker;
	private Marker2D ClydeScatterMarker;

	private CharacterBody2D blinky;

	private GhostModeController _modeController;

	public override void _Ready()
	{
		_modeController = GetNode<GhostModeController>("../GhostModeController");
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

		HouseCenter ??= GetNodeOrNull<Marker2D>("../HouseCenter");
		HouseExit ??= GetNodeOrNull<Marker2D>("../HouseExit");
		if (HouseCenter == null || HouseExit == null)
		{
			GD.PushError($"Assign house markers for {Name}.");
			SetPhysicsProcess(false);
			return;
		}
		Vector2I mazeEntryCell = Map.LocalToMap(Map.ToLocal(HouseExit.GlobalPosition));
		_mazeEntryPosition = Map.ToGlobal(Map.MapToLocal(mazeEntryCell));

		if (StartInHouse)
		{
			// House positions may be between maze cells to center the group visually.
			// Align to a real cell only after crossing the doorway.
			_houseState = HouseState.Waiting;
		}
		else
		{
			Vector2I cell = Map.LocalToMap(Map.ToLocal(GlobalPosition));
			GlobalPosition = Map.ToGlobal(Map.MapToLocal(cell));
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_houseState == HouseState.Entering)
		{
			MoveIntoHouse(delta);
			return;
		}
		if (_houseState == HouseState.Recovering)
		{
			Velocity = Vector2.Zero;
			_recoveryLeft -= delta;
			if (_recoveryLeft <= 0)
			{
				_alignedWithDoor = false;
				_clearedDoor = false;
				_houseState = HouseState.Exiting;
			}
			return;
		}
		if (_houseState == HouseState.Waiting)
		{
			Velocity = Vector2.Zero;
			UpdateAnimation(false);
			return;
		}
		if (_houseState == HouseState.Exiting)
		{
			MoveOutOfHouse(delta);
			return;
		}

		float distanceLeft = MovementSpeed * (float)delta;
		bool moved = false;

		// Finish each grid step exactly, even when a frame crosses a tile center.
		while (distanceLeft > 0f)
		{
			if (!_hasTarget)
			{
				if (_returningEyes && GlobalPosition.IsEqualApprox(_mazeEntryPosition))
				{
					_houseState = HouseState.Entering;
					_atHouseExit = false;
					Velocity = Vector2.Zero;
					break;
				}
				GetInput();
				if (_requestedDirection == Vector2.Zero)
					break;
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
				_returnPath.Clear();

				break;
			}

			if (step >= distance)
			{
				_hasTarget = false;
			}
		}

		Velocity = moved ? GetGridStep(_direction).Normalized() * MovementSpeed : Vector2.Zero;
		UpdateAnimation(moved);
	}

	internal void ReleaseFromHouse()
	{
		if (_houseState == HouseState.Waiting)
			_houseState = HouseState.Exiting;
	}

	private void MoveOutOfHouse(double delta)
	{
		// Align with the door, cross it, then join the maze at a tile center.
		// This scripted movement passes through the house gate's map collision.
		Vector2 target = !_alignedWithDoor ? HouseCenter.GlobalPosition
			: !_clearedDoor ? HouseExit.GlobalPosition : _mazeEntryPosition;
		Vector2 before = GlobalPosition;
		Vector2 motion = target - before;
		if (!motion.IsZeroApprox())
			_direction = Mathf.Abs(motion.X) > Mathf.Abs(motion.Y)
				? new Vector2(Mathf.Sign(motion.X), 0)
				: new Vector2(0, Mathf.Sign(motion.Y));

		GlobalPosition = before.MoveToward(target, MovementSpeed * (float)delta);
		bool moved = !GlobalPosition.IsEqualApprox(before);
		Velocity = moved ? _direction * MovementSpeed : Vector2.Zero;
		UpdateAnimation(moved);

		if (GlobalPosition.IsEqualApprox(target))
		{
			if (!_alignedWithDoor)
				_alignedWithDoor = true;
			else if (!_clearedDoor)
				_clearedDoor = true;
			else
			{
				_houseState = HouseState.Active;
				_hasTarget = false;
				_requestedDirection = _direction;
			}
		}
	}

	private void GetInput()
	{
		if (_returningEyes)
		{
			ChooseReturnDirection();
			return;
		}
		if (_reversePending)
		{
			_reversePending = false;
			if (CanMove(-_direction))
			{
				_requestedDirection = -_direction;
				return;
			}
		}
		if (_isFrightened)
		{
			frightened();
			return;
		}
		switch (_modeController.CurrentMode)
		{
			case GhostMode.CHASE:
				chase();
				break;
			case GhostMode.SCATTER:
				scatter();
				break;
		}

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
		if (_returningEyes)
		{
			string direction = _direction == Vector2.Right ? "right" : _direction == Vector2.Left ? "left"
				: _direction == Vector2.Up ? "up" : "down";
			AnimationPlayer.Play($"dead_ghost_{direction}");
			return;
		}
		if (_isFrightened)
		{
			AnimationPlayer.Play(_modeController.FrightenedFlashing ? "vulnerable_ghost_expire" : "vulerable_ghost");
			return;
		}
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
	private void frightened()
	{
		var choices = new List<Vector2>();
		foreach (Vector2I direction in GridDirections)
		{
			Vector2 move = (Vector2)direction;
			if (move != -_direction && CanMove(move))
				choices.Add(move);
		}
		_requestedDirection = choices.Count > 0
			? choices[(int)GD.RandRange(0, choices.Count - 1)]
			: CanMove(-_direction) ? -_direction : _direction;
	}

	internal void SetFrightened(bool frightened)
	{
		if (_returningEyes || _houseState == HouseState.Recovering)
			return;
		_isFrightened = frightened;
		_reversePending = frightened && _houseState == HouseState.Active;
		UpdateAnimation(true);
	}

	internal void Eat()
	{
		if (!CanBeEaten)
			return;
		_isFrightened = false;
		_returningEyes = true;
		_reversePending = false;
		_returnPath.Clear();
		// Complete the current tile step before planning a route home.
		UpdateAnimation(true);
	}

	private void ChooseReturnDirection()
	{
		Vector2I start = Map.LocalToMap(Map.ToLocal(GlobalPosition));
		Vector2I goal = Map.LocalToMap(Map.ToLocal(_mazeEntryPosition));
		if (_returnPath.Count == 0)
		{
			// Eyes need an actual route home rather than a greedy chase target.
			var frontier = new Queue<Vector2I>();
			var previous = new Dictionary<Vector2I, Vector2I> { [start] = start };
			Rect2I bounds = Map.GetUsedRect();
			frontier.Enqueue(start);
			while (frontier.Count > 0 && !previous.ContainsKey(goal))
			{
				Vector2I cell = frontier.Dequeue();
				foreach (Vector2I direction in GridDirections)
				{
					Vector2I next = cell + direction;
					if (!bounds.HasPoint(next) || previous.ContainsKey(next))
						continue;
					Transform2D from = GlobalTransform;
					from.Origin = Map.ToGlobal(Map.MapToLocal(cell));
					if (TestMove(from, GetGridStep((Vector2)direction)))
						continue;
					previous[next] = cell;
					frontier.Enqueue(next);
				}
			}
			if (previous.ContainsKey(goal))
			{
				var path = new Stack<Vector2I>();
				for (Vector2I cell = goal; cell != start; cell = previous[cell])
					path.Push(cell);
				while (path.Count > 0)
					_returnPath.Enqueue(path.Pop());
			}
		}
		_requestedDirection = _returnPath.Count > 0 ? (Vector2)(_returnPath.Dequeue() - start) : Vector2.Zero;
	}

	private void MoveIntoHouse(double delta)
	{
		Vector2 target = _atHouseExit ? HouseCenter.GlobalPosition : HouseExit.GlobalPosition;
		Vector2 before = GlobalPosition;
		Vector2 motion = target - before;
		if (!motion.IsZeroApprox())
			_direction = Mathf.Abs(motion.X) > Mathf.Abs(motion.Y)
				? new Vector2(Mathf.Sign(motion.X), 0) : new Vector2(0, Mathf.Sign(motion.Y));
		GlobalPosition = before.MoveToward(target, MovementSpeed * (float)delta);
		Velocity = _direction * MovementSpeed;
		UpdateAnimation(true);
		if (GlobalPosition.IsEqualApprox(target))
		{
			if (!_atHouseExit)
				_atHouseExit = true;
			else
			{
				_returningEyes = false;
				_houseState = HouseState.Recovering;
				_recoveryLeft = RecoverySeconds;
				Velocity = Vector2.Zero;
				UpdateAnimation(true);
			}
		}
	}
}
