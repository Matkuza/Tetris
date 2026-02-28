using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using IOPath = System.IO.Path;
using System.Text.Json;
using System.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Tetris;

public partial class MainWindow : Window
{
    private enum UiLanguage
    {
        English,
        Polish,
        German,
        Russian,
        Spanish
    }

    private const int BoardWidth = 10;
    private const int BoardHeight = 20;
    private const string DefaultAdminPassword = "admin";
    private const int HighscoreMaxEntries = 100;

    private readonly GameEngine _engine = new(BoardWidth, BoardHeight);
    private readonly Random _random = new();
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<GameMode, List<ScoreEntry>> _highScoresByMode = [];
    private readonly ObservableCollection<AdEntry> _ads = [];
    private readonly DispatcherTimer _adTimer;
    private readonly DispatcherTimer _visualFxTimer;
    private readonly DispatcherTimer _inputTimer;
    private readonly Dictionary<AdPanel, AdPlaybackState> _adStates = new();

    private readonly string _adStorageFolder;
    private readonly string _adManifestPath;
    private readonly string _settingsPath;
    private readonly string _highScoresPath;
    private readonly string _sessionHistoryPath;
    private readonly string _onboardingPath;

    private static readonly JsonSerializerOptions JsonWriteOptions = new() { WriteIndented = true };
    private const int MinAdSeconds = 2;
    private const int MaxAdSeconds = 120;
    private const double MaxMusicOutputVolume = 0.18;
    private const double MaxEffectsOutputVolume = 0.30;
    private const int MaxLockResetsPerPiece = 8;
    private const int LockImpactParticleCount = 24;
    private const int FallTrailParticleCount = 6;
    private const int FallTrailIntervalMs = 45;

    private int _defaultAdDurationSeconds = 10;
    private int _rotationIntervalSeconds = 1;
    private AdOrderMode _adOrderMode = AdOrderMode.Sequential;

    private Tetromino _nextPiece = null!;
    private int _score;
    private int _linesCleared;
    private bool _gameOver;
    private bool _isGameStarted;
    private bool _isPaused;
    private bool _isLoadingSettings = true;
    private int _startLevel;
    private GameMode _activeGameMode = GameMode.Classic;
    private GameMode _selectedHighscoreMode = GameMode.Classic;
    private int _survivalTickCounter;
    private readonly Stopwatch _ultraStopwatch = new();
    private readonly Stopwatch _inputStopwatch = Stopwatch.StartNew();
    private readonly Stopwatch _sessionStopwatch = new();
    private double _cellSize = 36;
    private bool _isFadeThemeActive;
    private bool _isHighscoreUnlocked;

    private readonly MediaPlayer _backgroundMusicPlayer = new();
    private readonly Dictionary<string, Uri> _soundUris;
    private readonly List<SessionHistoryEntry> _sessionHistory = [];
    private OnboardingState _onboardingState = new(false, string.Empty);
    private int _tutorialStepIndex;
    private const string CurrentWhatsNewVersion = "1.2.0";
    private static readonly string[] TutorialStepsEnglish =
    [
        "1/4 Move: ←/→ shift the piece, ↑ rotates, Space performs hard drop.",
        "2/4 Hold: key C stores the piece in HOLD and swaps it later.",
        "3/4 Modes: Sprint (40 lines), Ultra (120s), Marathon and Survival have separate highscores.",
        "4/4 Settings: you can change keys, DAS/ARR and enable colorblind mode."
    ];

    private readonly Dictionary<string, MediaPlayer> _effectPlayers = new();

    private double _effectsVolume = 0.8;
    private double _musicVolume = 0.6;
    private Tetromino? _holdPiece;
    private bool _holdUsedThisTurn;
    private int _dasMs = 140;
    private int _arrMs = 45;
    private int _softDropArrMs = 35;
    private Key _moveLeftKey = Key.Left;
    private Key _moveRightKey = Key.Right;
    private Key _softDropKey = Key.Down;
    private Key _rotateKey = Key.Up;
    private Key _hardDropKey = Key.Space;
    private Key _holdKey = Key.C;
    private int _horizontalDirection;
    private long _horizontalPressedAtMs;
    private long _lastHorizontalRepeatMs;
    private bool _softDropPressed;
    private long _softDropPressedAtMs;
    private long _lastSoftDropRepeatMs;
    private int _piecesLocked;
    private int _playerActions;
    private int _quadLineClears;
    private long _totalLockDelayMs;
    private int _lockSamples;
    private long? _groundContactStartedMs;
    private bool _colorblindMode;
    private bool _showSessionStats = true;
    private bool _musicEnabled = true;
    private bool _effectsEnabled = true;
    private bool _lockParticlesEnabled = true;
    private string _adminPassword = DefaultAdminPassword;
    private long _lastFallParticleMs;
    private UiLanguage _uiLanguage = UiLanguage.English;


    private Color _emptyCellColor = Color.FromRgb(12, 20, 38);

    private static readonly Color[] NeonPalette =
    [
        Color.FromRgb(34, 211, 238), Color.FromRgb(59, 130, 246), Color.FromRgb(251, 146, 60),
        Color.FromRgb(250, 204, 21), Color.FromRgb(74, 222, 128), Color.FromRgb(167, 139, 250),
        Color.FromRgb(248, 113, 113)
    ];

    private static readonly Color[] RetroPalette =
    [
        Color.FromRgb(255, 112, 67), Color.FromRgb(255, 202, 40), Color.FromRgb(156, 204, 101),
        Color.FromRgb(38, 198, 218), Color.FromRgb(126, 87, 194), Color.FromRgb(236, 64, 122),
        Color.FromRgb(255, 167, 38)
    ];

    private static readonly Color[] FadePalette =
    [
        Color.FromRgb(0, 245, 255), Color.FromRgb(88, 101, 242), Color.FromRgb(255, 0, 204),
        Color.FromRgb(255, 95, 109), Color.FromRgb(255, 200, 87), Color.FromRgb(57, 255, 20),
        Color.FromRgb(153, 102, 255)
    ];

    private static readonly Color[] ColorblindPalette =
    [
        Color.FromRgb(0, 114, 178), Color.FromRgb(213, 94, 0), Color.FromRgb(0, 158, 115),
        Color.FromRgb(204, 121, 167), Color.FromRgb(230, 159, 0), Color.FromRgb(86, 180, 233),
        Color.FromRgb(240, 228, 66)
    ];

    private static readonly string[] PieceSymbols = ["I", "J", "L", "O", "S", "T", "Z"];

    private Brush[] _activePaletteBrushes = [];

    private Brush?[,] _board => _engine.Board;
    private Tetromino _currentPiece
    {
        get => _engine.CurrentPiece;
        set => _engine.CurrentPiece = value;
    }

    private int _currentX
    {
        get => _engine.CurrentX;
        set => _engine.CurrentX = value;
    }

    private int _currentY
    {
        get => _engine.CurrentY;
        set => _engine.CurrentY = value;
    }

    private int _groundedTicks
    {
        get => _engine.GroundedTicks;
        set => _engine.GroundedTicks = value;
    }

    private int _lockResetsUsed
    {
        get => _engine.LockResetsUsed;
        set => _engine.LockResetsUsed = value;
    }

    private static readonly Point[][] PieceDefinitions =
    [
        [new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1)],
        [new Point(0, 0), new Point(0, 1), new Point(1, 1), new Point(2, 1)],
        [new Point(2, 0), new Point(0, 1), new Point(1, 1), new Point(2, 1)],
        [new Point(1, 0), new Point(2, 0), new Point(1, 1), new Point(2, 1)],
        [new Point(1, 0), new Point(2, 0), new Point(0, 1), new Point(1, 1)],
        [new Point(1, 0), new Point(0, 1), new Point(1, 1), new Point(2, 1)],
        [new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(2, 1)]
    ];

    public MainWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => Tick();
        _adTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _adTimer.Tick += (_, _) => RotateAds();
        _visualFxTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        _visualFxTimer.Tick += (_, _) =>
        {
            if (_isFadeThemeActive)
            {
                Draw();
                DrawNextPiece();
                DrawHoldPiece();
            }
        };
        _inputTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _inputTimer.Tick += (_, _) => ProcessHeldInput();

        _adStorageFolder = ResolveAdStoragePath();
        _adManifestPath = IOPath.Combine(_adStorageFolder, "ads.json");
        _settingsPath = IOPath.Combine(_adStorageFolder, "settings.json");
        _highScoresPath = IOPath.Combine(_adStorageFolder, "highscores.json");
        _sessionHistoryPath = IOPath.Combine(_adStorageFolder, "session-history.json");
        _onboardingPath = IOPath.Combine(_adStorageFolder, "onboarding.json");
        _soundUris = CreateSoundUriMap();
        InitializeEffectPlayers();
        _backgroundMusicPlayer.MediaEnded += (_, _) =>
        {
            _backgroundMusicPlayer.Position = TimeSpan.Zero;
            _backgroundMusicPlayer.Play();
        };
        _backgroundMusicPlayer.Volume = ScaleVolume(_musicVolume, MaxMusicOutputVolume);

        AdListBox.ItemsSource = _ads;
        ShowStartMenuSection(StartMenuSection.NewGame);

        ConfigureAdStates();
        EnsureAdStorage();
        LoadAds();
        LoadHighScores();
        LoadSessionHistory();
        LoadSettings();
        LoadOnboardingState();
        ApplyLoadedGlobalSettingsToUi();
        ApplyControlSettingsToUi();
        ApplyTheme();
        ApplyLanguageToUi();
        UpdateBoardLayout();
        Draw();
        RotateAds();
        _adTimer.Start();
        _isLoadingSettings = false;
        DrawTrendChart();
        ShowOnboardingIfNeeded();
    }

    private Dictionary<string, Uri> CreateSoundUriMap()
    {
        var soundFolder = IOPath.Combine(AppContext.BaseDirectory, "Sound");
        return new Dictionary<string, Uri>
        {
            ["rotate"] = new Uri(IOPath.Combine(soundFolder, "rotate.mp3")),
            ["buttonClick"] = new Uri(IOPath.Combine(soundFolder, "ButtonClick.mp3")),
            ["startGame"] = new Uri(IOPath.Combine(soundFolder, "StartGame.mp3")),
            ["lineClear"] = new Uri(IOPath.Combine(soundFolder, "AllBrickInLine.mp3")),
            ["defeat"] = new Uri(IOPath.Combine(soundFolder, "defeat.mp3")),
            ["gameMusic"] = new Uri(IOPath.Combine(soundFolder, "GameMusicLoop.mp3"))
        };
    }

    private void InitializeEffectPlayers()
    {
        foreach (var (key, uri) in _soundUris)
        {
            if (key == "gameMusic" || !File.Exists(uri.LocalPath))
            {
                continue;
            }

            var player = new MediaPlayer();
            player.Open(uri);
            player.Volume = ScaleVolume(_effectsVolume, MaxEffectsOutputVolume);
            _effectPlayers[key] = player;
        }
    }

    private static double ScaleVolume(double uiValue, double maxOutput)
    {
        var normalized = Math.Clamp(uiValue, 0, 1);
        return Math.Pow(normalized, 2.2) * maxOutput;
    }

    private void PlayEffect(string soundKey)
    {
        if (!_effectsEnabled || _effectsVolume <= 0.001 || !_effectPlayers.TryGetValue(soundKey, out var player))
        {
            return;
        }

        player.Volume = ScaleVolume(_effectsVolume, MaxEffectsOutputVolume);
        player.Position = TimeSpan.Zero;
        player.Play();
    }

    private void PlayBackgroundMusic()
    {
        if (!_musicEnabled || _musicVolume <= 0.001)
        {
            return;
        }

        if (!_soundUris.TryGetValue("gameMusic", out var uri) || !File.Exists(uri.LocalPath))
        {
            return;
        }

        _backgroundMusicPlayer.Open(uri);
        _backgroundMusicPlayer.Volume = ScaleVolume(_musicVolume, MaxMusicOutputVolume);
        _backgroundMusicPlayer.Position = TimeSpan.Zero;
        _backgroundMusicPlayer.Play();
    }

    private void StopBackgroundMusic()
    {
        _backgroundMusicPlayer.Stop();
    }

    private void EffectsVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _effectsVolume = Math.Clamp(e.NewValue, 0, 1);
        foreach (var player in _effectPlayers.Values)
        {
            player.Volume = ScaleVolume(_effectsVolume, MaxEffectsOutputVolume);
        }

        if (!_isLoadingSettings)
        {
            SaveSettings();
        }
    }

    private void MusicVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _musicVolume = Math.Clamp(e.NewValue, 0, 1);
        _backgroundMusicPlayer.Volume = ScaleVolume(_musicVolume, MaxMusicOutputVolume);

        if (!_musicEnabled || _musicVolume <= 0.001)
        {
            StopBackgroundMusic();
            return;
        }

        if (e.OldValue <= 0.001 && _isGameStarted && !_gameOver && StartMenuOverlay.Visibility != Visibility.Visible)
        {
            PlayBackgroundMusic();
        }

        if (!_isLoadingSettings)
        {
            SaveSettings();
        }
    }

    private void ApplyTheme()
    {
        _colorblindMode = ColorblindModeCheckBox.IsChecked == true;

        if (_colorblindMode)
        {
            _isFadeThemeActive = false;
            _visualFxTimer.Stop();
            ClearFadeAccentEffects();
            _activePaletteBrushes = ColorblindPalette.Select(c => (Brush)new SolidColorBrush(c)).ToArray();
            Background = new SolidColorBrush(Color.FromRgb(8, 10, 20));
            _emptyCellColor = Color.FromRgb(18, 22, 34);
            SetCardTheme(Color.FromRgb(12, 18, 30), Color.FromRgb(74, 98, 122), Color.FromRgb(236, 242, 248));
            AdBorder.Background = new SolidColorBrush(Color.FromRgb(14, 24, 38));
            AdBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(80, 110, 140));
            BoardBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(80, 110, 140));
            BoardBorder.Background = new SolidColorBrush(Color.FromRgb(8, 12, 22));
            Draw();
            return;
        }

        _isFadeThemeActive = ThemeComboBox.SelectedIndex == 2;
        if (_isFadeThemeActive)
        {
            _activePaletteBrushes = CreateAnimatedFadeBrushes();
            Background = new SolidColorBrush(Color.FromRgb(4, 8, 24));
            _emptyCellColor = Color.FromRgb(14, 18, 42);
            SetCardTheme(Color.FromRgb(10, 18, 44), Color.FromRgb(70, 108, 196), Color.FromRgb(228, 244, 255));
            AdBorder.Background = new SolidColorBrush(Color.FromRgb(11, 28, 58));
            AdBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(92, 132, 226));
            BoardBorder.Background = new SolidColorBrush(Color.FromRgb(5, 9, 34));
            ApplyBoardGlowAnimation();
            ApplyFadeAccentAnimations();
            _visualFxTimer.Start();
        }
        else
        {
            _visualFxTimer.Stop();
            ClearFadeAccentEffects();
            if (ThemeComboBox.SelectedIndex == 1)
            {
                _activePaletteBrushes = RetroPalette.Select(c => (Brush)new SolidColorBrush(c)).ToArray();
                Background = new SolidColorBrush(Color.FromRgb(30, 10, 16));
                _emptyCellColor = Color.FromRgb(56, 20, 28);
                SetCardTheme(Color.FromRgb(73, 33, 44), Color.FromRgb(109, 60, 77), Color.FromRgb(248, 224, 193));
                AdBorder.Background = new SolidColorBrush(Color.FromRgb(61, 28, 40));
                AdBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(126, 74, 91));
                BoardBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(168, 109, 133));
                BoardBorder.Background = new SolidColorBrush(Color.FromRgb(37, 12, 20));
            }
            else
            {
                _activePaletteBrushes = NeonPalette.Select(c => (Brush)new SolidColorBrush(c)).ToArray();
                Background = new SolidColorBrush(Color.FromRgb(5, 8, 22));
                _emptyCellColor = Color.FromRgb(12, 20, 38);
                SetCardTheme(Color.FromRgb(9, 18, 36), Color.FromRgb(50, 67, 95), Color.FromRgb(230, 238, 250));
                AdBorder.Background = new SolidColorBrush(Color.FromRgb(8, 26, 51));
                AdBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(58, 74, 106));
                BoardBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(58, 74, 106));
                BoardBorder.Background = new SolidColorBrush(Color.FromRgb(2, 6, 23));
            }
        }

        Draw();
    }

    private Brush[] CreateAnimatedFadeBrushes()
    {
        var brushes = new Brush[FadePalette.Length];
        for (var i = 0; i < FadePalette.Length; i++)
        {
            var from = FadePalette[i];
            var to = FadePalette[(i + 1) % FadePalette.Length];
            var brush = new SolidColorBrush(from);
            var animation = new ColorAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(700 + i * 120),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            brushes[i] = brush;
        }

        return brushes;
    }

    private void ApplyBoardGlowAnimation()
    {
        var borderBrush = new SolidColorBrush(FadePalette[0]);
        BoardBorder.BorderBrush = borderBrush;

        var glow = new DropShadowEffect
        {
            BlurRadius = 26,
            ShadowDepth = 0,
            Color = FadePalette[2],
            Opacity = 0.92
        };

        BoardBorder.Effect = glow;

        var borderAnim = new ColorAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(6) };
        for (var i = 0; i < FadePalette.Length; i++)
        {
            borderAnim.KeyFrames.Add(new LinearColorKeyFrame(FadePalette[i], KeyTime.FromPercent((double)i / (FadePalette.Length - 1))));
        }

        borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, borderAnim);

        var glowAnim = new ColorAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(6) };
        for (var i = 0; i < FadePalette.Length; i++)
        {
            glowAnim.KeyFrames.Add(new LinearColorKeyFrame(FadePalette[(i + 2) % FadePalette.Length], KeyTime.FromPercent((double)i / (FadePalette.Length - 1))));
        }

        glow.BeginAnimation(DropShadowEffect.ColorProperty, glowAnim);
    }


    private void ApplyFadeAccentAnimations()
    {
        ApplyGlowToBorder(AdBorder, 1, 22, 0.85);
        ApplyGlowToBorder(AdSlotTopBorder, 2, 14, 0.72);
        ApplyGlowToBorder(AdSlotMiddleBorder, 3, 14, 0.72);
        ApplyGlowToBorder(AdSlotBottomBorder, 4, 14, 0.72);
        ApplyGlowToBorder(NickCard, 5, 16, 0.65);
        ApplyGlowToBorder(ScoreCard, 6, 16, 0.65);
        ApplyGlowToBorder(LevelCard, 7, 16, 0.65);
        ApplyGlowToBorder(NextCard, 8, 16, 0.65);
        ApplyGlowToBorder(HoldCard, 9, 16, 0.65);
        ApplyGlowToBorder(StatusCard, 10, 16, 0.65);
    }

    private void ApplyGlowToBorder(Border target, int phase, double blurRadius, double opacity)
    {
        var borderBrush = new SolidColorBrush(FadePalette[phase % FadePalette.Length]);
        target.BorderBrush = borderBrush;

        var glow = new DropShadowEffect
        {
            BlurRadius = blurRadius,
            ShadowDepth = 0,
            Color = FadePalette[(phase + 2) % FadePalette.Length],
            Opacity = opacity
        };

        target.Effect = glow;

        var borderAnim = new ColorAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(5.2 + phase * 0.3) };
        for (var i = 0; i < FadePalette.Length; i++)
        {
            var color = FadePalette[(i + phase) % FadePalette.Length];
            borderAnim.KeyFrames.Add(new LinearColorKeyFrame(color, KeyTime.FromPercent((double)i / (FadePalette.Length - 1))));
        }

        borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, borderAnim);

        var glowAnim = new ColorAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(5.2 + phase * 0.3) };
        for (var i = 0; i < FadePalette.Length; i++)
        {
            var color = FadePalette[(i + phase + 2) % FadePalette.Length];
            glowAnim.KeyFrames.Add(new LinearColorKeyFrame(color, KeyTime.FromPercent((double)i / (FadePalette.Length - 1))));
        }

        glow.BeginAnimation(DropShadowEffect.ColorProperty, glowAnim);
    }

    private void ClearFadeAccentEffects()
    {
        foreach (var border in (Border[])[BoardBorder, AdBorder, AdSlotTopBorder, AdSlotMiddleBorder, AdSlotBottomBorder, NickCard, ScoreCard, LevelCard, TimerCard, NextCard, HoldCard, StatusCard])
        {
            border.Effect = null;
        }
    }
    private void SetCardTheme(Color bg, Color border, Color titleColor)
    {
        var bgBrush = new SolidColorBrush(bg);
        var borderBrush = new SolidColorBrush(border);
        var titleBrush = new SolidColorBrush(titleColor);

        foreach (var card in (Border[])[NickCard, ScoreCard, LevelCard, TimerCard, NextCard, HoldCard, StatusCard])
        {
            card.Background = bgBrush;
            card.BorderBrush = borderBrush;
        }

        TitleText.Foreground = titleBrush;
        StartHighScoresListBox.BorderBrush = borderBrush;
        StartHighScoresListBox.Background = new SolidColorBrush(Color.FromArgb(90, bg.R, bg.G, bg.B));
    }

    private void StartNewGame()
    {
        _engine.ResetBoard();
        _score = 0;
        _linesCleared = 0;
        _gameOver = false;
        _isGameStarted = true;
        _isPaused = false;
        _survivalTickCounter = 0;
        _groundedTicks = 0;
        _lockResetsUsed = 0;
        _holdPiece = null;
        _holdUsedThisTurn = false;
        _horizontalDirection = 0;
        _softDropPressed = false;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        PauseOverlay.Visibility = Visibility.Collapsed;

        _startLevel = StartLevelComboBox.SelectedIndex switch
        {
            1 => 4,
            2 => 8,
            _ => 1
        };
        _activeGameMode = (GameMode)Math.Clamp(GameModeComboBox.SelectedIndex, 0, 4);
        _selectedHighscoreMode = _activeGameMode;
        HighscoreModeComboBox.SelectedIndex = (int)_selectedHighscoreMode;
        _ultraStopwatch.Reset();
        _sessionStopwatch.Reset();
        _sessionStopwatch.Start();
        _piecesLocked = 0;
        _playerActions = 0;
        _quadLineClears = 0;
        _totalLockDelayMs = 0;
        _lockSamples = 0;
        _groundContactStartedMs = null;
        if (_activeGameMode == GameMode.Ultra)
        {
            _ultraStopwatch.Start();
        }

        var nick = NickTextBox.Text.Trim();
        PlayerNameText.Text = string.IsNullOrWhiteSpace(nick) ? "Player" : nick;

        SetTimerSpeed();
        _nextPiece = CreateRandomPiece();
        SpawnPiece();
        UpdateHud();
        Draw();
        PlayEffect("startGame");
        PlayBackgroundMusic();
        _timer.Start();
        _inputTimer.Start();
    }

    private void SetTimerSpeed()
    {
        var level = _startLevel + (_linesCleared / (_activeGameMode == GameMode.Marathon ? 10 : 8));
        var speedMs = _activeGameMode == GameMode.Marathon
            ? Math.Max(65, 780 - (level - 1) * 50)
            : Math.Max(30, 620 - (level - 1) * 85);
        _timer.Interval = TimeSpan.FromMilliseconds(speedMs);
    }

    private int GetLockDelayTicks()
    {
        var intervalMs = Math.Max(1, _timer.Interval.TotalMilliseconds);
        return Math.Max(2, (int)Math.Ceiling(220 / intervalMs));
    }

    private void Tick()
    {
        if (_gameOver || !_isGameStarted || _isPaused)
        {
            return;
        }

        if (TryMove(_currentX, _currentY + 1, _currentPiece.Cells))
        {
            _groundedTicks = 0;
            _lockResetsUsed = 0;
            _groundContactStartedMs = null;
            EmitFallingTrailParticles();
        }
        else
        {
            _groundContactStartedMs ??= _sessionStopwatch.ElapsedMilliseconds;
            _groundedTicks++;
            if (_groundedTicks >= GetLockDelayTicks())
            {
                _groundedTicks = 0;
                LockPiece();
                var clearedRows = ClearFullLines();
                if (clearedRows.Count > 0)
                {
                    AnimateClearedLines(clearedRows);
                }

                SpawnPiece();
            }
        }

        if (_activeGameMode == GameMode.Survival)
        {
            _survivalTickCounter++;
            var interval = Math.Max(6, 22 - (_linesCleared / 6));
            if (_survivalTickCounter >= interval)
            {
                _survivalTickCounter = 0;
                AddGarbageRow();
                if (_gameOver)
                {
                    return;
                }
            }
        }

        if (_activeGameMode == GameMode.Ultra)
        {
            var ultraElapsedMs = (int)_ultraStopwatch.ElapsedMilliseconds;
            if (ultraElapsedMs >= 120000)
            {
                FinishGame(Lang(_uiLanguage, "ULTRA FINISHED • Space: start menu • Esc: close", "ULTRA ZAKOŃCZONE • Spacja: menu start • Esc: zamknij", "ULTRA BEENDET • Leertaste: Startmenü • Esc: schließen", "ULTRA ЗАВЕРШЁН • Пробел: стартовое меню • Esc: выход", "ULTRA FINALIZADO • Espacio: menú inicio • Esc: cerrar"));
                return;
            }
        }

        UpdateHud();
        Draw();
    }

    private void HandleHorizontalRepeat()
    {
        if (_horizontalDirection == 0)
        {
            return;
        }

        var nowMs = _inputStopwatch.ElapsedMilliseconds;
        if (nowMs - _horizontalPressedAtMs < _dasMs)
        {
            return;
        }

        if (_arrMs > 0 && nowMs - _lastHorizontalRepeatMs < _arrMs)
        {
            return;
        }

        var moved = TryMove(_currentX + _horizontalDirection, _currentY, _currentPiece.Cells);
        if (moved)
        {
            RefreshLockDelayAfterPlayerAction(true);
            _playerActions++;
        }

        _lastHorizontalRepeatMs = nowMs;
    }

    private void HandleSoftDropRepeat()
    {
        if (!_softDropPressed)
        {
            return;
        }

        var nowMs = _inputStopwatch.ElapsedMilliseconds;
        if (nowMs - _softDropPressedAtMs < _dasMs)
        {
            return;
        }

        if (_softDropArrMs > 0 && nowMs - _lastSoftDropRepeatMs < _softDropArrMs)
        {
            return;
        }

        if (TryMove(_currentX, _currentY + 1, _currentPiece.Cells))
        {
            _groundedTicks = 0;
            _lockResetsUsed = 0;
            _playerActions++;
            _lastSoftDropRepeatMs = nowMs;
            EmitFallingTrailParticles(forceBurst: true);
            Draw();
        }
    }

    private void ProcessHeldInput()
    {
        if (_gameOver || !_isGameStarted || _isPaused || StartMenuOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        HandleHorizontalRepeat();
        HandleSoftDropRepeat();
    }

    private void SpawnPiece()
    {
        _currentPiece = _nextPiece;
        _nextPiece = CreateRandomPiece();
        _holdUsedThisTurn = false;
        _currentX = BoardWidth / 2 - 2;
        _currentY = 0;
        _groundedTicks = 0;
        _lockResetsUsed = 0;
        _groundContactStartedMs = null;

        if (_engine.IsGameOverOnSpawn(_currentX, _currentY, _currentPiece.Cells))
        {
            OnGameOver();
        }

        DrawNextPiece();
        DrawHoldPiece();
    }

    private void OnGameOver()
    {
        FinishGame(Lang(_uiLanguage, "GAME OVER • Space: start menu • Esc: close", "KONIEC GRY • Spacja: menu start • Esc: zamknij", "SPIEL VORBEI • Leertaste: Startmenü • Esc: schließen", "КОНЕЦ ИГРЫ • Пробел: стартовое меню • Esc: выход", "FIN DEL JUEGO • Espacio: menú inicio • Esc: cerrar"), true);
    }

    private void FinishGame(string statusText, bool playDefeatSound = false)
    {
        if (_gameOver)
        {
            return;
        }

        _gameOver = true;
        _timer.Stop();
        _inputTimer.Stop();
        _ultraStopwatch.Stop();
        _sessionStopwatch.Stop();
        StopBackgroundMusic();
        if (playDefeatSound)
        {
            PlayEffect("defeat");
        }

        CaptureSessionResult();
        RegisterScore();
        GameOverOverlay.Visibility = Visibility.Visible;
        StatusText.Text = statusText;
    }

    private List<ScoreEntry> GetHighScoresForMode(GameMode mode)
    {
        if (_highScoresByMode.TryGetValue(mode, out var scores))
        {
            return scores;
        }

        scores = [];
        _highScoresByMode[mode] = scores;
        return scores;
    }

    private void RegisterScore()
    {
        var nick = string.IsNullOrWhiteSpace(PlayerNameText.Text) ? "Player" : PlayerNameText.Text;
        var modeScores = GetHighScoresForMode(_activeGameMode);
        modeScores.Add(new ScoreEntry(nick, _score));
        modeScores.Sort((a, b) => b.Points.CompareTo(a.Points));
        if (modeScores.Count > HighscoreMaxEntries)
        {
            modeScores.RemoveRange(HighscoreMaxEntries, modeScores.Count - HighscoreMaxEntries);
        }

        RefreshHighScores();
        SaveHighScores();
    }

    private void RefreshHighScores()
    {
        if (StartHighScoresListBox is null)
        {
            return;
        }

        StartHighScoresListBox.Items.Clear();

        var modeScores = GetHighScoresForMode(_selectedHighscoreMode);

        var rowCount = Math.Max(5, Math.Min(HighscoreMaxEntries, modeScores.Count));
        for (var i = 0; i < rowCount; i++)
        {
            var hasEntry = i < modeScores.Count;
            var entry = hasEntry ? modeScores[i] : new ScoreEntry("---", 0);

            var rankText = new TextBlock
            {
                Text = $"#{i + 1}",
                Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
                FontWeight = FontWeights.Bold,
                Width = 56,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 18
            };

            var nameText = new TextBlock
            {
                Text = entry.Name,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold
            };

            var pointsText = new TextBlock
            {
                Text = $"{entry.Points} {Lang(_uiLanguage, "pts", "pkt", "Pkt", "очк", "pts")}",
                Foreground = new SolidColorBrush(Color.FromRgb(196, 181, 253)),
                Width = 120,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 18,
                FontWeight = FontWeights.Bold
            };

            var editBox = new TextBox
            {
                Text = entry.Name,
                Visibility = Visibility.Collapsed,
                Width = 180,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(19, 38, 71)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 87, 124)),
                Padding = new Thickness(6, 4, 6, 4)
            };

            var applyButton = new Button
            {
                Content = "✔",
                Tag = i,
                Width = 34,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(30, 64, 175)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(91, 142, 255)),
                Visibility = hasEntry ? Visibility.Visible : Visibility.Collapsed,
                IsEnabled = hasEntry && _isHighscoreUnlocked
            };
            applyButton.Click += HighscoreApplyNameButton_Click;

            var editButton = new Button
            {
                Content = Lang(_uiLanguage, "Edit", "Edytuj", "Bearbeiten", "Изменить", "Editar"),
                Tag = i,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(10, 4, 10, 4),
                Background = new SolidColorBrush(Color.FromRgb(30, 64, 175)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(91, 142, 255)),
                Visibility = hasEntry ? Visibility.Visible : Visibility.Collapsed,
                IsEnabled = hasEntry && _isHighscoreUnlocked
            };
            editButton.Click += HighscoreEditNameButton_Click;

            var deleteButton = new Button
            {
                Content = "X",
                Tag = i,
                Width = 34,
                Height = 30,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(127, 29, 29)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                Visibility = hasEntry ? Visibility.Visible : Visibility.Collapsed,
                IsEnabled = hasEntry && _isHighscoreUnlocked
            };
            deleteButton.Click += HighscoreDeleteButton_Click;

            var row = new DockPanel { LastChildFill = false };
            row.Children.Add(rankText);
            row.Children.Add(nameText);
            row.Children.Add(pointsText);
            row.Children.Add(editBox);
            row.Children.Add(applyButton);
            row.Children.Add(editButton);
            row.Children.Add(deleteButton);

            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(i % 2 == 0 ? Color.FromRgb(12, 29, 54) : Color.FromRgb(16, 36, 66)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 87, 124)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 8),
                Child = row,
                Tag = editBox
            };

            StartHighScoresListBox.Items.Add(card);
        }
    }

    private Tetromino CreateRandomPiece()
    {
        var index = _engine.DrawNextPieceIndex(PieceDefinitions.Length);
        var shape = PieceDefinitions[index].Select(p => new Point(p.X, p.Y)).ToArray();
        return new Tetromino(shape, _activePaletteBrushes[index], index == 3);
    }

    private bool TryMove(int newX, int newY, Point[] cells)
    {
        return _engine.TryMove(newX, newY, cells);
    }

    private bool IsPositionValid(int x, int y, Point[] cells)
    {
        return _engine.IsPositionValid(x, y, cells);
    }

    private void AddGarbageRow()
    {
        if (_engine.AddGarbageRow(_random.Next(BoardWidth)))
        {
            return;
        }

        OnGameOver();
    }

    private void RefreshLockDelayAfterPlayerAction(bool actionApplied)
    {
        if (!actionApplied || !IsCurrentPieceGrounded())
        {
            return;
        }

        if (_lockResetsUsed >= MaxLockResetsPerPiece)
        {
            return;
        }

        _groundedTicks = 0;
        _lockResetsUsed++;
    }

    private bool IsCurrentPieceGrounded()
    {
        return _engine.IsCurrentPieceGrounded();
    }

    private bool RotateCurrentPiece()
    {
        return _engine.RotateCurrentPiece();
    }

    private void HardDrop()
    {
        var droppedRows = 0;
        while (TryMove(_currentX, _currentY + 1, _currentPiece.Cells))
        {
            droppedRows++;
        }

        if (droppedRows > 0)
        {
            EmitFallingTrailParticles(Math.Min(3, droppedRows / 4 + 1), forceBurst: true);
        }

        LockPiece();
        var clearedRows = ClearFullLines();
        if (clearedRows.Count > 0)
        {
            AnimateClearedLines(clearedRows);
        }

        SpawnPiece();
        Draw();
    }

    private void LockPiece()
    {
        EmitLockImpactParticles();
        _engine.LockPiece();
        RegisterPieceLockStats();
    }

    private void EmitLockImpactParticles()
    {
        if (!_lockParticlesEnabled)
        {
            return;
        }

        if (EffectCanvas is null || _currentPiece is null)
        {
            return;
        }

        var contactCells = _currentPiece.Cells
            .Where(cell =>
            {
                var boardY = _currentY + (int)cell.Y;
                return !_engine.IsPositionValid(_currentX, _currentY + 1, new[] { cell }) || boardY == BoardHeight - 1;
            })
            .Select(cell => new Point(_currentX + cell.X + 0.5, _currentY + cell.Y + 1))
            .ToList();

        if (contactCells.Count == 0)
        {
            contactCells = _currentPiece.Cells
                .Select(cell => new Point(_currentX + cell.X + 0.5, _currentY + cell.Y + 1))
                .ToList();
        }

        var color = (_currentPiece.Color as SolidColorBrush)?.Color ?? Colors.White;
        var rnd = _random;

        for (var i = 0; i < LockImpactParticleCount; i++)
        {
            var origin = contactCells[rnd.Next(contactCells.Count)];
            var size = _cellSize * (0.06 + rnd.NextDouble() * 0.14);
            var particle = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
                IsHitTestVisible = false
            };

            var startX = origin.X * _cellSize + (rnd.NextDouble() - 0.5) * (_cellSize * 0.4);
            var startY = origin.Y * _cellSize - size;
            Canvas.SetLeft(particle, startX);
            Canvas.SetTop(particle, startY);
            EffectCanvas.Children.Add(particle);

            var endX = startX + (rnd.NextDouble() - 0.5) * (_cellSize * 1.2);
            var endY = startY + rnd.NextDouble() * (_cellSize * 0.9) + (_cellSize * 0.25);

            var moveX = new DoubleAnimation(startX, endX, TimeSpan.FromMilliseconds(180 + rnd.Next(140)));
            var moveY = new DoubleAnimation(startY, endY, TimeSpan.FromMilliseconds(200 + rnd.Next(160)));
            var fade = new DoubleAnimation(0.95, 0, TimeSpan.FromMilliseconds(220 + rnd.Next(140)));
            fade.Completed += (_, _) => EffectCanvas.Children.Remove(particle);

            particle.BeginAnimation(OpacityProperty, fade);
            particle.BeginAnimation(Canvas.LeftProperty, moveX);
            particle.BeginAnimation(Canvas.TopProperty, moveY);
        }
    }

    private void EmitFallingTrailParticles(int intensity = 1, bool forceBurst = false)
    {
        if (!_lockParticlesEnabled || EffectCanvas is null || _currentPiece is null)
        {
            return;
        }

        var nowMs = _inputStopwatch.ElapsedMilliseconds;
        if (!forceBurst && nowMs - _lastFallParticleMs < FallTrailIntervalMs)
        {
            return;
        }

        _lastFallParticleMs = nowMs;
        var color = (_currentPiece.Color as SolidColorBrush)?.Color ?? Colors.White;
        var trailColor = Color.FromArgb(185, color.R, color.G, color.B);
        var topCells = _currentPiece.Cells
            .OrderBy(cell => cell.Y)
            .Take(2)
            .Select(cell => new Point(_currentX + cell.X + 0.5, _currentY + cell.Y + 0.15))
            .ToList();

        if (topCells.Count == 0)
        {
            return;
        }

        var particleCount = Math.Max(3, FallTrailParticleCount * Math.Max(1, intensity) / 2);
        var coneWidth = _cellSize * 0.8;
        var coneHeight = _cellSize * 0.9;

        for (var i = 0; i < particleCount; i++)
        {
            var apex = topCells[_random.Next(topCells.Count)];
            var spreadFactor = _random.NextDouble();
            var spread = coneWidth * spreadFactor;
            var horizontalOffset = (_random.NextDouble() - 0.5) * spread;
            var upwardOffset = coneHeight * (0.15 + _random.NextDouble() * 0.85);

            var size = _cellSize * (0.04 + _random.NextDouble() * 0.08);
            var particle = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(trailColor),
                IsHitTestVisible = false,
                Opacity = 0.82
            };

            var startX = apex.X * _cellSize + (_random.NextDouble() - 0.5) * (_cellSize * 0.25);
            var startY = apex.Y * _cellSize + (_random.NextDouble() * _cellSize * 0.1);
            Canvas.SetLeft(particle, startX);
            Canvas.SetTop(particle, startY);
            EffectCanvas.Children.Add(particle);

            var endX = startX + horizontalOffset;
            var endY = startY - upwardOffset;
            var duration = TimeSpan.FromMilliseconds(130 + _random.Next(90));
            var moveX = new DoubleAnimation(startX, endX, duration);
            var moveY = new DoubleAnimation(startY, endY, duration);
            var fade = new DoubleAnimation(0.82, 0, duration);
            fade.Completed += (_, _) => EffectCanvas.Children.Remove(particle);

            particle.BeginAnimation(OpacityProperty, fade);
            particle.BeginAnimation(Canvas.LeftProperty, moveX);
            particle.BeginAnimation(Canvas.TopProperty, moveY);
        }
    }

    private void RegisterPieceLockStats()
    {
        _piecesLocked++;
        if (_groundContactStartedMs is null)
        {
            return;
        }

        var lockDelay = Math.Max(0, _sessionStopwatch.ElapsedMilliseconds - _groundContactStartedMs.Value);
        _totalLockDelayMs += lockDelay;
        _lockSamples++;
        _groundContactStartedMs = null;
    }

    private List<int> ClearFullLines()
    {
        List<int> removedRows = _engine.ClearFullLines();

        if (removedRows.Count == 0)
        {
            return removedRows;
        }

        _linesCleared += removedRows.Count;
        _score += GameEngine.CalculateScoreForClearedLines(removedRows.Count);
        if (removedRows.Count == 4)
        {
            _quadLineClears++;
        }

        SetTimerSpeed();
        UpdateHud();
        AnimateScorePulse();
        PlayEffect("lineClear");

        if (_activeGameMode == GameMode.Sprint && _linesCleared >= 40)
        {
            FinishGame(Lang(_uiLanguage, "SPRINT COMPLETED • Space: start menu • Esc: close", "SPRINT UKOŃCZONY • Spacja: menu start • Esc: zamknij", "SPRINT ABGESCHLOSSEN • Leertaste: Startmenü • Esc: schließen", "СПРИНТ ЗАВЕРШЁН • Пробел: стартовое меню • Esc: выход", "SPRINT COMPLETADO • Espacio: menú inicio • Esc: cerrar"));
        }

        return removedRows;
    }

    private void AnimateScorePulse()
    {
        var animation = new DoubleAnimation
        {
            From = 32,
            To = 42,
            Duration = TimeSpan.FromMilliseconds(170),
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        ScoreText.BeginAnimation(TextBlock.FontSizeProperty, animation);
    }

    private void AnimateClearedLines(List<int> rows)
    {
        EffectCanvas.Children.Clear();
        foreach (var row in rows)
        {
            var glow = new Rectangle
            {
                Width = BoardWidth * _cellSize,
                Height = _cellSize,
                Fill = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)),
                Opacity = 0.9
            };

            Canvas.SetLeft(glow, 0);
            Canvas.SetTop(glow, row * _cellSize);
            EffectCanvas.Children.Add(glow);

            var fade = new DoubleAnimation
            {
                From = 0.9,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220)
            };

            fade.Completed += (_, _) => EffectCanvas.Children.Remove(glow);
            glow.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void UpdateHud()
    {
        var level = _startLevel + (_linesCleared / (_activeGameMode == GameMode.Marathon ? 10 : 8));
        var activeModeScores = GetHighScoresForMode(_activeGameMode);
        var best = activeModeScores.Count == 0 ? 0 : activeModeScores.Max(h => h.Points);
        ScoreText.Text = _score.ToString();
        BestScoreText.Text = $"BEST: {best}";
        LevelText.Text = level.ToString();

        var ultraElapsedSeconds = (int)_ultraStopwatch.Elapsed.TotalSeconds;
        var sessionSeconds = Math.Max(1d, _sessionStopwatch.Elapsed.TotalSeconds);
        var apm = _playerActions * 60d / sessionSeconds;
        var pps = _piecesLocked / sessionSeconds;
        var averageLockDelay = _lockSamples == 0 ? 0 : (int)Math.Round((double)_totalLockDelayMs / _lockSamples);
        var timerText = _activeGameMode switch
        {
            GameMode.Sprint => $"{Math.Max(0, 40 - _linesCleared)} {TranslateWord("lines")}",
            GameMode.Ultra => TimeSpan.FromSeconds(Math.Max(0, 120 - ultraElapsedSeconds)).ToString(@"mm\:ss"),
            GameMode.Marathon => $"Lvl+ {Math.Max(0, 10 - (_linesCleared % 10))} {TranslateWord("lines")}",
            _ => "--:--"
        };
        ModeTimerText.Text = timerText;
        SessionStatsCard.Visibility = _showSessionStats ? Visibility.Visible : Visibility.Collapsed;
        SessionStatsText.Text = $"APM: {apm:0.0} • PPS: {pps:0.00} • Lock: {averageLockDelay}ms • Clears: {_quadLineClears}";
        DrawTrendChart();

        if (!_gameOver)
        {
            var modeText = _activeGameMode switch
            {
                GameMode.Survival => "SURVIVAL",
                GameMode.Sprint => "SPRINT 40",
                GameMode.Ultra => $"ULTRA {timerText}",
                GameMode.Marathon => "MARATHON",
                _ => TranslateWord("CLASSIC")
            };
            StatusText.Text = _isPaused
                ? TranslateSentence("PAUSED • P: resume • Esc")
                : TranslateSentence($"{modeText} • {_moveLeftKey}/{_moveRightKey} : move • {_rotateKey}: rotate • {_hardDropKey}: drop • {_holdKey}: hold • P: pause • Esc");
        }
    }

    private string TranslateWord(string text) => _uiLanguage switch
    {
        UiLanguage.Polish => text switch
        {
            "lines" => "linii",
            "CLASSIC" => "KLASYCZNY",
            _ => text
        },
        UiLanguage.German => text switch
        {
            "lines" => "Zeilen",
            "CLASSIC" => "KLASSISCH",
            _ => text
        },
        UiLanguage.Russian => text switch
        {
            "lines" => "линий",
            "CLASSIC" => "КЛАССИКА",
            _ => text
        },
        UiLanguage.Spanish => text switch
        {
            "lines" => "líneas",
            "CLASSIC" => "CLÁSICO",
            _ => text
        },
        _ => text
    };

    private string TranslateSentence(string text)
    {
        if (text == "PAUSED • P: resume • Esc")
        {
            return _uiLanguage switch
            {
                UiLanguage.Polish => "PAUZA • P: wznów • Esc",
                UiLanguage.German => "PAUSE • P: fortsetzen • Esc",
                UiLanguage.Russian => "ПАУЗА • P: продолжить • Esc",
                UiLanguage.Spanish => "PAUSA • P: reanudar • Esc",
                _ => text
            };
        }

        return _uiLanguage switch
        {
            UiLanguage.Polish => text.Replace("move", "ruch").Replace("rotate", "obrót").Replace("drop", "zrzut").Replace("hold", "hold").Replace("pause", "pauza"),
            UiLanguage.German => text.Replace("move", "bewegen").Replace("rotate", "drehen").Replace("drop", "Drop").Replace("hold", "halten").Replace("pause", "Pause"),
            UiLanguage.Russian => text.Replace("move", "движение").Replace("rotate", "поворот").Replace("drop", "сброс").Replace("hold", "удержание").Replace("pause", "пауза"),
            UiLanguage.Spanish => text.Replace("move", "mover").Replace("rotate", "girar").Replace("drop", "caída").Replace("hold", "hold").Replace("pause", "pausa"),
            _ => text
        };
    }

    private static string Lang(UiLanguage lang, string en, string pl, string de, string ru, string es) => lang switch
    {
        UiLanguage.Polish => pl,
        UiLanguage.German => de,
        UiLanguage.Russian => ru,
        UiLanguage.Spanish => es,
        _ => en
    };

    private static bool MatchesAny(string value, params string[] variants) => variants.Any(v => string.Equals(value, v, StringComparison.Ordinal));

    private string GetWhatsNewMessage() => Lang(
        _uiLanguage,
        "• Settings now include new toggles (session HUD, music, effects) with descriptions.\n• Added administrator password change (default: admin).\n• Highscores expanded to TOP 100 per mode.",
        "• Ustawienia mają nowe przełączniki (HUD sesji, muzyka, efekty).\n• Dodano zmianę hasła administratora (domyślnie: admin).\n• Ranking rozszerzono do TOP 100 na tryb.",
        "• Einstellungen enthalten neue Schalter (Session-HUD, Musik, Effekte).\n• Passwortänderung für Administrator hinzugefügt (Standard: admin).\n• Highscores auf TOP 100 pro Modus erweitert.",
        "• В настройках появились новые переключатели (HUD сессии, музыка, эффекты).\n• Добавлена смена пароля администратора (по умолчанию: admin).\n• Таблица рекордов расширена до ТОП 100 для каждого режима.",
        "• Ajustes con nuevos interruptores (HUD de sesión, música, efectos).\n• Se añadió cambio de contraseña de administrador (por defecto: admin).\n• Ranking ampliado a TOP 100 por modo.");

    private string[] GetTutorialSteps() => _uiLanguage switch
    {
        UiLanguage.Polish =>
        [
            "1/4 Ruch: ←/→ przesuwają klocek, ↑ obraca, Spacja robi hard drop.",
            "2/4 Hold: klawisz C odkłada klocek do HOLD i zamienia go później.",
            "3/4 Tryby: Sprint (40 linii), Ultra (120s), Marathon i Survival mają osobne rankingi.",
            "4/4 Ustawienia: możesz zmienić klawisze, DAS/ARR i włączyć tryb daltonistyczny."
        ],
        UiLanguage.German =>
        [
            "1/4 Bewegung: ←/→ verschieben den Block, ↑ rotiert, Leertaste macht Hard Drop.",
            "2/4 Hold: Taste C legt den Block in HOLD und tauscht ihn später zurück.",
            "3/4 Modi: Sprint (40 Zeilen), Ultra (120s), Marathon und Survival haben eigene Highscores.",
            "4/4 Einstellungen: Tasten, DAS/ARR und Farbenblindmodus können geändert werden."
        ],
        UiLanguage.Russian =>
        [
            "1/4 Движение: ←/→ двигают фигуру, ↑ поворачивает, Пробел делает hard drop.",
            "2/4 Hold: клавиша C отправляет фигуру в HOLD и потом меняет её обратно.",
            "3/4 Режимы: Спринт (40 линий), Ultra (120s), Marathon и Survival имеют отдельные рекорды.",
            "4/4 Настройки: можно изменить клавиши, DAS/ARR и включить режим дальтонизма."
        ],
        UiLanguage.Spanish =>
        [
            "1/4 Movimiento: ←/→ desplazan la pieza, ↑ rota, Espacio hace hard drop.",
            "2/4 Hold: tecla C guarda la pieza en HOLD y luego la intercambia.",
            "3/4 Modos: Sprint (40 líneas), Ultra (120s), Marathon y Survival tienen rankings separados.",
            "4/4 Ajustes: puedes cambiar teclas, DAS/ARR y activar modo daltónico."
        ],
        _ => TutorialStepsEnglish
    };

    private void LocalizeTextBlock(TextBlock item, string en, string pl, string de, string ru, string es)
    {
        if (MatchesAny(item.Text, en, pl, de, ru, es))
        {
            item.Text = Lang(_uiLanguage, en, pl, de, ru, es);
        }
    }

    private void LocalizeContent(Control control, string en, string pl, string de, string ru, string es)
    {
        if (control is ContentControl contentControl && contentControl.Content is string content && MatchesAny(content, en, pl, de, ru, es))
        {
            contentControl.Content = Lang(_uiLanguage, en, pl, de, ru, es);
        }
    }

    private void LocalizeToolTip(Control control, string en, string pl, string de, string ru, string es)
    {
        if (control.ToolTip is string toolTip && MatchesAny(toolTip, en, pl, de, ru, es))
        {
            control.ToolTip = Lang(_uiLanguage, en, pl, de, ru, es);
        }
    }

    private void ApplyLanguageToUi()
    {
        Title = "StackMaster";

        foreach (var item in FindVisualChildren<TextBlock>(this))
        {
            LocalizeTextBlock(item, "MAIN MENU", "MENU GŁÓWNE", "HAUPTMENÜ", "ГЛАВНОЕ МЕНЮ", "MENÚ PRINCIPAL");
            LocalizeTextBlock(item, "GAME OVER", "KONIEC GRY", "SPIEL VORBEI", "КОНЕЦ ИГРЫ", "FIN DEL JUEGO");
            LocalizeTextBlock(item, "PAUSED", "PAUZA", "PAUSE", "ПАУЗА", "PAUSA");
            LocalizeTextBlock(item, "Press P to resume", "Naciśnij P aby wznowić", "Drücke P zum Fortsetzen", "Нажмите P, чтобы продолжить", "Pulsa P para reanudar");
            LocalizeTextBlock(item, "Press SPACE to return to menu", "Naciśnij SPACJĘ aby wrócić do menu", "Drücke LEERTASTE, um zum Menü zurückzukehren", "Нажмите ПРОБЕЛ, чтобы вернуться в меню", "Pulsa ESPACIO para volver al menú");
            LocalizeTextBlock(item, "Panel 1 • No ads", "Panel 1 • Brak reklam", "Panel 1 • Keine Werbung", "Панель 1 • Нет рекламы", "Panel 1 • Sin anuncios");
            LocalizeTextBlock(item, "Panel 2 • No ads", "Panel 2 • Brak reklam", "Panel 2 • Keine Werbung", "Панель 2 • Нет рекламы", "Panel 2 • Sin anuncios");
            LocalizeTextBlock(item, "Panel 3 • No ads", "Panel 3 • Brak reklam", "Panel 3 • Keine Werbung", "Панель 3 • Нет рекламы", "Panel 3 • Sin anuncios");
            LocalizeTextBlock(item, "Arrows: move/rotate • Space: hard drop • P: pause • Esc", "Strzałki: ruch/obrót • Spacja: hard drop • P: pauza • Esc", "Pfeile: bewegen/drehen • Leertaste: Hard Drop • P: Pause • Esc", "Стрелки: движение/поворот • Пробел: жёсткий сброс • P: пауза • Esc", "Flechas: mover/girar • Espacio: hard drop • P: pausa • Esc");
            LocalizeTextBlock(item, "SESSION STATS", "STATYSTYKI SESJI", "SITZUNGSSTATISTIK", "СТАТИСТИКА СЕССИИ", "ESTADÍSTICAS DE SESIÓN");
            LocalizeTextBlock(item, "NEXT", "NASTĘPNY", "NÄCHSTER", "СЛЕДУЮЩИЙ", "SIGUIENTE");
            LocalizeTextBlock(item, "SCORE", "WYNIK", "PUNKTE", "СЧЁТ", "PUNTAJE");
            LocalizeTextBlock(item, "LEVEL", "POZIOM", "LEVEL", "УРОВЕНЬ", "NIVEL");
            LocalizeTextBlock(item, "Game settings", "Ustawienia gry", "Spieleinstellungen", "Настройки игры", "Configuración del juego");
            LocalizeTextBlock(item, "Language", "Język", "Sprache", "Язык", "Idioma");
            LocalizeTextBlock(item, "Display and audio", "Widok i audio", "Anzeige und Audio", "Экран и аудио", "Pantalla y audio");
            LocalizeTextBlock(item, "Starting level", "Poziom startowy", "Startlevel", "Начальный уровень", "Nivel inicial");
            LocalizeTextBlock(item, "Game mode", "Tryb gry", "Spielmodus", "Режим игры", "Modo de juego");
            LocalizeTextBlock(item, "Color theme", "Tryb kolorów", "Farbthema", "Цветовая тема", "Tema de color");
            LocalizeTextBlock(item, "Nickname", "Nick", "Spitzname", "Ник", "Apodo");
            LocalizeTextBlock(item, "NICK", "NICK", "SPITZNAME", "НИК", "APODO");
            LocalizeTextBlock(item, "TIMER", "TIMER", "TIMER", "ТАЙМЕР", "TEMPORIZADOR");
            LocalizeTextBlock(item, "HOLD", "HOLD", "HOLD", "УДЕРЖАНИЕ", "HOLD");
            LocalizeTextBlock(item, "TREND (20 games)", "TREND (20 gier)", "TREND (20 Spiele)", "ТРЕНД (20 игр)", "TENDENCIA (20 partidas)");
            LocalizeTextBlock(item, "VERSION 1.1.0", "WERSJA 1.1.0", "VERSION 1.1.0", "ВЕРСИЯ 1.1.0", "VERSIÓN 1.1.0");
            LocalizeTextBlock(item, "HIGHSCORE CONTROL CENTER (TOP 100)", "CENTRUM RANKINGU (TOP 100)", "HIGHSCORE-KONTROLLZENTRUM (TOP 100)", "ЦЕНТР ТАБЛИЦЫ РЕКОРДОВ (ТОП 100)", "CENTRO DE HIGHSCORES (TOP 100)");
            LocalizeTextBlock(item, "Mode ranking:", "Ranking trybu:", "Modus-Rangliste:", "Рейтинг режима:", "Ranking del modo:");
            LocalizeTextBlock(item, "Enter password to unlock nickname editing and record removal", "Podaj hasło, aby odblokować edycję nicków i usuwanie rekordów", "Passwort eingeben, um Nickname-Bearbeitung und Löschen freizuschalten", "Введите пароль, чтобы разблокировать редактирование ников и удаление", "Introduce la contraseña para desbloquear edición de apodos y borrado");
            LocalizeTextBlock(item, "Shows APM, PPS, average lock delay and line-clear stats in the right panel.", "Wyświetla APM, PPS, średni lock delay i statystyki czyszczenia linii po prawej.", "Zeigt APM, PPS, durchschnittliche Lock-Verzögerung und Zeilen-Stats rechts an.", "Показывает APM, PPS, среднюю задержку фиксации и статистику линий справа.", "Muestra APM, PPS, retardo medio de bloqueo y estadísticas de líneas a la derecha.");
            LocalizeTextBlock(item, "Background music during gameplay. Slider below controls music volume.", "Muzyka w tle podczas rozgrywki. Suwak poniżej ustawia głośność.", "Hintergrundmusik im Spiel. Regler unten steuert die Lautstärke.", "Фоновая музыка во время игры. Ползунок ниже регулирует громкость.", "Música de fondo durante la partida. El control inferior ajusta volumen.");
            LocalizeTextBlock(item, "Music volume", "Głośność muzyki", "Musiklautstärke", "Громкость музыки", "Volumen de música");
            LocalizeTextBlock(item, "Action sounds (rotate, line clear, buttons, game over).", "Dźwięki akcji (obrót, linie, przyciski, game over).", "Aktionssounds (Drehen, Zeilen, Buttons, Game Over).", "Звуки действий (поворот, линии, кнопки, конец игры).", "Sonidos de acción (giro, líneas, botones, fin de juego).");
            LocalizeTextBlock(item, "You can disable particles if they are distracting or too heavy for your hardware.", "Możesz wyłączyć cząsteczki, jeśli przeszkadzają lub obciążają sprzęt.", "Du kannst Partikel deaktivieren, wenn sie stören oder Hardware belasten.", "Можно отключить частицы, если они мешают или нагружают ПК.", "Puedes desactivar partículas si distraen o cargan tu hardware.");
            LocalizeTextBlock(item, "Effects volume", "Głośność efektów", "Effektlautstärke", "Громкость эффектов", "Volumen de efectos");
            LocalizeTextBlock(item, "Controls", "Sterowanie", "Steuerung", "Управление", "Controles");
            LocalizeTextBlock(item, "DAS is delayed auto-shift start time. ARR is repeat rate. Lower values = faster response.", "DAS to opóźnienie startu auto-przesuwu, ARR to tempo powtórzeń. Mniej = szybciej.", "DAS ist die Verzögerung, ARR die Wiederholrate. Niedriger = schneller.", "DAS — задержка авто-сдвига, ARR — частота повтора. Меньше = быстрее.", "DAS es el retardo, ARR la repetición. Menor valor = más rápido.");
            LocalizeTextBlock(item, "DAS (ms)", "DAS (ms)", "DAS (ms)", "DAS (мс)", "DAS (ms)");
            LocalizeTextBlock(item, "ARR (ms)", "ARR (ms)", "ARR (ms)", "ARR (мс)", "ARR (ms)");
            LocalizeTextBlock(item, "Soft drop ARR (ms)", "Soft drop ARR (ms)", "Soft Drop ARR (ms)", "Soft drop ARR (мс)", "ARR soft drop (ms)");
            LocalizeTextBlock(item, "Move left", "Ruch w lewo", "Nach links", "Влево", "Mover izquierda");
            LocalizeTextBlock(item, "Move right", "Ruch w prawo", "Nach rechts", "Вправо", "Mover derecha");
            LocalizeTextBlock(item, "Soft drop", "Soft drop", "Soft Drop", "Soft drop", "Soft drop");
            LocalizeTextBlock(item, "Rotate", "Obrót", "Drehen", "Поворот", "Rotar");
            LocalizeTextBlock(item, "Hard drop", "Hard drop", "Hard Drop", "Hard drop", "Hard drop");
            LocalizeTextBlock(item, "Security", "Bezpieczeństwo", "Sicherheit", "Безопасность", "Seguridad");
            LocalizeTextBlock(item, "Admin password protects highscore editing and ad panel management. Default password: admin.", "Hasło administratora chroni edycję rankingu i panel reklam. Domyślne: admin.", "Admin-Passwort schützt Highscore-Bearbeitung und Werbung. Standard: admin.", "Пароль администратора защищает редактирование рекордов и рекламу. По умолчанию: admin.", "La contraseña admin protege edición de highscores y anuncios. Por defecto: admin.");
            LocalizeTextBlock(item, "Stats and progress", "Statystyki i progres", "Statistik und Fortschritt", "Статистика и прогресс", "Estadísticas y progreso");
            LocalizeTextBlock(item, "AD MANAGEMENT", "ZARZĄDZANIE REKLAMAMI", "WERBEVERWALTUNG", "УПРАВЛЕНИЕ РЕКЛАМОЙ", "GESTIÓN DE ANUNCIOS");
            LocalizeTextBlock(item, "Enter password", "Podaj hasło", "Passwort eingeben", "Введите пароль", "Introducir contraseña");
            LocalizeTextBlock(item, "SELECTED IMAGE SETTINGS", "USTAWIENIA WYBRANEJ GRAFIKI", "EINSTELLUNGEN DES AUSGEWÄHLTEN BILDS", "НАСТРОЙКИ ВЫБРАННОГО ИЗОБРАЖЕНИЯ", "AJUSTES DE LA IMAGEN SELECCIONADA");
            LocalizeTextBlock(item, "Display time (sec)", "Czas wyświetlania (sek)", "Anzeigedauer (Sek)", "Время показа (сек)", "Tiempo de visualización (seg)");
            LocalizeTextBlock(item, "Show on panels", "Pokaż na panelach", "Auf Panels anzeigen", "Показать на панелях", "Mostrar en paneles");
            LocalizeTextBlock(item, "GLOBAL SETTINGS", "USTAWIENIA GLOBALNE", "GLOBALE EINSTELLUNGEN", "ГЛОБАЛЬНЫЕ НАСТРОЙКИ", "AJUSTES GLOBALES");
            LocalizeTextBlock(item, "Order mode", "Tryb kolejności", "Reihenfolgemodus", "Режим порядка", "Modo de orden");
            LocalizeTextBlock(item, "Schedule refresh interval (sec)", "Co ile sekund odświeżać harmonogram", "Intervall der Plan-Aktualisierung (Sek)", "Интервал обновления расписания (сек)", "Intervalo de actualización (seg)");
            LocalizeTextBlock(item, "Default duration for new images (sec)", "Domyślny czas dla nowych grafik (sek)", "Standarddauer für neue Bilder (Sek)", "Длительность по умолчанию для новых изображений (сек)", "Duración predeterminada para nuevas imágenes (seg)");
            LocalizeTextBlock(item, "Quick tutorial", "Szybki tutorial", "Kurzes Tutorial", "Быстрый туториал", "Tutorial rápido");
            LocalizeTextBlock(item, "What's new", "Co nowego", "Was ist neu", "Что нового", "Novedades");
            LocalizeTextBlock(item, "Esc: close game", "Esc: zamknij grę", "Esc: Spiel schließen", "Esc: закрыть игру", "Esc: cerrar juego");
        }

        foreach (var control in FindVisualChildren<Control>(this))
        {
            LocalizeContent(control, "WHAT'S NEW", "CO NOWEGO", "WAS IST NEU", "ЧТО НОВОГО", "NOVEDADES");
            LocalizeContent(control, "NEW GAME", "NOWA GRA", "NEUES SPIEL", "НОВАЯ ИГРА", "NUEVA PARTIDA");
            LocalizeContent(control, "HIGHSCORE", "HIGHSCORE", "HIGHSCORE", "РЕКОРДЫ", "HIGHSCORE");
            LocalizeContent(control, "SETTINGS", "USTAWIENIA", "EINSTELLUNGEN", "НАСТРОЙКИ", "AJUSTES");
            LocalizeContent(control, "Exit", "Wyjdź", "Beenden", "Выход", "Salir");
            LocalizeContent(control, "Start", "Start", "Start", "Старт", "Iniciar");
            LocalizeContent(control, "1 - Slow", "1 - Wolny", "1 - Langsam", "1 - Медленно", "1 - Lento");
            LocalizeContent(control, "2 - Dynamic", "2 - Dynamiczny", "2 - Dynamisch", "2 - Динамично", "2 - Dinámico");
            LocalizeContent(control, "3 - Turbo", "3 - Turbo", "3 - Turbo", "3 - Турбо", "3 - Turbo");
            LocalizeContent(control, "Neon Arena", "Neon Arena", "Neon Arena", "Неоновая арена", "Arena neón");
            LocalizeContent(control, "Sunset Retro", "Sunset Retro", "Sunset Retro", "Ретро закат", "Retro atardecer");
            LocalizeContent(control, "Fade Glow", "Fade Glow", "Fade Glow", "Светящийся градиент", "Brillo degradado");
            LocalizeContent(control, "CONFIRM PASSWORD", "ZATWIERDŹ HASŁO", "PASSWORT BESTÄTIGEN", "ПОДТВЕРДИТЬ ПАРОЛЬ", "CONFIRMAR CONTRASEÑA");
            LocalizeContent(control, "Colorblind mode (color + symbols)", "Tryb daltonistyczny (kolor + symbole)", "Farbenblindmodus (Farbe + Symbole)", "Режим дальтонизма (цвет + символы)", "Modo daltónico (color + símbolos)");
            LocalizeContent(control, "Change password", "Zmień hasło", "Passwort ändern", "Сменить пароль", "Cambiar contraseña");
            LocalizeContent(control, "Export stats CSV", "Eksport statystyk CSV", "Statistiken CSV exportieren", "Экспорт статистики CSV", "Exportar estadísticas CSV");
            LocalizeContent(control, "Export stats JSON", "Eksport statystyk JSON", "Statistiken JSON exportieren", "Экспорт статистики JSON", "Exportar estadísticas JSON");
            LocalizeContent(control, "Export highscores CSV", "Eksport rekordów CSV", "Highscores CSV exportieren", "Экспорт рекордов CSV", "Exportar highscores CSV");
            LocalizeContent(control, "Reset stats", "Reset statystyk", "Statistiken zurücksetzen", "Сбросить статистику", "Restablecer estadísticas");
            LocalizeContent(control, "Show session stats in HUD", "Pokazuj statystyki sesji w HUD", "Sitzungsstatistiken im HUD anzeigen", "Показывать статистику сессии в HUD", "Mostrar estadísticas de sesión en HUD");
            LocalizeContent(control, "Enable music", "Włącz muzykę", "Musik aktivieren", "Включить музыку", "Activar música");
            LocalizeContent(control, "Enable sound effects", "Włącz efekty dźwiękowe", "Soundeffekte aktivieren", "Включить звуковые эффекты", "Activar efectos de sonido");
            LocalizeContent(control, "Show landing particles", "Pokaż cząsteczki przy lądowaniu", "Landepartikel anzeigen", "Показывать частицы при приземлении", "Mostrar partículas al aterrizar");
            LocalizeContent(control, "Unlock", "Odblokuj", "Freischalten", "Разблокировать", "Desbloquear");
            LocalizeContent(control, "Close", "Zamknij", "Schließen", "Закрыть", "Cerrar");
            LocalizeContent(control, "Add image", "Dodaj grafikę", "Bild hinzufügen", "Добавить изображение", "Agregar imagen");
            LocalizeContent(control, "Delete", "Usuń", "Löschen", "Удалить", "Eliminar");
            LocalizeContent(control, "Panel 1", "Panel 1", "Panel 1", "Панель 1", "Panel 1");
            LocalizeContent(control, "Panel 2", "Panel 2", "Panel 2", "Панель 2", "Panel 2");
            LocalizeContent(control, "Panel 3", "Panel 3", "Panel 3", "Панель 3", "Panel 3");
            LocalizeContent(control, "Save image settings", "Zapisz ustawienia grafiki", "Bildeinstellungen speichern", "Сохранить настройки изображения", "Guardar ajustes de imagen");
            LocalizeContent(control, "Sequential", "Po kolei", "Der Reihe nach", "По порядку", "En orden");
            LocalizeContent(control, "Random", "Losowo", "Zufällig", "Случайно", "Aleatorio");
            LocalizeContent(control, "Save global settings", "Zapisz ustawienia globalne", "Globale Einstellungen speichern", "Сохранить глобальные настройки", "Guardar ajustes globales");
            LocalizeContent(control, "Ad options (password)", "Opcje reklam (hasło)", "Werbeoptionen (Passwort)", "Параметры рекламы (пароль)", "Opciones de anuncios (contraseña)");
            LocalizeContent(control, "Skip", "Pomiń", "Überspringen", "Пропустить", "Saltar");
            LocalizeContent(control, "Next", "Dalej", "Weiter", "Далее", "Siguiente");
            LocalizeContent(control, "Finish", "Zakończ", "Fertig", "Завершить", "Finalizar");
            LocalizeContent(control, "OK", "OK", "OK", "ОК", "OK");

            LocalizeToolTip(control, "DAS: delayed auto-shift start after holding (ms).", "DAS: opóźnienie auto-przesuwu po przytrzymaniu (ms).", "DAS: Verzögerung nach Halten (ms).", "DAS: задержка авто-сдвига после удержания (мс).", "DAS: retraso tras mantener pulsado (ms).");
            LocalizeToolTip(control, "ARR: repeat rate after DAS (ms).", "ARR: szybkość powtarzania po DAS (ms).", "ARR: Wiederholrate nach DAS (ms).", "ARR: частота повтора после DAS (мс).", "ARR: repetición tras DAS (ms).");
            LocalizeToolTip(control, "Soft drop repeat speed while holding (ms). 0 = every frame.", "Jak szybko powtarzać soft drop przy przytrzymaniu (ms). 0 = co klatkę.", "Soft-Drop-Wiederholung beim Halten (ms). 0 = jedes Frame.", "Скорость повтора soft drop при удержании (мс). 0 = каждый кадр.", "Velocidad de repetición soft drop al mantener (ms). 0 = cada frame.");
            LocalizeToolTip(control, "Left movement key.", "Klawisz ruchu w lewo.", "Taste für Bewegung nach links.", "Клавиша движения влево.", "Tecla para mover a la izquierda.");
            LocalizeToolTip(control, "Right movement key.", "Klawisz ruchu w prawo.", "Taste für Bewegung nach rechts.", "Клавиша движения вправо.", "Tecla para mover a la derecha.");
            LocalizeToolTip(control, "Soft drop key.", "Klawisz miękkiego opuszczania (soft drop).", "Taste für Soft Drop.", "Клавиша soft drop.", "Tecla de soft drop.");
            LocalizeToolTip(control, "Rotate key.", "Klawisz obrotu klocka.", "Taste zum Drehen.", "Клавиша поворота.", "Tecla de rotación.");
            LocalizeToolTip(control, "Hard drop key.", "Klawisz natychmiastowego zrzutu (hard drop).", "Taste für Hard Drop.", "Клавиша hard drop.", "Tecla de hard drop.");
            LocalizeToolTip(control, "Hold piece key.", "Klawisz przechowania klocka (hold).", "Taste für Hold.", "Клавиша hold.", "Tecla de hold.");
            LocalizeToolTip(control, "Export recent sessions to CSV.", "Eksport ostatnich sesji do CSV.", "Letzte Sitzungen als CSV exportieren.", "Экспорт последних сессий в CSV.", "Exportar sesiones recientes a CSV.");
            LocalizeToolTip(control, "Export recent sessions to JSON.", "Eksport ostatnich sesji do JSON.", "Letzte Sitzungen als JSON exportieren.", "Экспорт последних сессий в JSON.", "Exportar sesiones recientes a JSON.");
            LocalizeToolTip(control, "Export per-mode highscores to CSV.", "Eksport rankingów per tryb do CSV.", "Highscores je Modus als CSV exportieren.", "Экспорт рекордов по режимам в CSV.", "Exportar highscores por modo a CSV.");
            LocalizeToolTip(control, "Clears session history, trend chart and current stat counters.", "Czyści historię sesji, wykres trendu i bieżące liczniki statystyk.", "Löscht Sitzungsverlauf, Trenddiagramm und aktuelle Zähler.", "Очищает историю сессий, график тренда и текущие счётчики.", "Limpia historial de sesión, gráfico y contadores actuales.");
        }

        var modeItems = new[] { "Classic", "Survival", "Sprint (40 lines)", "Ultra (120s)", "Marathon" };
        if (GameModeComboBox.Items.Count == modeItems.Length)
        {
            var translated = _uiLanguage switch
            {
                UiLanguage.Polish => new[] { "Klasyczny", "Survival", "Sprint (40 linii)", "Ultra (120s)", "Marathon" },
                UiLanguage.German => new[] { "Klassisch", "Survival", "Sprint (40 Zeilen)", "Ultra (120s)", "Marathon" },
                UiLanguage.Russian => new[] { "Классический", "Survival", "Спринт (40 линий)", "Ultra (120s)", "Marathon" },
                UiLanguage.Spanish => new[] { "Clásico", "Survival", "Sprint (40 líneas)", "Ultra (120s)", "Marathon" },
                _ => modeItems
            };

            for (var i = 0; i < GameModeComboBox.Items.Count; i++)
            {
                if (GameModeComboBox.Items[i] is ComboBoxItem item)
                {
                    item.Content = translated[i];
                }
            }
        }

        UpdateHud();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null)
        {
            yield break;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    private void UpdateBoardLayout()
    {
        var availableHeight = Math.Max(420, RootGrid.ActualHeight - 48);
        _cellSize = Math.Floor(availableHeight / BoardHeight);
        var boardWidthPx = _cellSize * BoardWidth;
        var boardHeightPx = _cellSize * BoardHeight;

        GameCanvas.Width = boardWidthPx;
        GameCanvas.Height = boardHeightPx;
        EffectCanvas.Width = boardWidthPx;
        EffectCanvas.Height = boardHeightPx;
        BoardBorder.Width = boardWidthPx + 12;
        BoardBorder.Height = boardHeightPx + 12;
    }

    private void Draw()
    {
        GameCanvas.Children.Clear();

        var emptyBrush = new SolidColorBrush(_emptyCellColor);
        for (var y = 0; y < BoardHeight; y++)
        {
            for (var x = 0; x < BoardWidth; x++)
            {
                DrawCell(x, y, _board[y, x] ?? emptyBrush, _board[y, x] is null ? 0.33 : 1);
            }
        }

        if (!_gameOver && _isGameStarted)
        {
            if (!_isPaused)
            {
                DrawGhostPiece();
            }

            foreach (var cell in _currentPiece.Cells)
            {
                DrawCell(_currentX + (int)cell.X, _currentY + (int)cell.Y, _currentPiece.Color, 1);
            }
        }
    }

    private void DrawGhostPiece()
    {
        var ghostY = _engine.CalculateGhostY();

        if (ghostY == _currentY)
        {
            return;
        }

        foreach (var cell in _currentPiece.Cells)
        {
            var ghostX = _currentX + (int)cell.X;
            var ghostCellY = ghostY + (int)cell.Y;
            DrawCell(ghostX, ghostCellY, _currentPiece.Color, 0.22);
        }
    }

    private void DrawCell(int x, int y, Brush brush, double opacity)
    {
        var rect = new Rectangle
        {
            Width = _cellSize - 2,
            Height = _cellSize - 2,
            RadiusX = 6,
            RadiusY = 6,
            Fill = brush,
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            Opacity = opacity
        };

        Canvas.SetLeft(rect, x * _cellSize + 1);
        Canvas.SetTop(rect, y * _cellSize + 1);
        GameCanvas.Children.Add(rect);

        if (!_colorblindMode || opacity < 0.45)
        {
            return;
        }

        var pieceIndex = GetPieceIndexForBrush(brush);
        if (pieceIndex < 0 || pieceIndex >= PieceSymbols.Length)
        {
            return;
        }

        DrawPatternOverlay(x, y, pieceIndex);
    }

    private int GetPieceIndexForBrush(Brush brush)
    {
        for (var i = 0; i < _activePaletteBrushes.Length; i++)
        {
            if (ReferenceEquals(_activePaletteBrushes[i], brush))
            {
                return i;
            }
        }

        return -1;
    }

    private void DrawPatternOverlay(int x, int y, int pieceIndex)
    {
        var left = x * _cellSize + 1;
        var top = y * _cellSize + 1;
        var size = _cellSize - 2;

        var symbol = new TextBlock
        {
            Text = PieceSymbols[pieceIndex],
            Width = size,
            TextAlignment = TextAlignment.Center,
            Foreground = Brushes.Black,
            FontSize = Math.Max(10, size * 0.45),
            FontWeight = FontWeights.Bold,
            Opacity = 0.6
        };

        Canvas.SetLeft(symbol, left);
        Canvas.SetTop(symbol, top + size * 0.2);
        GameCanvas.Children.Add(symbol);

        var stroke = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255));
        if (pieceIndex % 3 == 0)
        {
            GameCanvas.Children.Add(new Line { X1 = left + 2, Y1 = top + 2, X2 = left + size - 2, Y2 = top + size - 2, Stroke = stroke, StrokeThickness = 1.5 });
        }
        else if (pieceIndex % 3 == 1)
        {
            GameCanvas.Children.Add(new Line { X1 = left + size - 2, Y1 = top + 2, X2 = left + 2, Y2 = top + size - 2, Stroke = stroke, StrokeThickness = 1.5 });
        }
        else
        {
            GameCanvas.Children.Add(new Line { X1 = left + 2, Y1 = top + size * 0.5, X2 = left + size - 2, Y2 = top + size * 0.5, Stroke = stroke, StrokeThickness = 1.5 });
        }
    }

    private void DrawNextPiece()
    {
        NextPieceCanvas.Children.Clear();

        if (_nextPiece is null)
        {
            return;
        }

        const double previewCell = 24;

        foreach (var cell in _nextPiece.Cells)
        {
            var rect = new Rectangle
            {
                Width = previewCell,
                Height = previewCell,
                RadiusX = 5,
                RadiusY = 5,
                Fill = _nextPiece.Color,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            Canvas.SetLeft(rect, cell.X * previewCell + 18);
            Canvas.SetTop(rect, cell.Y * previewCell + 16);
            NextPieceCanvas.Children.Add(rect);
        }
    }

    private void DrawHoldPiece()
    {
        HoldPieceCanvas.Children.Clear();

        if (_holdPiece is null)
        {
            return;
        }

        const double previewCell = 24;
        foreach (var cell in _holdPiece.Cells)
        {
            var rect = new Rectangle
            {
                Width = previewCell,
                Height = previewCell,
                RadiusX = 5,
                RadiusY = 5,
                Fill = _holdPiece.Color,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            Canvas.SetLeft(rect, cell.X * previewCell + 18);
            Canvas.SetTop(rect, cell.Y * previewCell + 16);
            HoldPieceCanvas.Children.Add(rect);
        }
    }

    private void HoldCurrentPiece()
    {
        if (_holdUsedThisTurn)
        {
            return;
        }

        var previousHold = _holdPiece;
        _holdPiece = ClonePiece(_currentPiece);
        _holdUsedThisTurn = true;

        if (previousHold is null)
        {
            _currentPiece = _nextPiece;
            _nextPiece = CreateRandomPiece();
        }
        else
        {
            _currentPiece = ClonePiece(previousHold);
        }

        _currentX = BoardWidth / 2 - 2;
        _currentY = 0;
        _groundedTicks = 0;
        _lockResetsUsed = 0;

        if (!IsPositionValid(_currentX, _currentY, _currentPiece.Cells))
        {
            OnGameOver();
            return;
        }

        DrawNextPiece();
        DrawHoldPiece();
    }

    private static Tetromino ClonePiece(Tetromino piece)
    {
        var clonedCells = piece.Cells.Select(cell => new Point(cell.X, cell.Y)).ToArray();
        return new Tetromino(clonedCells, piece.Color, piece.IsSquare);
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }

        if (StartMenuOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Enter)
            {
                StartButton_Click(this, new RoutedEventArgs());
            }

            return;
        }

        if (_gameOver)
        {
            if (e.Key == Key.Space)
            {
                GameOverOverlay.Visibility = Visibility.Collapsed;
                StartMenuOverlay.Visibility = Visibility.Visible;
                ShowStartMenuSection(StartMenuSection.NewGame);
                ResetAdManagerUi();
                _isGameStarted = false;
            }

            return;
        }

        if (e.Key == Key.P)
        {
            TogglePause();
            return;
        }

        if (_isPaused)
        {
            return;
        }

        if (e.Key == _moveLeftKey || e.Key == _moveRightKey)
        {
            var direction = e.Key == _moveLeftKey ? -1 : 1;
            _horizontalDirection = direction;
            _horizontalPressedAtMs = _inputStopwatch.ElapsedMilliseconds;
            _lastHorizontalRepeatMs = _horizontalPressedAtMs;

            if (!e.IsRepeat)
            {
                var moved = TryMove(_currentX + direction, _currentY, _currentPiece.Cells);
                RefreshLockDelayAfterPlayerAction(moved);
                if (moved)
                {
                    _playerActions++;
                }
                PlayEffect("rotate");
            }

            Draw();
            return;
        }

        if (e.Key == _softDropKey)
        {
            _softDropPressed = true;
            _softDropPressedAtMs = _inputStopwatch.ElapsedMilliseconds;
            _lastSoftDropRepeatMs = _softDropPressedAtMs;
            var moved = TryMove(_currentX, _currentY + 1, _currentPiece.Cells);
            if (moved)
            {
                _groundedTicks = 0;
                _lockResetsUsed = 0;
                _playerActions++;
                EmitFallingTrailParticles(forceBurst: true);
                Draw();
            }

            return;
        }

        if (e.Key == _rotateKey)
        {
            var rotated = RotateCurrentPiece();
            RefreshLockDelayAfterPlayerAction(rotated);
            if (rotated)
            {
                _playerActions++;
            }
            PlayEffect("rotate");
            Draw();
            return;
        }

        if (e.Key == _hardDropKey)
        {
            _playerActions++;
            HardDrop();
            return;
        }

        if (e.Key == _holdKey)
        {
            _playerActions++;
            HoldCurrentPiece();
            Draw();
            return;
        }

    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == _moveLeftKey && _horizontalDirection < 0)
        {
            _horizontalDirection = Keyboard.IsKeyDown(_moveRightKey) ? 1 : 0;
            _horizontalPressedAtMs = _inputStopwatch.ElapsedMilliseconds;
            _lastHorizontalRepeatMs = _horizontalPressedAtMs;
        }
        else if (e.Key == _moveRightKey && _horizontalDirection > 0)
        {
            _horizontalDirection = Keyboard.IsKeyDown(_moveLeftKey) ? -1 : 0;
            _horizontalPressedAtMs = _inputStopwatch.ElapsedMilliseconds;
            _lastHorizontalRepeatMs = _horizontalPressedAtMs;
        }

        if (e.Key == _softDropKey)
        {
            _softDropPressed = false;
        }
    }

    private void TogglePause()
    {
        if (!_isGameStarted || _gameOver)
        {
            return;
        }

        _isPaused = !_isPaused;
        PauseOverlay.Visibility = _isPaused ? Visibility.Visible : Visibility.Collapsed;

        if (_isPaused)
        {
            _timer.Stop();
            _inputTimer.Stop();
            if (_activeGameMode == GameMode.Ultra)
            {
                _ultraStopwatch.Stop();
            }

            _sessionStopwatch.Stop();
            StopBackgroundMusic();
        }
        else
        {
            _timer.Start();
            _inputTimer.Start();
            if (_activeGameMode == GameMode.Ultra)
            {
                _ultraStopwatch.Start();
            }

            _sessionStopwatch.Start();
            PlayBackgroundMusic();
        }

        UpdateHud();
        Draw();
    }

    private static string ResolveAdStoragePath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var projectFile = Directory.GetFiles(current.FullName, "*.csproj").FirstOrDefault() ?? string.Empty;
            if (File.Exists(projectFile))
            {
                return IOPath.Combine(current.FullName, "AdAssets");
            }

            current = current.Parent;
        }

        return IOPath.Combine(AppContext.BaseDirectory, "AdAssets");
    }

    private void ConfigureAdStates()
    {
        _adStates.Clear();
        _adStates[AdPanel.Top] = new AdPlaybackState();
        _adStates[AdPanel.Middle] = new AdPlaybackState();
        _adStates[AdPanel.Bottom] = new AdPlaybackState();
    }

    private void ApplyLoadedGlobalSettingsToUi()
    {
        var rotationBox = GetRotationIntervalTextBox();
        var defaultDurationBox = GetDefaultAdDurationTextBox();
        if (rotationBox is not null)
        {
            rotationBox.Text = _rotationIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        }

        if (defaultDurationBox is not null)
        {
            defaultDurationBox.Text = _defaultAdDurationSeconds.ToString(CultureInfo.InvariantCulture);
        }

        var orderModeComboBox = GetOrderModeComboBox();
        if (orderModeComboBox is not null)
        {
            orderModeComboBox.SelectedIndex = _adOrderMode == AdOrderMode.Random ? 1 : 0;
        }

        _adTimer.Interval = TimeSpan.FromSeconds(_rotationIntervalSeconds);
    }

    private static int NormalizeSeconds(int value, int fallback)
    {
        if (value < MinAdSeconds || value > MaxAdSeconds)
        {
            return fallback;
        }

        return value;
    }

    private static bool ParseSeconds(string? text, int fallback, out int value)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            value = fallback;
            return false;
        }

        value = NormalizeSeconds(parsed, fallback);
        return parsed == value;
    }

    private static int ParseNonNegativeInt(string? text, int fallback)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
            ? parsed
            : fallback;
    }

    private static Key ParseKey(string? keyText, Key fallback)
    {
        if (!string.IsNullOrWhiteSpace(keyText) && Enum.TryParse<Key>(keyText, true, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private void ApplyControlSettingsToUi()
    {
        DasTextBox.Text = _dasMs.ToString(CultureInfo.InvariantCulture);
        ArrTextBox.Text = _arrMs.ToString(CultureInfo.InvariantCulture);
        SoftDropArrTextBox.Text = _softDropArrMs.ToString(CultureInfo.InvariantCulture);
        MoveLeftKeyTextBox.Text = _moveLeftKey.ToString();
        MoveRightKeyTextBox.Text = _moveRightKey.ToString();
        SoftDropKeyTextBox.Text = _softDropKey.ToString();
        RotateKeyTextBox.Text = _rotateKey.ToString();
        HardDropKeyTextBox.Text = _hardDropKey.ToString();
        HoldKeyTextBox.Text = _holdKey.ToString();
    }

    private void LoadHighScores()
    {
        _highScoresByMode.Clear();
        foreach (var mode in Enum.GetValues<GameMode>())
        {
            _highScoresByMode[mode] = [];
        }

        if (!File.Exists(_highScoresPath))
        {
            RefreshHighScores();
            return;
        }

        try
        {
            var json = File.ReadAllText(_highScoresPath);
            var parsed = HighscorePersistence.Parse(json, Enum.GetNames<GameMode>());
            foreach (var mode in Enum.GetValues<GameMode>())
            {
                var key = mode.ToString();
                _highScoresByMode[mode] = parsed.TryGetValue(key, out var entries)
                    ? entries.Select(e => new ScoreEntry(e.Name, e.Points)).ToList()
                    : [];
            }
        }
        catch
        {
            foreach (var mode in Enum.GetValues<GameMode>())
            {
                _highScoresByMode[mode] = [];
            }
        }

        RefreshHighScores();
    }

    private void SaveHighScores()
    {
        var modes = _highScoresByMode.ToDictionary(
            pair => pair.Key.ToString(),
            pair => pair.Value.Select(e => new ScoreEntryData(e.Name, e.Points)).ToList());

        var json = HighscorePersistence.Serialize(modes, options: JsonWriteOptions);
        File.WriteAllText(_highScoresPath, json);
    }

    private void LoadSettings()
    {
        if (!File.Exists(_settingsPath))
        {
            SaveSettings();
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = SettingsPersistence.DeserializeOrDefault(json, CreateDefaultSettings());

            _isLoadingSettings = true;
            NickTextBox.Text = string.IsNullOrWhiteSpace(settings.Nick) ? "Player" : settings.Nick;
            StartLevelComboBox.SelectedIndex = Math.Clamp(settings.StartLevelIndex, 0, 2);
            GameModeComboBox.SelectedIndex = Math.Clamp(settings.GameModeIndex, 0, 4);
            ThemeComboBox.SelectedIndex = Math.Clamp(settings.ThemeIndex, 0, 2);
            ColorblindModeCheckBox.IsChecked = settings.ColorblindMode;
            ShowSessionStatsCheckBox.IsChecked = settings.ShowSessionStats;
            MusicEnabledCheckBox.IsChecked = settings.MusicEnabled;
            EffectsEnabledCheckBox.IsChecked = settings.EffectsEnabled;
            LockParticlesCheckBox.IsChecked = settings.LockParticlesEnabled;
            MusicVolumeSlider.Value = Math.Clamp(settings.MusicVolume, 0, 1);
            EffectsVolumeSlider.Value = Math.Clamp(settings.EffectsVolume, 0, 1);
            _dasMs = Math.Clamp(settings.DasMs, 0, 500);
            _arrMs = Math.Clamp(settings.ArrMs, 0, 300);
            _softDropArrMs = Math.Clamp(settings.SoftDropArrMs, 0, 200);
            _moveLeftKey = ParseKey(settings.MoveLeftKey, Key.Left);
            _moveRightKey = ParseKey(settings.MoveRightKey, Key.Right);
            _softDropKey = ParseKey(settings.SoftDropKey, Key.Down);
            _rotateKey = ParseKey(settings.RotateKey, Key.Up);
            _hardDropKey = ParseKey(settings.HardDropKey, Key.Space);
            _holdKey = ParseKey(settings.HoldKey, Key.C);
            _colorblindMode = settings.ColorblindMode;
            _showSessionStats = settings.ShowSessionStats == true;
            _musicEnabled = settings.MusicEnabled == true;
            _effectsEnabled = settings.EffectsEnabled == true;
            _lockParticlesEnabled = settings.LockParticlesEnabled == true;
            _adminPassword = string.IsNullOrWhiteSpace(settings.AdminPassword) ? DefaultAdminPassword : settings.AdminPassword;
            _uiLanguage = ParseLanguageCode(settings.LanguageCode);
            LanguageComboBox.SelectedIndex = (int)_uiLanguage;
            AdminPasswordStatusText.Text = $"{Lang(_uiLanguage, "Admin password set (default", "Hasło administratora ustawione (domyślne", "Admin-Passwort gesetzt (Standard", "Пароль администратора установлен (по умолчанию", "Contraseña admin establecida (por defecto")}: {DefaultAdminPassword}).";
            ApplyControlSettingsToUi();
            SessionStatsCard.Visibility = _showSessionStats ? Visibility.Visible : Visibility.Collapsed;
            ApplyAudioSettingsUi();
            ApplyLanguageToUi();
        }
        catch
        {
            // ignore settings corruption and keep defaults
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void SaveSettings()
    {
        _dasMs = Math.Clamp(ParseNonNegativeInt(DasTextBox.Text, _dasMs), 0, 500);
        _arrMs = Math.Clamp(ParseNonNegativeInt(ArrTextBox.Text, _arrMs), 0, 300);
        _softDropArrMs = Math.Clamp(ParseNonNegativeInt(SoftDropArrTextBox.Text, _softDropArrMs), 0, 200);
        _moveLeftKey = ParseKey(MoveLeftKeyTextBox.Text, _moveLeftKey);
        _moveRightKey = ParseKey(MoveRightKeyTextBox.Text, _moveRightKey);
        _softDropKey = ParseKey(SoftDropKeyTextBox.Text, _softDropKey);
        _rotateKey = ParseKey(RotateKeyTextBox.Text, _rotateKey);
        _hardDropKey = ParseKey(HardDropKeyTextBox.Text, _hardDropKey);
        _holdKey = ParseKey(HoldKeyTextBox.Text, _holdKey);
        ApplyControlSettingsToUi();

        _colorblindMode = ColorblindModeCheckBox.IsChecked == true;
        _showSessionStats = ShowSessionStatsCheckBox.IsChecked == true;
        _musicEnabled = MusicEnabledCheckBox.IsChecked == true;
        _effectsEnabled = EffectsEnabledCheckBox.IsChecked == true;
        _lockParticlesEnabled = LockParticlesCheckBox.IsChecked == true;

        var settings = new GameSettings(
            NickTextBox.Text.Trim(),
            StartLevelComboBox.SelectedIndex,
            GameModeComboBox.SelectedIndex,
            ThemeComboBox.SelectedIndex,
            MusicVolumeSlider.Value,
            EffectsVolumeSlider.Value,
            _dasMs,
            _arrMs,
            _softDropArrMs,
            _moveLeftKey.ToString(),
            _moveRightKey.ToString(),
            _softDropKey.ToString(),
            _rotateKey.ToString(),
            _hardDropKey.ToString(),
            _holdKey.ToString(),
            _colorblindMode,
            _showSessionStats,
            _musicEnabled,
            _effectsEnabled,
            _lockParticlesEnabled,
            _adminPassword,
            ToLanguageCode(_uiLanguage));

        var json = SettingsPersistence.Serialize(settings, JsonWriteOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private void EnsureAdStorage()
    {
        Directory.CreateDirectory(_adStorageFolder);
    }

    private void LoadAds()
    {
        _ads.Clear();

        if (!File.Exists(_adManifestPath))
        {
            SaveAdsManifest();
            return;
        }

        try
        {
            var json = File.ReadAllText(_adManifestPath);
            var root = JsonSerializer.Deserialize<AdManifestRoot>(json);

            List<AdManifestItem> items;
            if (root is null)
            {
                items = JsonSerializer.Deserialize<List<AdManifestItem>>(json) ?? [];
            }
            else
            {
                _rotationIntervalSeconds = NormalizeSeconds(root.RotationIntervalSeconds, 1);
                _defaultAdDurationSeconds = NormalizeSeconds(root.DefaultAdDurationSeconds, 10);
                _adOrderMode = root.OrderMode;
                items = root.Ads;
            }

            foreach (var item in items)
            {
                var fullPath = IOPath.Combine(_adStorageFolder, item.FileName);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                var duration = NormalizeSeconds(item.DurationSeconds, _defaultAdDurationSeconds);
                var panels = item.Panels == 0 ? AdPanel.Top | AdPanel.Middle | AdPanel.Bottom : item.Panels;
                _ads.Add(new AdEntry(item.FileName, item.DisplayName, duration, panels));
            }
        }
        catch
        {
            _ads.Clear();
        }
    }

    private void SaveAdsManifest()
    {
        List<AdManifestItem> items = [.. _ads.Select(a => new AdManifestItem(a.FileName, a.DisplayName, a.DurationSeconds, a.Panels))];
        var root = new AdManifestRoot(_defaultAdDurationSeconds, _rotationIntervalSeconds, _adOrderMode, items);
        var json = JsonSerializer.Serialize(root, JsonWriteOptions);
        File.WriteAllText(_adManifestPath, json);
    }

    private void RotateAds()
    {
        HashSet<string> usedInFrame = [];

        RenderPanel(AdPanel.Top, AdImageTop, AdPlaceholderTop, usedInFrame);
        RenderPanel(AdPanel.Middle, AdImageMiddle, AdPlaceholderMiddle, usedInFrame);
        RenderPanel(AdPanel.Bottom, AdImageBottom, AdPlaceholderBottom, usedInFrame);
    }

    private void RenderPanel(AdPanel panel, Image target, TextBlock placeholder, HashSet<string> usedInFrame)
    {
        if (!_adStates.TryGetValue(panel, out var state))
        {
            return;
        }

        if (_ads.Count == 0)
        {
            state.CurrentAd = null;
            ShowNoAds(target, placeholder);
            return;
        }

        var shouldSwitch = state.CurrentAd is null || !_ads.Contains(state.CurrentAd) || !state.CurrentAd.IsVisibleOn(panel);

        if (!shouldSwitch)
        {
            state.ElapsedSeconds += _rotationIntervalSeconds;
            if (state.ElapsedSeconds >= state.CurrentAd!.DurationSeconds)
            {
                shouldSwitch = true;
            }
        }

        if (!shouldSwitch && state.CurrentAd is not null && usedInFrame.Contains(state.CurrentAd.FileName))
        {
            shouldSwitch = true;
        }

        if (shouldSwitch)
        {
            ShowNextAdForPanel(panel, state, target, placeholder, usedInFrame);
            return;
        }

        if (state.CurrentAd is not null)
        {
            usedInFrame.Add(state.CurrentAd.FileName);
        }
    }

    private void ShowNextAdForPanel(AdPanel panel, AdPlaybackState state, Image target, TextBlock placeholder, HashSet<string> usedInFrame)
    {
        var nextIndex = FindNextAdIndex(panel, state.LastIndex, usedInFrame);
        if (nextIndex < 0)
        {
            state.CurrentAd = null;
            state.LastIndex = -1;
            state.ElapsedSeconds = 0;
            ShowNoAds(target, placeholder);
            return;
        }

        state.LastIndex = nextIndex;
        state.CurrentAd = _ads[nextIndex];
        state.ElapsedSeconds = 0;
        usedInFrame.Add(state.CurrentAd.FileName);
        ShowAd(target, placeholder, state.CurrentAd);
    }

    private int FindNextAdIndex(AdPanel panel, int afterIndex, HashSet<string> usedInFrame)
    {
        if (_ads.Count == 0)
        {
            return -1;
        }

        List<int> eligible = [];
        for (var i = 0; i < _ads.Count; i++)
        {
            if (_ads[i].IsVisibleOn(panel) && !usedInFrame.Contains(_ads[i].FileName))
            {
                eligible.Add(i);
            }
        }

        if (eligible.Count == 0)
        {
            return -1;
        }

        if (_adOrderMode == AdOrderMode.Random)
        {
            var idx = _random.Next(eligible.Count);
            return eligible[idx];
        }

        for (var offset = 1; offset <= _ads.Count; offset++)
        {
            var candidate = (afterIndex + offset + _ads.Count) % _ads.Count;
            if (eligible.Contains(candidate))
            {
                return candidate;
            }
        }

        return eligible[0];
    }

    private static void ShowNoAds(Image target, TextBlock placeholder)
    {
        target.Source = null;
        target.Opacity = 0;
        placeholder.Visibility = Visibility.Visible;
    }

    private void ShowAd(Image target, TextBlock placeholder, AdEntry ad)
    {
        var filePath = IOPath.Combine(_adStorageFolder, ad.FileName);
        if (!File.Exists(filePath))
        {
            ShowNoAds(target, placeholder);
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(filePath);
        bitmap.EndInit();
        bitmap.Freeze();

        target.Source = bitmap;
        placeholder.Visibility = Visibility.Collapsed;

        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(700),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        target.BeginAnimation(OpacityProperty, fade);
    }

    private void AddAdButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        var dialog = new OpenFileDialog
        {
            Title = Lang(_uiLanguage, "Select an image", "Wybierz grafikę", "Bild auswählen", "Выбрать изображение", "Seleccionar imagen"),
            Filter = "Obrazy (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            EnsureAdStorage();
            var extension = IOPath.GetExtension(dialog.FileName);
            var savedName = $"{DateTime.Now:yyyyMMdd_HHmmssfff}{extension}";
            var destinationPath = IOPath.Combine(_adStorageFolder, savedName);
            File.Copy(dialog.FileName, destinationPath, overwrite: false);

            _ads.Add(new AdEntry(savedName, IOPath.GetFileName(dialog.FileName), _defaultAdDurationSeconds, AdPanel.Top | AdPanel.Middle | AdPanel.Bottom));
            SaveAdsManifest();
            AdListBox.SelectedIndex = _ads.Count - 1;
            LoadSelectedAdSettings();
            RotateAds();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{Lang(_uiLanguage, "Failed to add image", "Nie udało się dodać grafiki", "Bild konnte nicht hinzugefügt werden", "Не удалось добавить изображение", "No se pudo agregar la imagen")}: {ex.Message}", Lang(_uiLanguage, "Error", "Błąd", "Fehler", "Ошибка", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteAdButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        if (AdListBox.SelectedItem is not AdEntry selected)
        {
            return;
        }

        var selectedIndex = AdListBox.SelectedIndex;
        var filePath = IOPath.Combine(_adStorageFolder, selected.FileName);
        _ads.Remove(selected);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        SaveAdsManifest();

        if (_ads.Count == 0)
        {
            RotateAds();
            return;
        }

        AdListBox.SelectedIndex = Math.Min(selectedIndex, _ads.Count - 1);
        LoadSelectedAdSettings();
        RotateAds();
    }

    private void MoveAdUpButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        var index = AdListBox.SelectedIndex;
        if (index <= 0)
        {
            return;
        }

        _ads.Move(index, index - 1);
        AdListBox.SelectedIndex = index - 1;
        SaveAdsManifest();
        RotateAds();
    }

    private void MoveAdDownButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        var index = AdListBox.SelectedIndex;
        if (index < 0 || index >= _ads.Count - 1)
        {
            return;
        }

        _ads.Move(index, index + 1);
        AdListBox.SelectedIndex = index + 1;
        SaveAdsManifest();
        RotateAds();
    }

    private void AdListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadSelectedAdSettings();
    }

    private void LoadSelectedAdSettings()
    {
        if (AdListBox.SelectedItem is not AdEntry selected)
        {
            return;
        }

        var durationBox = GetSelectedAdDurationTextBox();
        var top = GetSelectedAdTopCheckBox();
        var middle = GetSelectedAdMiddleCheckBox();
        var bottom = GetSelectedAdBottomCheckBox();

        if (durationBox is null || top is null || middle is null || bottom is null)
        {
            return;
        }

        durationBox.Text = selected.DurationSeconds.ToString(CultureInfo.InvariantCulture);
        top.IsChecked = selected.IsVisibleOn(AdPanel.Top);
        middle.IsChecked = selected.IsVisibleOn(AdPanel.Middle);
        bottom.IsChecked = selected.IsVisibleOn(AdPanel.Bottom);
    }

    private void SaveSelectedAdSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        if (AdListBox.SelectedItem is not AdEntry selected)
        {
            MessageBox.Show(Lang(_uiLanguage, "Select an image from the list first.", "Najpierw wybierz grafikę z listy.", "Wähle zuerst ein Bild aus der Liste.", "Сначала выберите изображение из списка.", "Primero selecciona una imagen de la lista."), Lang(_uiLanguage, "Information", "Informacja", "Information", "Информация", "Información"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var durationBox = GetSelectedAdDurationTextBox();
        var top = GetSelectedAdTopCheckBox();
        var middle = GetSelectedAdMiddleCheckBox();
        var bottom = GetSelectedAdBottomCheckBox();

        if (durationBox is null || top is null || middle is null || bottom is null)
        {
            return;
        }

        _ = ParseSeconds(durationBox.Text, selected.DurationSeconds, out var duration);
        var panels = AdPanel.None;
        if (top.IsChecked == true)
        {
            panels |= AdPanel.Top;
        }

        if (middle.IsChecked == true)
        {
            panels |= AdPanel.Middle;
        }

        if (bottom.IsChecked == true)
        {
            panels |= AdPanel.Bottom;
        }

        if (panels == AdPanel.None)
        {
            MessageBox.Show(Lang(_uiLanguage, "Select at least one panel for this image.", "Wybierz co najmniej jeden panel dla tej grafiki.", "Wähle mindestens ein Panel für dieses Bild.", "Выберите как минимум одну панель для этого изображения.", "Selecciona al menos un panel para esta imagen."), Lang(_uiLanguage, "Error", "Błąd", "Fehler", "Ошибка", "Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        selected.DurationSeconds = duration;
        selected.Panels = panels;

        SaveAdsManifest();
        RotateAds();
    }

    private void SaveGlobalAdSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        var rotationBox = GetRotationIntervalTextBox();
        var defaultDurationBox = GetDefaultAdDurationTextBox();

        ParseSeconds(defaultDurationBox?.Text, _defaultAdDurationSeconds, out _defaultAdDurationSeconds);
        ParseSeconds(rotationBox?.Text, _rotationIntervalSeconds, out _rotationIntervalSeconds);

        var orderModeComboBox = GetOrderModeComboBox();
        _adOrderMode = orderModeComboBox?.SelectedIndex == 1 ? AdOrderMode.Random : AdOrderMode.Sequential;

        _rotationIntervalSeconds = Math.Clamp(_rotationIntervalSeconds, 1, 30);
        _adTimer.Interval = TimeSpan.FromSeconds(_rotationIntervalSeconds);
        if (rotationBox is not null)
        {
            rotationBox.Text = _rotationIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        }

        if (defaultDurationBox is not null)
        {
            defaultDurationBox.Text = _defaultAdDurationSeconds.ToString(CultureInfo.InvariantCulture);
        }

        SaveAdsManifest();
        RotateAds();
    }

    private Border? GetAdManagerPanel() => FindName("AdManagerPanel") as Border;
    private StackPanel? GetAdManagerAuthPanel() => FindName("AdManagerAuthPanel") as StackPanel;
    private StackPanel? GetAdManagerControls() => FindName("AdManagerControls") as StackPanel;
    private PasswordBox? GetAdManagerPasswordBox() => FindName("AdManagerPasswordBox") as PasswordBox;
    private TextBlock? GetAdManagerHintText() => FindName("AdManagerHintText") as TextBlock;

    private TextBox? GetSelectedAdDurationTextBox() => FindName("SelectedAdDurationTextBox") as TextBox;
    private CheckBox? GetSelectedAdTopCheckBox() => FindName("SelectedAdTopCheckBox") as CheckBox;
    private CheckBox? GetSelectedAdMiddleCheckBox() => FindName("SelectedAdMiddleCheckBox") as CheckBox;
    private CheckBox? GetSelectedAdBottomCheckBox() => FindName("SelectedAdBottomCheckBox") as CheckBox;
    private TextBox? GetRotationIntervalTextBox() => FindName("RotationIntervalTextBox") as TextBox;
    private TextBox? GetDefaultAdDurationTextBox() => FindName("DefaultAdDurationTextBox") as TextBox;
    private ComboBox? GetOrderModeComboBox() => FindName("OrderModeComboBox") as ComboBox;

    private enum StartMenuSection
    {
        NewGame,
        Highscore,
        Settings
    }

    private void ShowStartMenuSection(StartMenuSection section)
    {
        NewGamePanel.Visibility = section == StartMenuSection.NewGame ? Visibility.Visible : Visibility.Collapsed;
        HighscorePanel.Visibility = section == StartMenuSection.Highscore ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = section == StartMenuSection.Settings ? Visibility.Visible : Visibility.Collapsed;

        MenuNewGameButton.Background = section == StartMenuSection.NewGame ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(29, 78, 216));
        MenuHighscoreButton.Background = section == StartMenuSection.Highscore ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(29, 78, 216));
        MenuSettingsButton.Background = section == StartMenuSection.Settings ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(29, 78, 216));
    }

    private void MenuNewGameButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        ShowStartMenuSection(StartMenuSection.NewGame);
    }

    private void MenuHighscoreButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        HighscoreManageHintText.Text = string.Empty;
        HighscoreAuthStatusText.Text = _isHighscoreUnlocked ? Lang(_uiLanguage, "Edit mode active.", "Tryb edycji aktywny.", "Bearbeitungsmodus aktiv.", "Режим редактирования активен.", "Modo edición activo.") : Lang(_uiLanguage, "Read-only mode. Confirm password to edit.", "Tryb tylko podglądu. Potwierdź hasło, aby edytować.", "Nur-Lese-Modus. Passwort bestätigen, um zu bearbeiten.", "Режим только чтения. Подтвердите пароль для редактирования.", "Modo solo lectura. Confirma contraseña para editar.");
        ShowStartMenuSection(StartMenuSection.Highscore);
    }

    private void MenuSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        ShowStartMenuSection(StartMenuSection.Settings);
    }

    private void HighscoreModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedHighscoreMode = (GameMode)Math.Clamp(HighscoreModeComboBox.SelectedIndex, 0, 4);
        RefreshHighScores();
    }

    private void GameModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HighscoreModeComboBox is null)
        {
            return;
        }

        HighscoreModeComboBox.SelectedIndex = Math.Clamp(GameModeComboBox.SelectedIndex, 0, 4);
    }

    private bool EnsureHighscoreUnlocked()
    {
        if (_isHighscoreUnlocked)
        {
            return true;
        }

        HighscoreManageHintText.Text = Lang(_uiLanguage, "Confirm password first to manage records.", "Najpierw zatwierdź hasło, aby zarządzać rekordami.", "Bestätige zuerst das Passwort, um Rekorde zu verwalten.", "Сначала подтвердите пароль для управления рекордами.", "Confirma la contraseña primero para gestionar récords.");
        return false;
    }

    private void UnlockHighscoreActionsButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        if (HighscorePasswordBox.Password != _adminPassword)
        {
            _isHighscoreUnlocked = false;
            HighscoreAuthStatusText.Text = Lang(_uiLanguage, "❌ Wrong password", "❌ Błędne hasło", "❌ Falsches Passwort", "❌ Неверный пароль", "❌ Contraseña incorrecta");
            HighscoreAuthStatusText.Foreground = new SolidColorBrush(Color.FromRgb(252, 165, 165));
            HighscoreManageHintText.Text = Lang(_uiLanguage, "Incorrect password. Editing locked.", "Niepoprawne hasło. Edycja zablokowana.", "Falsches Passwort. Bearbeitung gesperrt.", "Неверный пароль. Редактирование заблокировано.", "Contraseña incorrecta. Edición bloqueada.");
            RefreshHighScores();
            return;
        }

        _isHighscoreUnlocked = true;
        HighscorePasswordBox.Password = string.Empty;
        HighscoreAuthStatusText.Text = Lang(_uiLanguage, "✅ Editing unlocked: you can edit nicknames or delete records.", "✅ Edycja odblokowana: możesz zmieniać nicki lub usuwać rekordy.", "✅ Bearbeitung entsperrt: Du kannst Namen ändern oder löschen.", "✅ Редактирование разблокировано: можно менять ники и удалять.", "✅ Edición desbloqueada: puedes editar nicks o borrar registros.");
        HighscoreAuthStatusText.Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253));
        HighscoreManageHintText.Text = Lang(_uiLanguage, "Edit mode active.", "Tryb edycji aktywny.", "Bearbeitungsmodus aktiv.", "Режим редактирования активен.", "Modo edición activo.");
        RefreshHighScores();
    }

    private void HighscoreDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        if (!EnsureHighscoreUnlocked())
        {
            return;
        }

        var modeScores = GetHighScoresForMode(_selectedHighscoreMode);
        if (sender is not Button { Tag: int index } || index < 0 || index >= modeScores.Count)
        {
            HighscoreManageHintText.Text = Lang(_uiLanguage, "Failed to delete record.", "Nie udało się usunąć rekordu.", "Eintrag konnte nicht gelöscht werden.", "Не удалось удалить запись.", "No se pudo eliminar el registro.");
            return;
        }

        modeScores.RemoveAt(index);
        RefreshHighScores();
        SaveHighScores();
        UpdateHud();
        HighscoreManageHintText.Text = Lang(_uiLanguage, "Record deleted.", "Rekord usunięty.", "Eintrag gelöscht.", "Запись удалена.", "Registro eliminado.");
    }

    private void HighscoreEditNameButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        if (!EnsureHighscoreUnlocked())
        {
            return;
        }

        var modeScores = GetHighScoresForMode(_selectedHighscoreMode);
        if (sender is not Button { Tag: int index } || index < 0 || index >= modeScores.Count)
        {
            HighscoreManageHintText.Text = Lang(_uiLanguage, "Failed to edit record.", "Nie udało się edytować rekordu.", "Eintrag konnte nicht bearbeitet werden.", "Не удалось отредактировать запись.", "No se pudo editar el registro.");
            return;
        }

        var rowBorder = StartHighScoresListBox.Items[index] as Border;
        var editor = rowBorder?.Tag as TextBox;
        if (editor is null)
        {
            HighscoreManageHintText.Text = Lang(_uiLanguage, "Missing edit field for this entry.", "Brak pola edycji dla tego wpisu.", "Bearbeitungsfeld für diesen Eintrag fehlt.", "Нет поля редактирования для этой записи.", "Falta el campo de edición para esta entrada.");
            return;
        }

        editor.Visibility = Visibility.Visible;
        editor.Focus();
        editor.SelectAll();
        HighscoreManageHintText.Text = Lang(_uiLanguage, "Type a new nickname and click ✔.", "Wpisz nowy nick i kliknij ✔.", "Gib einen neuen Namen ein und klicke ✔.", "Введите новый ник и нажмите ✔.", "Escribe un nuevo apodo y pulsa ✔.");
    }

    private void HighscoreApplyNameButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        if (!EnsureHighscoreUnlocked())
        {
            return;
        }

        var modeScores = GetHighScoresForMode(_selectedHighscoreMode);
        if (sender is not Button { Tag: int index } || index < 0 || index >= modeScores.Count)
        {
            HighscoreManageHintText.Text = Lang(_uiLanguage, "Failed to save nickname.", "Nie udało się zapisać nicku.", "Nickname konnte nicht gespeichert werden.", "Не удалось сохранить ник.", "No se pudo guardar el apodo.");
            return;
        }

        var rowBorder = StartHighScoresListBox.Items[index] as Border;
        var editor = rowBorder?.Tag as TextBox;
        if (editor is null)
        {
            HighscoreManageHintText.Text = Lang(_uiLanguage, "Missing edit field for this entry.", "Brak pola edycji dla tego wpisu.", "Bearbeitungsfeld für diesen Eintrag fehlt.", "Нет поля редактирования для этой записи.", "Falta el campo de edición para esta entrada.");
            return;
        }

        var newName = editor.Text.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            HighscoreManageHintText.Text = Lang(_uiLanguage, "Nickname cannot be empty.", "Nick nie może być pusty.", "Nickname darf nicht leer sein.", "Ник не может быть пустым.", "El apodo no puede estar vacío.");
            return;
        }

        var current = modeScores[index];
        modeScores[index] = current with { Name = newName };
        SaveHighScores();
        RefreshHighScores();
        UpdateHud();
        HighscoreManageHintText.Text = Lang(_uiLanguage, "Nickname updated.", "Nick zaktualizowany.", "Nickname aktualisiert.", "Ник обновлён.", "Apodo actualizado.");
    }

    private void ResetAdManagerUi()
    {
        var panel = GetAdManagerPanel();
        var authPanel = GetAdManagerAuthPanel();
        var controls = GetAdManagerControls();
        var passwordBox = GetAdManagerPasswordBox();
        var hintText = GetAdManagerHintText();

        if (panel is null || authPanel is null || controls is null || passwordBox is null || hintText is null)
        {
            return;
        }

        panel.Visibility = Visibility.Collapsed;
        authPanel.Visibility = Visibility.Visible;
        controls.Visibility = Visibility.Collapsed;
        passwordBox.Password = string.Empty;
        hintText.Text = string.Empty;
    }

    private void OpenAdManagerButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        var panel = GetAdManagerPanel();
        var authPanel = GetAdManagerAuthPanel();
        var controls = GetAdManagerControls();
        var passwordBox = GetAdManagerPasswordBox();
        var hintText = GetAdManagerHintText();

        if (panel is null || authPanel is null || controls is null || passwordBox is null || hintText is null)
        {
            return;
        }

        panel.Visibility = Visibility.Visible;
        authPanel.Visibility = Visibility.Visible;
        controls.Visibility = Visibility.Collapsed;
        passwordBox.Password = string.Empty;
        hintText.Text = string.Empty;
        passwordBox.Focus();
    }

    private void UnlockAdManagerButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        var authPanel = GetAdManagerAuthPanel();
        var controls = GetAdManagerControls();
        var passwordBox = GetAdManagerPasswordBox();
        var hintText = GetAdManagerHintText();

        if (authPanel is null || controls is null || passwordBox is null || hintText is null)
        {
            return;
        }

        if (passwordBox.Password != _adminPassword)
        {
            hintText.Text = Lang(_uiLanguage, "Wrong password", "Błędne hasło", "Falsches Passwort", "Неверный пароль", "Contraseña incorrecta");
            return;
        }

        hintText.Text = string.Empty;
        authPanel.Visibility = Visibility.Collapsed;
        controls.Visibility = Visibility.Visible;
    }

    private void CloseAdManagerButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        ResetAdManagerUi();
    }

    private static GameSettings CreateDefaultSettings()
    {
        return new GameSettings(
            "Player",
            0,
            0,
            0,
            0.6,
            0.8,
            140,
            45,
            35,
            "Left",
            "Right",
            "Down",
            "Up",
            "Space",
            "C",
            false,
            true,
            true,
            true,
            true,
            DefaultAdminPassword,
            "en");
    }

    private static UiLanguage ParseLanguageCode(string? code) => code?.ToLowerInvariant() switch
    {
        "pl" => UiLanguage.Polish,
        "de" => UiLanguage.German,
        "ru" => UiLanguage.Russian,
        "es" => UiLanguage.Spanish,
        _ => UiLanguage.English
    };

    private static string ToLanguageCode(UiLanguage language) => language switch
    {
        UiLanguage.Polish => "pl",
        UiLanguage.German => "de",
        UiLanguage.Russian => "ru",
        UiLanguage.Spanish => "es",
        _ => "en"
    };

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        ApplyTheme();
        SaveSettings();
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _uiLanguage = (UiLanguage)Math.Clamp(LanguageComboBox.SelectedIndex, 0, 4);
        ApplyLanguageToUi();
        SaveSettings();
    }

    private void ColorblindModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        ApplyTheme();
        SaveSettings();
    }
    private void ApplyAudioSettingsUi()
    {
        MusicVolumeSlider.IsEnabled = _musicEnabled;
        EffectsVolumeSlider.IsEnabled = _effectsEnabled;
        MusicVolumeSlider.Opacity = _musicEnabled ? 1 : 0.45;
        EffectsVolumeSlider.Opacity = _effectsEnabled ? 1 : 0.45;
    }

    private void SettingsToggleCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _showSessionStats = ShowSessionStatsCheckBox.IsChecked == true;
        _musicEnabled = MusicEnabledCheckBox.IsChecked == true;
        _effectsEnabled = EffectsEnabledCheckBox.IsChecked == true;

        SessionStatsCard.Visibility = _showSessionStats ? Visibility.Visible : Visibility.Collapsed;
        ApplyAudioSettingsUi();

        if (!_musicEnabled)
        {
            StopBackgroundMusic();
        }
        else if (_isGameStarted && !_gameOver && !_isPaused && StartMenuOverlay.Visibility != Visibility.Visible)
        {
            PlayBackgroundMusic();
        }

        SaveSettings();
    }

    private void SaveAdminPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");

        var newPassword = AdminPasswordSettingsBox.Password.Trim();
        if (newPassword.Length < 4)
        {
            AdminPasswordStatusText.Text = Lang(_uiLanguage, "Password must be at least 4 characters long.", "Hasło musi mieć co najmniej 4 znaki.", "Passwort muss mindestens 4 Zeichen haben.", "Пароль должен быть не короче 4 символов.", "La contraseña debe tener al menos 4 caracteres.");
            AdminPasswordStatusText.Foreground = new SolidColorBrush(Color.FromRgb(252, 165, 165));
            return;
        }

        _adminPassword = newPassword;
        AdminPasswordSettingsBox.Password = string.Empty;
        AdminPasswordStatusText.Text = Lang(_uiLanguage, "✅ Administrator password updated.", "✅ Hasło administratora zaktualizowane.", "✅ Administratorpasswort aktualisiert.", "✅ Пароль администратора обновлён.", "✅ Contraseña de administrador actualizada.");
        AdminPasswordStatusText.Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253));
        SaveSettings();
    }

    private void LoadSessionHistory()
    {
        _sessionHistory.Clear();
        if (!File.Exists(_sessionHistoryPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_sessionHistoryPath);
            var data = JsonSerializer.Deserialize<List<SessionHistoryEntry>>(json) ?? [];
            _sessionHistory.AddRange(data.OrderBy(e => e.PlayedAtUtc).TakeLast(200));
        }
        catch
        {
            _sessionHistory.Clear();
        }
    }

    private void SaveSessionHistory()
    {
        var json = JsonSerializer.Serialize(_sessionHistory.TakeLast(200), JsonWriteOptions);
        File.WriteAllText(_sessionHistoryPath, json);
    }

    private void CaptureSessionResult()
    {
        var seconds = Math.Max(1d, _sessionStopwatch.Elapsed.TotalSeconds);
        var entry = new SessionHistoryEntry(
            DateTime.UtcNow,
            _activeGameMode.ToString(),
            _score,
            _playerActions * 60d / seconds,
            _piecesLocked / seconds,
            _lockSamples == 0 ? 0 : (double)_totalLockDelayMs / _lockSamples,
            _linesCleared);

        _sessionHistory.Add(entry);
        if (_sessionHistory.Count > 200)
        {
            _sessionHistory.RemoveRange(0, _sessionHistory.Count - 200);
        }

        SaveSessionHistory();
    }

    private void DrawTrendChart()
    {
        if (TrendChartCanvas is null)
        {
            return;
        }

        TrendChartCanvas.Children.Clear();
        var points = _sessionHistory.TakeLast(20).ToList();
        if (points.Count < 2)
        {
            return;
        }

        var width = TrendChartCanvas.Width;
        var height = TrendChartCanvas.Height;
        var pad = 6d;

        double maxScore = Math.Max(1, points.Max(p => p.Score));
        double maxPps = Math.Max(0.01, points.Max(p => p.Pps));
        double maxApm = Math.Max(1, points.Max(p => p.Apm));

        var scoreLine = new Polyline { Stroke = Brushes.Gold, StrokeThickness = 2, Opacity = 0.9 };
        var ppsLine = new Polyline { Stroke = Brushes.LightSkyBlue, StrokeThickness = 1.6, Opacity = 0.85 };
        var apmLine = new Polyline { Stroke = Brushes.LightGreen, StrokeThickness = 1.6, Opacity = 0.85 };

        for (var i = 0; i < points.Count; i++)
        {
            var x = pad + (width - 2 * pad) * i / Math.Max(1, points.Count - 1);
            scoreLine.Points.Add(new Point(x, pad + (height - 2 * pad) * (1 - points[i].Score / maxScore)));
            ppsLine.Points.Add(new Point(x, pad + (height - 2 * pad) * (1 - points[i].Pps / maxPps)));
            apmLine.Points.Add(new Point(x, pad + (height - 2 * pad) * (1 - points[i].Apm / maxApm)));
        }

        TrendChartCanvas.Children.Add(scoreLine);
        TrendChartCanvas.Children.Add(ppsLine);
        TrendChartCanvas.Children.Add(apmLine);
    }

    private void ResetStatsButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");

        _sessionHistory.Clear();
        SaveSessionHistory();

        _playerActions = 0;
        _piecesLocked = 0;
        _totalLockDelayMs = 0;
        _lockSamples = 0;
        _quadLineClears = 0;

        DrawTrendChart();
        UpdateHud();
        ExportStatusText.Text = Lang(_uiLanguage, "✅ Stats reset (history + chart + session counters).", "✅ Statystyki zresetowane (historia + wykres + liczniki sesji).", "✅ Statistiken zurückgesetzt (Verlauf + Diagramm + Zähler).", "✅ Статистика сброшена (история + график + счётчики).", "✅ Estadísticas reiniciadas (historial + gráfico + contadores).");
    }

    private void KeyBindingTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectAll();
            ExportStatusText.Text = Lang(_uiLanguage, "Press a key to bind this control.", "Naciśnij klawisz, aby przypisać sterowanie.", "Drücke eine Taste, um die Steuerung zuzuweisen.", "Нажмите клавишу для назначения управления.", "Pulsa una tecla para asignar este control.");
        }
    }

    private void KeyBindingTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab)
        {
            return;
        }

        textBox.Text = key.ToString();
        SaveSettings();
        ExportStatusText.Text = $"{Lang(_uiLanguage, "✅ Assigned key", "✅ Przypisano klawisz", "✅ Taste zugewiesen", "✅ Клавиша назначена", "✅ Tecla asignada")}: {key}";
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void ExportStatsCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var save = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"stackmaster-session-stats-{DateTime.Now:yyyyMMdd-HHmm}.csv"
        };

        if (save.ShowDialog() != true)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("PlayedAtUtc,Mode,Score,Apm,Pps,AvgLockDelayMs,LinesCleared");
        foreach (var e1 in _sessionHistory)
        {
            sb.AppendLine($"{e1.PlayedAtUtc:O},{e1.Mode},{e1.Score},{e1.Apm:0.##},{e1.Pps:0.###},{e1.AvgLockDelayMs:0.##},{e1.LinesCleared}");
        }

        File.WriteAllText(save.FileName, sb.ToString());
        ExportStatusText.Text = $"{Lang(_uiLanguage, "✅ Saved CSV", "✅ Zapisano CSV", "✅ CSV gespeichert", "✅ CSV сохранён", "✅ CSV guardado")}: {save.FileName}";
    }

    private void ExportStatsJsonButton_Click(object sender, RoutedEventArgs e)
    {
        var save = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = $"stackmaster-session-stats-{DateTime.Now:yyyyMMdd-HHmm}.json"
        };

        if (save.ShowDialog() != true)
        {
            return;
        }

        var json = JsonSerializer.Serialize(_sessionHistory, JsonWriteOptions);
        File.WriteAllText(save.FileName, json);
        ExportStatusText.Text = $"{Lang(_uiLanguage, "✅ Saved JSON", "✅ Zapisano JSON", "✅ JSON gespeichert", "✅ JSON сохранён", "✅ JSON guardado")}: {save.FileName}";
    }

    private void ExportHighscoresCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var save = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"stackmaster-highscores-{DateTime.Now:yyyyMMdd-HHmm}.csv"
        };

        if (save.ShowDialog() != true)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Mode,Rank,Name,Points");
        foreach (var mode in Enum.GetValues<GameMode>())
        {
            var rows = GetHighScoresForMode(mode);
            for (var i = 0; i < rows.Count; i++)
            {
                sb.AppendLine($"{mode},{i + 1},{rows[i].Name},{rows[i].Points}");
            }
        }

        File.WriteAllText(save.FileName, sb.ToString());
        ExportStatusText.Text = $"{Lang(_uiLanguage, "✅ Saved highscores CSV", "✅ Zapisano rekordy CSV", "✅ Highscores-CSV gespeichert", "✅ CSV рекордов сохранён", "✅ CSV de highscores guardado")}: {save.FileName}";
    }

    private void LoadOnboardingState()
    {
        if (!File.Exists(_onboardingPath))
        {
            _onboardingState = new OnboardingState(false, string.Empty);
            return;
        }

        try
        {
            var json = File.ReadAllText(_onboardingPath);
            _onboardingState = JsonSerializer.Deserialize<OnboardingState>(json) ?? new OnboardingState(false, string.Empty);
        }
        catch
        {
            _onboardingState = new OnboardingState(false, string.Empty);
        }
    }

    private void SaveOnboardingState()
    {
        var json = JsonSerializer.Serialize(_onboardingState, JsonWriteOptions);
        File.WriteAllText(_onboardingPath, json);
    }

    private void ShowOnboardingIfNeeded()
    {
        if (!_onboardingState.TutorialCompleted)
        {
            _tutorialStepIndex = 0;
            TutorialStepText.Text = GetTutorialSteps()[_tutorialStepIndex];
            TutorialNextButton.Content = Lang(_uiLanguage, "Next", "Dalej", "Weiter", "Далее", "Siguiente");
            TutorialOverlay.Visibility = Visibility.Visible;
            return;
        }

        if (!string.Equals(_onboardingState.LastSeenWhatsNewVersion, CurrentWhatsNewVersion, StringComparison.Ordinal))
        {
            WhatsNewText.Text = GetWhatsNewMessage();
            WhatsNewOverlay.Visibility = Visibility.Visible;
        }
    }

    private void TutorialNextButton_Click(object sender, RoutedEventArgs e)
    {
        _tutorialStepIndex++;
        if (_tutorialStepIndex >= GetTutorialSteps().Length)
        {
            TutorialOverlay.Visibility = Visibility.Collapsed;
            _onboardingState = _onboardingState with { TutorialCompleted = true };
            SaveOnboardingState();
            ShowOnboardingIfNeeded();
            return;
        }

        TutorialStepText.Text = GetTutorialSteps()[_tutorialStepIndex];
        TutorialNextButton.Content = _tutorialStepIndex == GetTutorialSteps().Length - 1 ? Lang(_uiLanguage, "Finish", "Zakończ", "Fertig", "Завершить", "Finalizar") : Lang(_uiLanguage, "Next", "Dalej", "Weiter", "Далее", "Siguiente");
    }

    private void SkipTutorialButton_Click(object sender, RoutedEventArgs e)
    {
        TutorialOverlay.Visibility = Visibility.Collapsed;
        _onboardingState = _onboardingState with { TutorialCompleted = true };
        SaveOnboardingState();
        ShowOnboardingIfNeeded();
    }

    private void CloseWhatsNewButton_Click(object sender, RoutedEventArgs e)
    {
        WhatsNewOverlay.Visibility = Visibility.Collapsed;
        _onboardingState = _onboardingState with { LastSeenWhatsNewVersion = CurrentWhatsNewVersion };
        SaveOnboardingState();
    }

    private void OpenWhatsNewButton_Click(object sender, RoutedEventArgs e)
    {
        WhatsNewText.Text = GetWhatsNewMessage();
        WhatsNewOverlay.Visibility = Visibility.Visible;
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        ResetAdManagerUi();
        ApplyTheme();
        SaveSettings();
        StartMenuOverlay.Visibility = Visibility.Collapsed;
        StartNewGame();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        PlayEffect("buttonClick");
        SaveSettings();
        Close();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateBoardLayout();
        Draw();
    }

    private record ScoreEntry(string Name, int Points);
    private record SessionHistoryEntry(DateTime PlayedAtUtc, string Mode, int Score, double Apm, double Pps, double AvgLockDelayMs, int LinesCleared);
    private record OnboardingState(bool TutorialCompleted, string LastSeenWhatsNewVersion);
    private enum GameMode
    {
        Classic = 0,
        Survival = 1,
        Sprint = 2,
        Ultra = 3,
        Marathon = 4
    }

    private enum AdOrderMode
    {
        Sequential = 0,
        Random = 1
    }

    [Flags]
    private enum AdPanel
    {
        None = 0,
        Top = 1,
        Middle = 2,
        Bottom = 4
    }

    private sealed class AdPlaybackState
    {
        public int LastIndex { get; set; } = -1;
        public int ElapsedSeconds { get; set; }
        public AdEntry? CurrentAd { get; set; }
    }

    private sealed class AdEntry(string fileName, string displayName, int durationSeconds, AdPanel panels)
    {
        public string FileName { get; } = fileName;
        public string DisplayName { get; } = displayName;
        public int DurationSeconds { get; set; } = durationSeconds;
        public AdPanel Panels { get; set; } = panels;

        public bool IsVisibleOn(AdPanel panel) => (Panels & panel) != 0;
    }

    private record AdManifestItem(string FileName, string DisplayName, int DurationSeconds, AdPanel Panels);
    private record AdManifestRoot(int DefaultAdDurationSeconds, int RotationIntervalSeconds, AdOrderMode OrderMode, List<AdManifestItem> Ads);
}
