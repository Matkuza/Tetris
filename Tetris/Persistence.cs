using System.Text.Json;

namespace Tetris;

internal record ScoreEntryData(string Name, int Points);
internal record HighscoreStoreData(Dictionary<string, List<ScoreEntryData>> Modes);

internal static class HighscorePersistence
{
    public static Dictionary<string, List<ScoreEntryData>> Parse(string? json, IEnumerable<string> modeKeys, int maxEntries = 100)
    {
        var keys = modeKeys.ToList();
        var result = keys.ToDictionary(k => k, _ => new List<ScoreEntryData>(), StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        var trimmed = json.TrimStart();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            var legacyEntries = JsonSerializer.Deserialize<List<ScoreEntryData>>(json) ?? [];
            var targetKey = keys.FirstOrDefault(k => k.Equals("Classic", StringComparison.OrdinalIgnoreCase)) ?? keys.FirstOrDefault();
            if (targetKey is not null)
            {
                result[targetKey] = legacyEntries
                    .OrderByDescending(e => e.Points)
                    .Take(maxEntries)
                    .ToList();
            }

            return result;
        }

        var store = JsonSerializer.Deserialize<HighscoreStoreData>(json);
        if (store?.Modes is null)
        {
            return result;
        }

        foreach (var (modeKey, entries) in store.Modes)
        {
            var normalizedKey = keys.FirstOrDefault(k => k.Equals(modeKey, StringComparison.OrdinalIgnoreCase));
            if (normalizedKey is null)
            {
                continue;
            }

            result[normalizedKey] = (entries ?? [])
                .OrderByDescending(e => e.Points)
                .Take(maxEntries)
                .ToList();
        }

        return result;
    }

    public static string Serialize(Dictionary<string, List<ScoreEntryData>> byMode, int maxEntries = 100, JsonSerializerOptions? options = null)
    {
        var trimmed = byMode.ToDictionary(
            pair => pair.Key,
            pair => (pair.Value ?? [])
                .OrderByDescending(e => e.Points)
                .Take(maxEntries)
                .ToList());

        return JsonSerializer.Serialize(new HighscoreStoreData(trimmed), options ?? new JsonSerializerOptions { WriteIndented = true });
    }
}

internal static class SettingsPersistence
{
    public static GameSettings DeserializeOrDefault(string? json, GameSettings defaults)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return defaults;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<GameSettings>(json);
            return settings is null ? defaults : Normalize(settings);
        }
        catch
        {
            return defaults;
        }
    }

    public static string Serialize(GameSettings settings, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(Normalize(settings), options ?? new JsonSerializerOptions { WriteIndented = true });
    }

    public static GameSettings Normalize(GameSettings settings)
    {
        static string KeyOr(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

        return settings with
        {
            Nick = string.IsNullOrWhiteSpace(settings.Nick) ? "Gracz" : settings.Nick.Trim(),
            StartLevelIndex = Math.Clamp(settings.StartLevelIndex, 0, 2),
            GameModeIndex = Math.Clamp(settings.GameModeIndex, 0, 4),
            ThemeIndex = Math.Clamp(settings.ThemeIndex, 0, 2),
            MusicVolume = Math.Clamp(settings.MusicVolume, 0, 1),
            EffectsVolume = Math.Clamp(settings.EffectsVolume, 0, 1),
            DasMs = Math.Clamp(settings.DasMs, 0, 500),
            ArrMs = Math.Clamp(settings.ArrMs, 0, 300),
            SoftDropArrMs = Math.Clamp(settings.SoftDropArrMs, 0, 200),
            MoveLeftKey = KeyOr(settings.MoveLeftKey, "Left"),
            MoveRightKey = KeyOr(settings.MoveRightKey, "Right"),
            SoftDropKey = KeyOr(settings.SoftDropKey, "Down"),
            RotateKey = KeyOr(settings.RotateKey, "Up"),
            HardDropKey = KeyOr(settings.HardDropKey, "Space"),
            HoldKey = KeyOr(settings.HoldKey, "C"),
            ShowSessionStats = settings.ShowSessionStats ?? true,
            MusicEnabled = settings.MusicEnabled ?? true,
            EffectsEnabled = settings.EffectsEnabled ?? true,
            LockParticlesEnabled = settings.LockParticlesEnabled ?? true,
            AdminPassword = string.IsNullOrWhiteSpace(settings.AdminPassword) ? "admin" : settings.AdminPassword.Trim()
        };
    }
}
