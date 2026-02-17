using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Tetris;

public partial class MainWindow : Window
{
    private const int BoardWidth = 10;
    private const int BoardHeight = 20;

    private readonly Brush?[,] _board = new Brush?[BoardHeight, BoardWidth];
    private readonly Random _random = new();
    private readonly DispatcherTimer _timer;

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

    private static readonly Color[] NeonPalette =
    [
        Color.FromRgb(34, 211, 238), Color.FromRgb(59, 130, 246), Color.FromRgb(251, 146, 60),
        Color.FromRgb(250, 204, 21), Color.FromRgb(74, 222, 128), Color.FromRgb(167, 139, 250),
        Color.FromRgb(248, 113, 113)
    ];

    private static readonly Color[] PastelPalette =
    [
        Color.FromRgb(125, 211, 252), Color.FromRgb(165, 180, 252), Color.FromRgb(253, 186, 116),
        Color.FromRgb(253, 224, 71), Color.FromRgb(134, 239, 172), Color.FromRgb(216, 180, 254),
        Color.FromRgb(252, 165, 165)
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

        ApplyTheme();
        UpdateBoardLayout();
        Draw();
    }

    private void ApplyTheme()
    {
        var source = ThemeComboBox.SelectedIndex == 1 ? PastelPalette : NeonPalette;
        _activePaletteBrushes = source.Select(c => (Brush)new SolidColorBrush(c)).ToArray();
    }

    private void StartNewGame()
    {
        Array.Clear(_board);
        _score = 0;
        _linesCleared = 0;
        _gameOver = false;
        _isGameStarted = true;

        _startLevel = StartLevelComboBox.SelectedIndex switch
        {
            1 => 2,
            2 => 4,
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
        var speedMs = Math.Max(85, 560 - (level - 1) * 45);
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
            _gameOver = true;
            _timer.Stop();
            StatusText.Text = "Koniec gry • Enter: nowa gra • Esc: zamknij";
        }

        DrawNextPiece();
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
            StatusText.Text = "Punkty tylko za pełne linie • Strzałki/Spacja • Esc: zamknij";
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

        var emptyBrush = new SolidColorBrush(Color.FromRgb(12, 20, 38));
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
            if (e.Key == Key.Enter)
            {
                StartMenuOverlay.Visibility = Visibility.Visible;
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
}
