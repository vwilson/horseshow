using System.Text.Json;

namespace HorseShow.UI.Platform;

/// <summary>JSON-file settings store. Used by the Web host and as the MAUI fallback.</summary>
public sealed class FileUserSettings(string directory) : IUserSettings
{
    private readonly string _file = Path.Combine(directory, "settings.json");
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, string?>? _cache;

    public async Task<string?> GetAsync(string key)
    {
        await _gate.WaitAsync();
        try
        {
            _cache ??= Load();
            return _cache.GetValueOrDefault(key);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetAsync(string key, string? value)
    {
        await _gate.WaitAsync();
        try
        {
            _cache ??= Load();
            if (value is null) _cache.Remove(key); else _cache[key] = value;
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(_file, JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            _gate.Release();
        }
    }

    private Dictionary<string, string?> Load()
    {
        try
        {
            return File.Exists(_file)
                ? JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(_file)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }
}
