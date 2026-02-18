using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Tetris;

public partial class MainWindow : Window
{
    private const int BoardWidth = 10;
    private const int BoardHeight = 20;

    private readonly Brush?[,] _board = new Brush?[BoardHeight, BoardWidth];
    private readonly Random _random = new();
    private readonly DispatcherTimer _timer;
    private readonly ObservableCollection<string> _highScoreRows =
    [
        "1. --- 0",
        "2. --- 0",
        "3. --- 0",
        "4. --- 0",
        "5. --- 0"
    ];

    private readonly List<ScoreEntry> _highScores = [];
    private readonly ObservableCollection<AdEntry> _ads = [];
    private readonly DispatcherTimer _adTimer;

    private int _adDisplayCursor;
    private readonly string _adStorageFolder;
    private readonly string _adManifestPath;

    private Tetromino _currentPiece = null!;
    private Tetromino _nextPiece = null!;
    private int _currentX;
    private int _currentY;
    private int _score;
    private int _linesCleared;
    private bool _gameOver;
    private bool _isGameStarted;
    private int _startLevel;
    private double _cellSize = 36;

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

    private Brush[] _activePaletteBrushes = [];

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
        _adTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _adTimer.Tick += (_, _) => RotateAds();

        _adStorageFolder = ResolveAdStoragePath();
        _adManifestPath = Path.Combine(_adStorageFolder, "ads.json");

        HighScoresListBox.ItemsSource = _highScoreRows;
        AdListBox.ItemsSource = _ads;

        EnsureAdStorage();
        LoadAds();
        ApplyTheme();
        UpdateBoardLayout();
        Draw();
        RotateAds();
        _adTimer.Start();
    }

    private void ApplyTheme()
    {
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

        Draw();
    }

    private void SetCardTheme(Color bg, Color border, Color titleColor)
    {
        var bgBrush = new SolidColorBrush(bg);
        var borderBrush = new SolidColorBrush(border);
        var titleBrush = new SolidColorBrush(titleColor);

        foreach (var card in new[] { NickCard, ScoreCard, LevelCard, NextCard, HighScoreCard, StatusCard })
        {
            card.Background = bgBrush;
            card.BorderBrush = borderBrush;
        }

        TitleText.Foreground = titleBrush;
        HighScoresListBox.BorderBrush = borderBrush;
        HighScoresListBox.Background = new SolidColorBrush(Color.FromArgb(90, bg.R, bg.G, bg.B));
    }

    private void StartNewGame()
    {
        Array.Clear(_board);
        _score = 0;
        _linesCleared = 0;
        _gameOver = false;
        _isGameStarted = true;
        GameOverOverlay.Visibility = Visibility.Collapsed;

        _startLevel = StartLevelComboBox.SelectedIndex switch
        {
            1 => 4,
            2 => 8,
            _ => 1
        };

        var nick = NickTextBox.Text.Trim();
        PlayerNameText.Text = string.IsNullOrWhiteSpace(nick) ? "Gracz" : nick;

        SetTimerSpeed();
        _nextPiece = CreateRandomPiece();
        SpawnPiece();
        UpdateHud();
        Draw();
        _timer.Start();
    }

    private void SetTimerSpeed()
    {
        var level = _startLevel + (_linesCleared / 8);
        var speedMs = Math.Max(30, 620 - (level - 1) * 85);
        _timer.Interval = TimeSpan.FromMilliseconds(speedMs);
    }

    private void Tick()
    {
        if (_gameOver || !_isGameStarted)
        {
            return;
        }

        if (!TryMove(_currentX, _currentY + 1, _currentPiece.Cells))
        {
            LockPiece();
            var clearedRows = ClearFullLines();
            if (clearedRows.Count > 0)
            {
                AnimateClearedLines(clearedRows);
            }

            SpawnPiece();
        }

        Draw();
    }

    private void SpawnPiece()
    {
        _currentPiece = _nextPiece;
        _nextPiece = CreateRandomPiece();
        _currentX = BoardWidth / 2 - 2;
        _currentY = 0;

        if (!IsPositionValid(_currentX, _currentY, _currentPiece.Cells))
        {
            OnGameOver();
        }

        DrawNextPiece();
    }

    private void OnGameOver()
    {
        _gameOver = true;
        _timer.Stop();
        RegisterScore();
        GameOverOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "PRZEGRAŁEŚ • Spacja: menu start • Esc: zamknij";
    }

    private void RegisterScore()
    {
        var nick = string.IsNullOrWhiteSpace(PlayerNameText.Text) ? "Gracz" : PlayerNameText.Text;
        _highScores.Add(new ScoreEntry(nick, _score));
        _highScores.Sort((a, b) => b.Points.CompareTo(a.Points));
        if (_highScores.Count > 5)
        {
            _highScores.RemoveRange(5, _highScores.Count - 5);
        }

        RefreshHighScores();
    }

    private void RefreshHighScores()
    {
        _highScoreRows.Clear();
        for (var i = 0; i < 5; i++)
        {
            if (i < _highScores.Count)
            {
                var entry = _highScores[i];
                _highScoreRows.Add($"{i + 1}. {entry.Name} - {entry.Points}");
            }
            else
            {
                _highScoreRows.Add($"{i + 1}. --- 0");
            }
        }
    }

    private Tetromino CreateRandomPiece()
    {
        var index = _random.Next(PieceDefinitions.Length);
        var shape = PieceDefinitions[index].Select(p => new Point(p.X, p.Y)).ToArray();
        return new Tetromino(shape, _activePaletteBrushes[index], index == 3);
    }

    private bool TryMove(int newX, int newY, Point[] cells)
    {
        if (!IsPositionValid(newX, newY, cells))
        {
            return false;
        }

        _currentX = newX;
        _currentY = newY;
        return true;
    }

    private bool IsPositionValid(int x, int y, Point[] cells)
    {
        foreach (var cell in cells)
        {
            var boardX = x + (int)cell.X;
            var boardY = y + (int)cell.Y;

            if (boardX < 0 || boardX >= BoardWidth || boardY < 0 || boardY >= BoardHeight)
            {
                return false;
            }

            if (_board[boardY, boardX] is not null)
            {
                return false;
            }
        }

        return true;
    }

    private void RotateCurrentPiece()
    {
        if (_currentPiece.IsSquare)
        {
            return;
        }

        var rotated = _currentPiece.Cells.Select(p => new Point(2 - p.Y, p.X)).ToArray();
        int[] offsets = [0, -1, 1, -2, 2];
        foreach (var offset in offsets)
        {
            if (!IsPositionValid(_currentX + offset, _currentY, rotated))
            {
                continue;
            }

            _currentX += offset;
            _currentPiece = _currentPiece with { Cells = rotated };
            return;
        }
    }

    private void HardDrop()
    {
        while (TryMove(_currentX, _currentY + 1, _currentPiece.Cells))
        {
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
        foreach (var cell in _currentPiece.Cells)
        {
            var boardX = _currentX + (int)cell.X;
            var boardY = _currentY + (int)cell.Y;
            if (boardY >= 0 && boardY < BoardHeight && boardX >= 0 && boardX < BoardWidth)
            {
                _board[boardY, boardX] = _currentPiece.Color;
            }
        }
    }

    private List<int> ClearFullLines()
    {
        var removedRows = new List<int>();

        for (var y = BoardHeight - 1; y >= 0; y--)
        {
            var isFull = true;
            for (var x = 0; x < BoardWidth; x++)
            {
                if (_board[y, x] is null)
                {
                    isFull = false;
                    break;
                }
            }

            if (!isFull)
            {
                continue;
            }

            removedRows.Add(y);
            for (var pullY = y; pullY > 0; pullY--)
            {
                for (var x = 0; x < BoardWidth; x++)
                {
                    _board[pullY, x] = _board[pullY - 1, x];
                }
            }

            for (var x = 0; x < BoardWidth; x++)
            {
                _board[0, x] = null;
            }

            y++;
        }

        if (removedRows.Count == 0)
        {
            return removedRows;
        }

        _linesCleared += removedRows.Count;
        _score += removedRows.Count switch
        {
            1 => 120,
            2 => 360,
            3 => 700,
            4 => 1100,
            _ => removedRows.Count * 250
        };

        SetTimerSpeed();
        UpdateHud();
        AnimateScorePulse();
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
        var level = _startLevel + (_linesCleared / 8);
        ScoreText.Text = _score.ToString();
        LevelText.Text = level.ToString();

        if (!_gameOver)
        {
            StatusText.Text = "Tryb szybki mocno zwiększa tempo • Strzałki/Spacja • Esc";
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
            foreach (var cell in _currentPiece.Cells)
            {
                DrawCell(_currentX + (int)cell.X, _currentY + (int)cell.Y, _currentPiece.Color, 1);
            }
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
    }

    private void DrawNextPiece()
    {
        NextPieceCanvas.Children.Clear();
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
                _isGameStarted = false;
            }

            return;
        }

        switch (e.Key)
        {
            case Key.Left:
                TryMove(_currentX - 1, _currentY, _currentPiece.Cells);
                break;
            case Key.Right:
                TryMove(_currentX + 1, _currentY, _currentPiece.Cells);
                break;
            case Key.Down:
                TryMove(_currentX, _currentY + 1, _currentPiece.Cells);
                break;
            case Key.Up:
                RotateCurrentPiece();
                break;
            case Key.Space:
                HardDrop();
                return;
        }

        Draw();
    }

    private static string ResolveAdStoragePath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var projectFile = Path.Combine(current.FullName, "Tetris.csproj");
            if (File.Exists(projectFile))
            {
                return Path.Combine(current.FullName, "AdAssets");
            }

            current = current.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "AdAssets");
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
            var data = JsonSerializer.Deserialize<List<AdManifestItem>>(json) ?? [];
            foreach (var item in data)
            {
                var fullPath = Path.Combine(_adStorageFolder, item.FileName);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                _ads.Add(new AdEntry(item.FileName, item.DisplayName));
            }
        }
        catch
        {
            _ads.Clear();
        }
    }

    private void SaveAdsManifest()
    {
        var data = _ads.Select(a => new AdManifestItem(a.FileName, a.DisplayName)).ToList();
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_adManifestPath, json);
    }

    private void RotateAds()
    {
        if (_ads.Count == 0)
        {
            ShowNoAds();
            return;
        }

        if (_adDisplayCursor >= _ads.Count)
        {
            _adDisplayCursor = 0;
        }

        var topAd = _ads[_adDisplayCursor];
        var bottomAd = _ads[(_adDisplayCursor + 1) % _ads.Count];

        ShowAd(AdImageTop, AdPlaceholderTop, topAd);
        ShowAd(AdImageBottom, AdPlaceholderBottom, bottomAd);

        _adDisplayCursor = (_adDisplayCursor + 2) % _ads.Count;
    }

    private void ShowNoAds()
    {
        AdImageTop.Source = null;
        AdImageBottom.Source = null;
        AdImageTop.Opacity = 0;
        AdImageBottom.Opacity = 0;
        AdPlaceholderTop.Visibility = Visibility.Visible;
        AdPlaceholderBottom.Visibility = Visibility.Visible;
    }

    private void ShowAd(Image target, TextBlock placeholder, AdEntry ad)
    {
        var filePath = Path.Combine(_adStorageFolder, ad.FileName);
        if (!File.Exists(filePath))
        {
            placeholder.Visibility = Visibility.Visible;
            target.Source = null;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
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
        var dialog = new OpenFileDialog
        {
            Title = "Wybierz reklamę",
            Filter = "Obrazy (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            EnsureAdStorage();
            var extension = Path.GetExtension(dialog.FileName);
            var savedName = $"{DateTime.Now:yyyyMMdd_HHmmssfff}{extension}";
            var destinationPath = Path.Combine(_adStorageFolder, savedName);
            File.Copy(dialog.FileName, destinationPath, overwrite: false);

            _ads.Add(new AdEntry(savedName, Path.GetFileName(dialog.FileName)));
            SaveAdsManifest();
            AdListBox.SelectedIndex = _ads.Count - 1;
            RotateAds();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Nie udało się dodać reklamy: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteAdButton_Click(object sender, RoutedEventArgs e)
    {
        if (AdListBox.SelectedItem is not AdEntry selected)
        {
            return;
        }

        var selectedIndex = AdListBox.SelectedIndex;
        var filePath = Path.Combine(_adStorageFolder, selected.FileName);
        _ads.Remove(selected);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        SaveAdsManifest();

        if (_ads.Count == 0)
        {
            ShowNoAds();
            return;
        }

        AdListBox.SelectedIndex = Math.Min(selectedIndex, _ads.Count - 1);
        RotateAds();
    }

    private void MoveAdUpButton_Click(object sender, RoutedEventArgs e)
    {
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

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme();
        StartMenuOverlay.Visibility = Visibility.Collapsed;
        StartNewGame();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateBoardLayout();
        Draw();
    }

    private record Tetromino(Point[] Cells, Brush Color, bool IsSquare);
    private record ScoreEntry(string Name, int Points);
    private record AdEntry(string FileName, string DisplayName);
    private record AdManifestItem(string FileName, string DisplayName);
}
