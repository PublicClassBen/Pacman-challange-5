using Godot;
using System.Collections.Generic;

public partial class GameApp : Node
{
    private enum Screen { Menu, Ready, Playing, Dying, Paused, GameOver, Clear }
    private Screen _screen;
    private Node2D _round;
    private Control _ui;
    private Control _overlay;
    private Label _scoreLabel;
    private Label _bestLabel;
    private Label _livesLabel;
    private HBoxContainer _lifeIcons;
    private Font _font;
    private Texture2D _pacIcon;
    private readonly List<Texture2D> _ghostIcons = new();
    private readonly HashSet<Vector2I> _consumed = new();
    private int _score;
    private int _best;
    private int _lives = 3;
    private int _level = 1;
    private int _pelletsLeft;
    private double _transitionLeft;
    private Screen _resumeScreen;
    private static readonly Color Yellow = new("ffe14a");
    private static readonly Color Cyan = new("73e9ff");
    private static readonly Color Muted = new("8997ba");

    public override void _Ready()
    {
        _font = GD.Load<Font>("res://prstartk.ttf");
        var pacTemplate = GD.Load<PackedScene>("res://pac_man.tscn").Instantiate();
        _pacIcon = pacTemplate.GetNode<AnimatedSprite2D>("AnimatedSprite2D").SpriteFrames.GetFrameTexture("chomp_right", 1);
        pacTemplate.Free();
        var ghostTemplate = GD.Load<PackedScene>("res://ghost.tscn").Instantiate();
        var frames = ghostTemplate.GetNode<AnimatedSprite2D>("AnimatedSprite2D").SpriteFrames;
        foreach (string color in new[] { "red", "pink", "blue", "yellow" })
            _ghostIcons.Add(frames.GetFrameTexture($"{color}_ghost_right", 0));
        ghostTemplate.Free();
        var save = new ConfigFile();
        if (save.Load("user://arcade.cfg") == Error.Ok)
            _best = (int)save.GetValue("scores", "best", 0);
        var canvas = new CanvasLayer { Name = "Interface", Layer = 10 };
        AddChild(canvas);
        _ui = new Control { Name = "UI", MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.AddChild(_ui);
        _ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ShowMenu();
    }

    public override void _Process(double delta)
    {
        if (_screen != Screen.Ready && _screen != Screen.Dying)
            return;
        _transitionLeft -= delta;
        if (_transitionLeft > 0)
            return;
        if (_screen == Screen.Ready)
        {
            _screen = Screen.Playing;
            _round.ProcessMode = ProcessModeEnum.Inherit;
            RemoveOverlay();
        }
        else if (_lives > 0)
            LoadRound();
        else
            ShowGameOver();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel"))
            return;
        if (_screen == Screen.Playing || _screen == Screen.Ready)
            PauseGame();
        else if (_screen == Screen.Paused)
            ResumeGame();
        GetViewport().SetInputAsHandled();
    }

    public void StartGame()
    {
        _score = 0;
        _lives = 3;
        _level = 1;
        _consumed.Clear();
        LoadRound();
    }

    private void LoadRound()
    {
        RemoveRound();
        ClearUi();
        _round = GD.Load<PackedScene>("res://pacman_the_game.tscn").Instantiate<Node2D>();
        _round.Name = "Round";
        var map = _round.GetNode<TileMapLayer>("Map");
        foreach (Vector2I cell in _consumed)
            map.SetCell(cell, 0, Vector2I.Zero);
        _pelletsLeft = 0;
        foreach (Vector2I cell in map.GetUsedCells())
        {
            Vector2I atlas = map.GetCellAtlasCoords(cell);
            if (map.GetCellSourceId(cell) == 0 && (atlas == new Vector2I(8, 1) || atlas == new Vector2I(8, 2)))
                _pelletsLeft++;
        }
        AddChild(_round);
        var player = _round.GetNode<PacMan>("PacMan");
        player.PelletConsumed += OnPelletConsumed;
        var controller = _round.GetNode<GhostModeController>("GhostModeController");
        controller.GhostEaten += AddScore;
        controller.PacManCaught += OnCaught;
        _round.ProcessMode = ProcessModeEnum.Disabled;
        _screen = Screen.Ready;
        _transitionLeft = 1.4;
        BuildHud();
        _overlay = new Control { Name = "ReadyOverlay", MouseFilter = Control.MouseFilterEnum.Ignore };
        _ui.AddChild(_overlay);
        Text(_overlay, "READY!", 48, 155, 128, 12, 10, Yellow);
    }

    private void OnPelletConsumed(Vector2I cell, int points)
    {
        if (!_consumed.Add(cell))
            return;
        AddScore(points);
        _pelletsLeft--;
        if (_pelletsLeft == 0)
            CallDeferred(nameof(CompleteMaze));
    }

    private void AddScore(int points)
    {
        _score += points;
        _best = Mathf.Max(_best, _score);
        UpdateHud();
    }

    private void OnCaught()
    {
        if (_screen != Screen.Playing)
            return;
        if (_pelletsLeft == 0)
        {
            CompleteMaze();
            return;
        }
        _lives--;
        _screen = Screen.Dying;
        _transitionLeft = 1.6;
        _round.ProcessMode = ProcessModeEnum.Disabled;
        _round.GetNode<PacMan>("PacMan").PlayDeath();
        UpdateHud();
    }

    private void CompleteMaze()
    {
        if (_screen != Screen.Playing)
            return;
        _screen = Screen.Clear;
        _round.ProcessMode = ProcessModeEnum.Disabled;
        SaveBest();
        var panel = Modal("MAZE CLEAR!", $"ROUND {_level:00} COMPLETE");
        Button(panel, "NEXT ROUND", 20, 80, 152, 25, () =>
        {
            _level++;
            _consumed.Clear();
            LoadRound();
        }).GrabFocus();
        Button(panel, "MAIN MENU", 20, 113, 152, 22, ShowMenu, true);
    }

    private void PauseGame()
    {
        if (_screen != Screen.Playing && _screen != Screen.Ready)
            return;
        _resumeScreen = _screen;
        _screen = Screen.Paused;
        _round.ProcessMode = ProcessModeEnum.Disabled;
        var panel = Modal("PAUSED", "TAKE A BREATHER");
        Button(panel, "RESUME", 20, 77, 152, 25, ResumeGame).GrabFocus();
        Button(panel, "RESTART", 20, 109, 72, 22, StartGame, true);
        Button(panel, "MENU", 100, 109, 72, 22, ShowMenu, true);
    }

    private void ResumeGame()
    {
        _screen = _resumeScreen;
        RemoveOverlay();
        if (_screen == Screen.Playing)
            _round.ProcessMode = ProcessModeEnum.Inherit;
        else
        {
            _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            _ui.AddChild(_overlay);
            Text(_overlay, "READY!", 48, 155, 128, 12, 10, Yellow);
        }
    }

    private void ShowGameOver()
    {
        _screen = Screen.GameOver;
        SaveBest();
        var panel = Modal("GAME OVER", $"FINAL SCORE  {_score:000000}");
        Button(panel, "PLAY AGAIN", 20, 80, 152, 25, StartGame).GrabFocus();
        Button(panel, "MAIN MENU", 20, 113, 152, 22, ShowMenu, true);
    }

    public void ShowMenu()
    {
        SaveBest();
        RemoveRound();
        ClearUi();
        _screen = Screen.Menu;
        var background = new ColorRect { Color = new Color("050712"), MouseFilter = Control.MouseFilterEnum.Ignore };
        _ui.AddChild(background);
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var border = new Panel { Position = new Vector2(10, 20), Size = new Vector2(204, 248), MouseFilter = Control.MouseFilterEnum.Ignore };
        border.AddThemeStyleboxOverride("panel", Box(new Color("080e20"), new Color("2845cc")));
        _ui.AddChild(border);
        Text(_ui, "THE ARCADE CLASSIC", 12, 36, 200, 12, 6, Cyan);
        Text(_ui, "PAC-MAN", 10, 60, 204, 32, 25, Yellow);
        Text(_ui, "ONE MAZE. FOUR GHOSTS.", 12, 99, 200, 12, 6, Muted);
        for (int i = 0; i < 4; i++)
            Icon(_ui, _ghostIcons[i], 41 + i * 40, 122, 22);
        Button(_ui, "START GAME", 36, 164, 152, 28, StartGame).GrabFocus();
        Text(_ui, "ARROW KEYS TO MOVE", 12, 210, 200, 12, 6, Colors.White);
        Text(_ui, "ESC TO PAUSE", 12, 225, 200, 12, 6, Muted);
        Text(_ui, $"HIGH SCORE  {_best:000000}", 12, 248, 200, 12, 6, Yellow);
    }

    private void BuildHud()
    {
        Text(_ui, "SCORE", 8, 2, 72, 10, 6, Muted, HorizontalAlignment.Left);
        _scoreLabel = Text(_ui, "", 8, 11, 76, 12, 9, Colors.White, HorizontalAlignment.Left);
        _scoreLabel.Name = "ScoreValue";
        Text(_ui, "HIGH SCORE", 77, 2, 90, 10, 6, Muted);
        _bestLabel = Text(_ui, "", 77, 11, 90, 12, 9, Yellow);
        Text(_ui, $"ROUND\n{_level:00}", 177, 2, 39, 22, 7, Cyan, HorizontalAlignment.Right);
        _livesLabel = Text(_ui, "", 8, 274, 53, 12, 6, Muted, HorizontalAlignment.Left);
        _livesLabel.Name = "LivesText";
        _lifeIcons = new HBoxContainer { Name = "LifeIcons", Position = new Vector2(65, 273), MouseFilter = Control.MouseFilterEnum.Ignore };
        _lifeIcons.AddThemeConstantOverride("separation", 3);
        _ui.AddChild(_lifeIcons);
        Button(_ui, "PAUSE", 174, 273, 42, 13, PauseGame, true);
        UpdateHud();
    }

    private void UpdateHud()
    {
        if (_scoreLabel == null || !IsInstanceValid(_scoreLabel))
            return;
        _scoreLabel.Text = _score.ToString("000000");
        _bestLabel.Text = _best.ToString("000000");
        _livesLabel.Text = $"LIVES {_lives}";
        foreach (Node child in _lifeIcons.GetChildren())
        {
            _lifeIcons.RemoveChild(child);
            child.QueueFree();
        }
        for (int i = 0; i < _lives; i++)
            _lifeIcons.AddChild(new TextureRect { Texture = _pacIcon, CustomMinimumSize = new Vector2(11, 11), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = CanvasItem.TextureFilterEnum.Nearest });
    }

    private Control Modal(string title, string subtitle)
    {
        RemoveOverlay();
        var shade = new ColorRect { Name = "Modal", Color = new Color(0.01f, 0.02f, 0.06f, 0.85f) };
        _ui.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _overlay = shade;
        var panel = new Panel { Position = new Vector2(16, 70), Size = new Vector2(192, 148) };
        panel.AddThemeStyleboxOverride("panel", Box(new Color("080e20"), new Color("3b58e8")));
        shade.AddChild(panel);
        Text(panel, title, 8, 22, 176, 20, 13, Yellow);
        Text(panel, subtitle, 8, 51, 176, 14, 6, Muted);
        return panel;
    }

    private Label Text(Control parent, string text, float x, float y, float width, float height, int size, Color color, HorizontalAlignment alignment = HorizontalAlignment.Center)
    {
        var label = new Label { Text = text, Position = new Vector2(x, y), Size = new Vector2(width, height), HorizontalAlignment = alignment, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        parent.AddChild(label);
        label.ResetSize();
        label.Size = new Vector2(width, height);
        return label;
    }

    private Button Button(Control parent, string text, float x, float y, float width, float height, System.Action action, bool subtle = false)
    {
        var button = new Button { Text = text, Position = new Vector2(x, y), Size = new Vector2(width, height) };
        button.AddThemeFontOverride("font", _font);
        button.AddThemeFontSizeOverride("font_size", height <= 22 ? 6 : 9);
        button.AddThemeColorOverride("font_color", subtle ? Cyan : Yellow);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_focus_color", Colors.White);
        button.AddThemeStyleboxOverride("normal", Box(new Color(subtle ? "0b142a" : "162958"), new Color(subtle ? "233962" : "627de3")));
        button.AddThemeStyleboxOverride("hover", Box(new Color("253f77"), Cyan));
        button.AddThemeStyleboxOverride("pressed", Box(new Color("304b83"), Yellow));
        var focus = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = Yellow };
        focus.SetBorderWidthAll(1);
        button.AddThemeStyleboxOverride("focus", focus);
        button.Pressed += action;
        parent.AddChild(button);
        button.ResetSize();
        button.Size = new Vector2(width, height);
        return button;
    }

    private static StyleBoxFlat Box(Color fill, Color border)
    {
        var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 2, ContentMarginBottom = 2 };
        box.SetBorderWidthAll(1);
        return box;
    }

    private static void Icon(Control parent, Texture2D texture, float x, float y, float size)
    {
        parent.AddChild(new TextureRect { Texture = texture, Position = new Vector2(x, y), Size = new Vector2(size, size), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore });
    }

    private void RemoveOverlay()
    {
        if (_overlay != null && IsInstanceValid(_overlay))
        {
            _overlay.Hide();
            _overlay.QueueFree();
        }
        _overlay = null;
    }

    private void ClearUi()
    {
        foreach (Node child in _ui.GetChildren())
        {
            _ui.RemoveChild(child);
            child.QueueFree();
        }
        _overlay = null;
        _scoreLabel = null;
    }

    private void RemoveRound()
    {
        if (_round != null && IsInstanceValid(_round))
        {
            _round.ProcessMode = ProcessModeEnum.Disabled;
            RemoveChild(_round);
            _round.QueueFree();
        }
        _round = null;
    }

    public override void _ExitTree()
    {
        if (_font != null)
            SaveBest();
    }

    private void SaveBest()
    {
        var save = new ConfigFile();
        save.SetValue("scores", "best", _best);
        save.Save("user://arcade.cfg");
    }
}
