using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Tetris;

public partial class MainWindow : Window
{
    private const int BoardWidth = 10;
    private const int BoardHeight = 20;
    private const int CellSize = 36;

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

    private static readonly (Point[] shape, Brush color)[] PieceDefinitions =
    [
        (new[] { new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1) }, Brushes.Cyan),
        (new[] { new Point(0, 0), new Point(0, 1), new Point(1, 1), new Point(2, 1) }, Brushes.Blue),
        (new[] { new Point(2, 0), new Point(0, 1), new Point(1, 1), new Point(2, 1) }, Brushes.Orange),
        (new[] { new Point(1, 0), new Point(2, 0), new Point(1, 1), new Point(2, 1) }, Brushes.Yellow),
        (new[] { new Point(1, 0), new Point(2, 0), new Point(0, 1), new Point(1, 1) }, Brushes.LimeGreen),
        (new[] { new Point(1, 0), new Point(0, 1), new Point(1, 1), new Point(2, 1) }, Brushes.MediumPurple),
        (new[] { new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(2, 1) }, Brushes.Red)
    ];

    public MainWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(520) };
        _timer.Tick += (_, _) => Tick();

        ResetGame();
    }

    private void ResetGame()
    {
        Array.Clear(_board);
        _score = 0;
        _linesCleared = 0;
        _gameOver = false;
        _nextPiece = CreateRandomPiece();

        SpawnPiece();
        _timer.Start();
        UpdateHud();
        Draw();
    }

    private void Tick()
    {
        if (_gameOver)
        {
            return;
        }

        if (!TryMove(_currentX, _currentY + 1, _currentPiece.Cells))
        {
            LockPiece();
            ClearFullLines();
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
        var (shape, color) = PieceDefinitions[_random.Next(PieceDefinitions.Length)];
        return new Tetromino(shape.Select(p => new Point(p.X, p.Y)).ToArray(), color);
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
        if (_currentPiece.Color == Brushes.Yellow)
        {
            return;
        }

        var rotated = _currentPiece.Cells
            .Select(p => new Point(2 - p.Y, p.X))
            .ToArray();

        // prosta "wall kick" - kilka prób przesunięcia po obrocie
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
            _score += 2;
        }

        LockPiece();
        ClearFullLines();
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

    private void ClearFullLines()
    {
        var removed = 0;

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

            removed++;
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

            y++; // ponowne sprawdzenie tego samego wiersza po zsunięciu
        }

        if (removed == 0)
        {
            return;
        }

        _linesCleared += removed;
        _score += removed switch
        {
            1 => 100,
            2 => 300,
            3 => 500,
            4 => 800,
            _ => removed * 200
        };

        var level = 1 + (_linesCleared / 8);
        var speedMs = Math.Max(90, 520 - (level - 1) * 40);
        _timer.Interval = TimeSpan.FromMilliseconds(speedMs);
        UpdateHud();
    }

    private void UpdateHud()
    {
        var level = 1 + (_linesCleared / 8);
        ScoreText.Text = _score.ToString();
        LevelText.Text = level.ToString();

        if (!_gameOver)
        {
            StatusText.Text = "Strzałki: ruch/obrót • Spacja: zrzut • Esc: zamknij";
        }
    }

    private void Draw()
    {
        GameCanvas.Children.Clear();

        // tło kratki
        for (var y = 0; y < BoardHeight; y++)
        {
            for (var x = 0; x < BoardWidth; x++)
            {
                DrawCell(x, y, _board[y, x] ?? new SolidColorBrush(Color.FromRgb(17, 24, 39)),
                    _board[y, x] is null ? 0.18 : 1);
            }
        }

        if (!_gameOver)
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
            Width = CellSize - 2,
            Height = CellSize - 2,
            RadiusX = 6,
            RadiusY = 6,
            Fill = brush,
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            Opacity = opacity
        };

        Canvas.SetLeft(rect, x * CellSize + 1);
        Canvas.SetTop(rect, y * CellSize + 1);
        GameCanvas.Children.Add(rect);
    }

    private void DrawNextPiece()
    {
        NextPieceCanvas.Children.Clear();

        foreach (var cell in _nextPiece.Cells)
        {
            var rect = new Rectangle
            {
                Width = 24,
                Height = 24,
                RadiusX = 5,
                RadiusY = 5,
                Fill = _nextPiece.Color,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            Canvas.SetLeft(rect, cell.X * 24 + 18);
            Canvas.SetTop(rect, cell.Y * 24 + 18);
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

        if (_gameOver)
        {
            if (e.Key == Key.Enter)
            {
                ResetGame();
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
                if (TryMove(_currentX, _currentY + 1, _currentPiece.Cells))
                {
                    _score += 1;
                    UpdateHud();
                }

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

    private record Tetromino(Point[] Cells, Brush Color);
}
