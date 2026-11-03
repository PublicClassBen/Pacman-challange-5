using Godot;
using System;

public partial class GhostModeController : Node
{

	[Export]
	public double ScatterSeconds = 7.0;
	[Export]
	public double ChaseSeconds = 20.0;

	internal GhostMode CurrentMode { get; private set; } = GhostMode.SCATTER;

	private Timer _modeTimer;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_modeTimer = GetNode<Timer>("ModeTimer");
		_modeTimer.OneShot = true;
		_modeTimer.Timeout += OnModeTimeout;

		_modeTimer.Start(ScatterSeconds);
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
