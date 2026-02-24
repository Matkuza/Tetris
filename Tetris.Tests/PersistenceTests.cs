using System.Text.Json;
using Xunit;

namespace Tetris.Tests;

public class PersistenceTests
{
    [Fact]
    public void SettingsDeserializeOrDefault_ReturnsDefaultsForInvalidJson()
    {
        var defaults = new GameSettings("Player", 0, 0, 0, 0.6, 0.8, 140, 45, "Left", "Right", "Down", "Up", "Space", "C", false);

        var parsed = SettingsPersistence.DeserializeOrDefault("not-json", defaults);

        Assert.Equal(defaults, parsed);
    }

    [Fact]
    public void SettingsSerialize_NormalizesOutOfRangeValues()
    {
        var settings = new GameSettings("  ", 99, 99, 99, 9, -2, 9999, -2, "", "", "", "", "", "", true);

        var json = SettingsPersistence.Serialize(settings);
        var parsed = JsonSerializer.Deserialize<GameSettings>(json);

        Assert.NotNull(parsed);
        Assert.Equal("Gracz", parsed!.Nick);
        Assert.Equal(2, parsed.StartLevelIndex);
        Assert.Equal(4, parsed.GameModeIndex);
        Assert.Equal(2, parsed.ThemeIndex);
        Assert.Equal(1, parsed.MusicVolume);
        Assert.Equal(0, parsed.EffectsVolume);
        Assert.Equal(500, parsed.DasMs);
        Assert.Equal(0, parsed.ArrMs);
        Assert.Equal("Left", parsed.MoveLeftKey);
        Assert.True(parsed.ColorblindMode);
    }

    [Fact]
    public void HighscoreParse_MigratesLegacyListToClassic()
    {
        var legacy = JsonSerializer.Serialize(new List<ScoreEntryData>
        {
            new("A", 10), new("B", 200), new("C", 150), new("D", 50), new("E", 80), new("F", 5)
        });

        var parsed = HighscorePersistence.Parse(legacy, new[] { "Classic", "Ultra" });

        Assert.Equal(5, parsed["Classic"].Count);
        Assert.Equal(200, parsed["Classic"][0].Points);
        Assert.Empty(parsed["Ultra"]);
    }

    [Fact]
    public void HighscoreSerialize_OrdersAndTrimsPerMode()
    {
        var data = new Dictionary<string, List<ScoreEntryData>>
        {
            ["Classic"] =
            [
                new("A", 2), new("B", 100), new("C", 50), new("D", 30), new("E", 20), new("F", 10)
            ]
        };

        var json = HighscorePersistence.Serialize(data);
        var parsed = HighscorePersistence.Parse(json, new[] { "Classic" });

        Assert.Equal(5, parsed["Classic"].Count);
        Assert.Equal(100, parsed["Classic"][0].Points);
        Assert.Equal(10, parsed["Classic"][4].Points);
    }
}
