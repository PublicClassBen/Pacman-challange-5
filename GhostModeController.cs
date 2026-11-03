using Godot;
using System;

public partial class GhostModeController : Node
{
	[Signal] public delegate void GhostEatenEventHandler(int points);
	[Signal] public delegate void PacManCaughtEventHandler();
	private int _ghostChain;

	[Export]
	public double ScatterSeconds = 7.0;
	[Export]
	public double ChaseSeconds = 20.0;

	internal GhostMode CurrentMode { get; private set; } = GhostMode.SCATTER;

	[Export] public double FirstReleaseSeconds = 2.0;
	[Export] public double ReleaseIntervalSeconds = 3.0;

	[Export] public double FrightenedSeconds = 6.0;
	[Export] public double FlashWarningSeconds = 2.0;

	internal bool FrightenedFlashing => _frightenedTimer.TimeLeft > 0
		&& _frightenedTimer.TimeLeft <= FlashWarningSeconds;
	private Timer _frightenedTimer;
	private Ghost[] _ghosts;
	private PacMan _pacMan;
	private Timer _releaseTimer;
	private Ghost[] _releaseOrder;
	private int _nextGhost;
	private Timer _modeTimer;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_modeTimer = GetNode<Timer>("ModeTimer");
		_modeTimer.OneShot = true;
		_modeTimer.Timeout += OnModeTimeout;

		_modeTimer.Start(ScatterSeconds);

		_releaseOrder = new Ghost[]
		{
			GetNode<Ghost>("../Pink"),
			GetNode<Ghost>("../Blue"),
			GetNode<Ghost>("../Yellow")
		};
		_releaseTimer = GetNode<Timer>("ReleaseTimer");
		_releaseTimer.OneShot = true;
		_releaseTimer.Timeout += OnReleaseTimeout;
		_releaseTimer.Start(Math.Max(FirstReleaseSeconds, 0.01));

		_ghosts = new Ghost[] { GetNode<Ghost>("../Red"), _releaseOrder[0], _releaseOrder[1], _releaseOrder[2] };
		_pacMan = GetNode<PacMan>("../PacMan");
		_pacMan.PowerPelletEaten += StartFrightened;
		_frightenedTimer = new Timer { Name = "FrightenedTimer", OneShot = true };
		AddChild(_frightenedTimer);
		_frightenedTimer.Timeout += EndFrightened;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Movement bodies ignore each other; handle eating without blocking them.
		foreach (Ghost ghost in _ghosts)
		{
			if (ghost.GlobalPosition.DistanceSquaredTo(_pacMan.GlobalPosition) > 36f)
				continue;
			if (ghost.CanBeEaten)
			{
				ghost.Eat();
				EmitSignal(SignalName.GhostEaten, 200 << Math.Min(_ghostChain++, 3));
			}
			else if (ghost.CanCatchPacMan)
			{
				EmitSignal(SignalName.PacManCaught);
				return;
			}
		}
	}

	private void StartFrightened()
	{
		_ghostChain = 0;
		_modeTimer.Paused = true;
		_frightenedTimer.Start(Math.Max(FrightenedSeconds, 0.01));
		foreach (Ghost ghost in _ghosts)
			ghost.SetFrightened(true);
	}

	private void EndFrightened()
	{
		foreach (Ghost ghost in _ghosts)
			ghost.SetFrightened(false);
		_modeTimer.Paused = false;
	}

	private void OnReleaseTimeout()
	{
		_releaseOrder[_nextGhost].ReleaseFromHouse();
		_nextGhost++;
		if (_nextGhost < _releaseOrder.Length)
			_releaseTimer.Start(Math.Max(ReleaseIntervalSeconds, 0.01));
	}

	private void OnModeTimeout()
	{
		if (CurrentMode == GhostMode.SCATTER)
		{
			CurrentMode = GhostMode.CHASE;
			_modeTimer.Start(ChaseSeconds);
		}
		else
		{
			CurrentMode = GhostMode.SCATTER;
			_modeTimer.Start(ScatterSeconds);
		}
		GD.Print($"Ghost mode: {CurrentMode}");
	}
}
