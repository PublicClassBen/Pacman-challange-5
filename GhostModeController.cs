using Godot;
using System;

public partial class GhostModeController : Node
{

	[Export]
	public double ScatterSeconds = 7.0;
	[Export]
	public double ChaseSeconds = 20.0;

	internal GhostMode CurrentMode { get; private set; } = GhostMode.SCATTER;

	[Export] public double FirstReleaseSeconds = 2.0;
	[Export] public double ReleaseIntervalSeconds = 3.0;

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
