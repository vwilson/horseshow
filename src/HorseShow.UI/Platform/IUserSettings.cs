namespace HorseShow.UI.Platform;

/// <summary>Small per-user key/value store: theme, window state, last-used choices. Host-provided.</summary>
public interface IUserSettings
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string? value);
}

public static class SettingKeys
{
    public const string DarkMode = "ui.darkMode";
}
