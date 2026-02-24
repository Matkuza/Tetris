using System.Windows;
using System.Windows.Media;
using System.Text.Json;
using Xunit;


namespace Tetris.Tests;

public class GameEngineTests
{
    [Fact]
    public void DrawNextPieceIndex_Uses7BagWithoutRepeatsInSingleBag()
    {
        var engine = new GameEngine(10, 20, new Random(123));

        var draw = Enumerable.Range(0, 7)
            .Select(_ => engine.DrawNextPieceIndex(7))
            .ToList();

        Assert.Equal(7, draw.Distinct().Count());
        Assert.Equal(Enumerable.Range(0, 7).OrderBy(x => x), draw.OrderBy(x => x));
    }

    [Fact]
    public void DrawNextPieceIndex_RefillsBagAfterDepletion()
    {
        var engine = new GameEngine(10, 20, new Random(7));

        var firstBag = Enumerable.Range(0, 7)
            .Select(_ => engine.DrawNextPieceIndex(7))
            .OrderBy(x => x)
            .ToList();
        var secondBag = Enumerable.Range(0, 7)
            .Select(_ => engine.DrawNextPieceIndex(7))
            .OrderBy(x => x)
            .ToList();

        var expected = Enumerable.Range(0, 7).ToList();
        Assert.Equal(expected, firstBag);
        Assert.Equal(expected, secondBag);
    }

    [Fact]
    public void CalculateGhostY_ReturnsLandingRowBeforeObstacle()
    {
        var engine = new GameEngine(10, 20);
        engine.CurrentPiece = CreateIPiece();
        engine.CurrentX = 3;
        engine.CurrentY = 0;

        for (var x = 0; x < 10; x++)
        {
            engine.Board[18, x] = Brushes.Gray;
        }

        var ghostY = engine.CalculateGhostY();

        Assert.Equal(16, ghostY);
    }

    [Fact]
    public void ClearFullLines_RemovesCompleteRows()
    {
        var engine = new GameEngine(10, 20);

        for (var x = 0; x < 10; x++)
        {
            engine.Board[19, x] = Brushes.Red;
        }

        engine.Board[18, 0] = Brushes.Blue;

        var removed = engine.ClearFullLines();

        Assert.Single(removed);
        Assert.Equal(19, removed[0]);
        Assert.Equal(Brushes.Blue, engine.Board[19, 0]);
    }

    private static Tetromino CreateIPiece()
    {
        var cells = new[]
        {
            new Point(0, 1),
            new Point(1, 1),
            new Point(2, 1),
            new Point(3, 1)
        };

        return new Tetromino(cells, Brushes.Cyan, false);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 120)]
    [InlineData(2, 360)]
    [InlineData(3, 700)]
    [InlineData(4, 1100)]
    public void CalculateScoreForClearedLines_ReturnsExpectedValue(int clearedLines, int expected)
    {
        var score = GameEngine.CalculateScoreForClearedLines(clearedLines);

        Assert.Equal(expected, score);
    }

    [Fact]
    public void IsGameOverOnSpawn_ReturnsTrueWhenSpawnBlocked()
    {
        var engine = new GameEngine(10, 20);
        var piece = CreateIPiece();
        engine.Board[1, 4] = Brushes.Red;

        var blocked = engine.IsGameOverOnSpawn(3, 0, piece.Cells);

        Assert.True(blocked);
    }

    [Fact]
    public void GameSettings_SerializesAndDeserializesControlBindings()
    {
        var settings = new GameSettings(
            "Tester",
            1,
            2,
            0,
            0.5,
            0.4,
            150,
            35,
            "A",
            "D",
            "S",
            "W",
            "Space",
            "LeftShift",
            true,
            true,
            true,
            true,
            "admin");

        var json = JsonSerializer.Serialize(settings);
        var roundtrip = JsonSerializer.Deserialize<GameSettings>(json);

        Assert.NotNull(roundtrip);
        Assert.Equal("A", roundtrip!.MoveLeftKey);
        Assert.Equal(150, roundtrip.DasMs);
        Assert.Equal(35, roundtrip.ArrMs);
    }
    [Fact]
    public void DrawNextPieceIndex_ThrowsWhenPieceCountIsInvalid()
    {
        var engine = new GameEngine(10, 20);

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.DrawNextPieceIndex(0));
    }

    [Fact]
    public void IsGameOverOnSpawn_ReturnsFalseWhenSpawnIsFree()
    {
        var engine = new GameEngine(10, 20);
        var piece = CreateIPiece();

        var blocked = engine.IsGameOverOnSpawn(3, 0, piece.Cells);

        Assert.False(blocked);
    }

    [Fact]
    public void CalculateScoreForClearedLines_FallsBackForFiveLines()
    {
        var score = GameEngine.CalculateScoreForClearedLines(5);

        Assert.Equal(1250, score);
    }

}
