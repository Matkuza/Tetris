using System.Windows;
using System.Windows.Media;

namespace Tetris;

internal sealed class GameEngine(int boardWidth, int boardHeight, Random? random = null)
{
    private readonly Random _random = random ?? new Random();
    private readonly Queue<int> _pieceBag = new();

    public Brush?[,] Board { get; } = new Brush?[boardHeight, boardWidth];

    public Tetromino CurrentPiece { get; set; } = null!;
    public int CurrentX { get; set; }
    public int CurrentY { get; set; }
    public int GroundedTicks { get; set; }
    public int LockResetsUsed { get; set; }

    public void ResetBoard() => Array.Clear(Board);

    public int DrawNextPieceIndex(int pieceCount)
    {
        if (pieceCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pieceCount));
        }

        if (_pieceBag.Count == 0)
        {
            RefillBag(pieceCount);
        }

        return _pieceBag.Dequeue();
    }

    public int CalculateGhostY()
    {
        var ghostY = CurrentY;
        while (IsPositionValid(CurrentX, ghostY + 1, CurrentPiece.Cells))
        {
            ghostY++;
        }

        return ghostY;
    }

    public bool TryMove(int newX, int newY, Point[] cells)
    {
        if (!IsPositionValid(newX, newY, cells))
        {
            return false;
        }

        CurrentX = newX;
        CurrentY = newY;
        return true;
    }

    public bool IsPositionValid(int x, int y, Point[] cells)
    {
        foreach (var cell in cells)
        {
            var boardX = x + (int)cell.X;
            var boardY = y + (int)cell.Y;

            if (boardX < 0 || boardX >= boardWidth || boardY < 0 || boardY >= boardHeight)
            {
                return false;
            }

            if (Board[boardY, boardX] is not null)
            {
                return false;
            }
        }

        return true;
    }

    public bool IsCurrentPieceGrounded() => !IsPositionValid(CurrentX, CurrentY + 1, CurrentPiece.Cells);

    public bool RotateCurrentPiece()
    {
        if (CurrentPiece.IsSquare)
        {
            return false;
        }

        var rotated = CurrentPiece.Cells.Select(p => new Point(2 - p.Y, p.X)).ToArray();
        int[] offsets = [0, -1, 1, -2, 2];

        foreach (var offset in offsets)
        {
            if (!IsPositionValid(CurrentX + offset, CurrentY, rotated))
            {
                continue;
            }

            CurrentX += offset;
            CurrentPiece = CurrentPiece with { Cells = rotated };
            return true;
        }

        return false;
    }

    public bool AddGarbageRow(int holeX)
    {
        for (var y = 0; y < boardHeight - 1; y++)
        {
            for (var x = 0; x < boardWidth; x++)
            {
                Board[y, x] = Board[y + 1, x];
            }
        }

        for (var x = 0; x < boardWidth; x++)
        {
            Board[boardHeight - 1, x] = x == holeX ? null : Brushes.DimGray;
        }

        if (IsPositionValid(CurrentX, CurrentY, CurrentPiece.Cells))
        {
            return true;
        }

        if (TryMove(CurrentX, CurrentY - 1, CurrentPiece.Cells))
        {
            GroundedTicks = 0;
            return true;
        }

        return false;
    }

    public void LockPiece()
    {
        foreach (var cell in CurrentPiece.Cells)
        {
            var boardX = CurrentX + (int)cell.X;
            var boardY = CurrentY + (int)cell.Y;
            if (boardY >= 0 && boardY < boardHeight && boardX >= 0 && boardX < boardWidth)
            {
                Board[boardY, boardX] = CurrentPiece.Color;
            }
        }
    }

    public List<int> ClearFullLines()
    {
        List<int> removedRows = [];

        for (var y = boardHeight - 1; y >= 0; y--)
        {
            var isFull = true;
            for (var x = 0; x < boardWidth; x++)
            {
                if (Board[y, x] is null)
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
                for (var x = 0; x < boardWidth; x++)
                {
                    Board[pullY, x] = Board[pullY - 1, x];
                }
            }

            for (var x = 0; x < boardWidth; x++)
            {
                Board[0, x] = null;
            }

            y++;
        }

        return removedRows;
    }

    private void RefillBag(int pieceCount)
    {
        var nextBag = Enumerable.Range(0, pieceCount).ToArray();
        for (var i = nextBag.Length - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (nextBag[i], nextBag[j]) = (nextBag[j], nextBag[i]);
        }

        foreach (var pieceIndex in nextBag)
        {
            _pieceBag.Enqueue(pieceIndex);
        }
    }
}
