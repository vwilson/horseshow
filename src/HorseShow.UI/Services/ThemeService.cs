using HorseShow.UI.Platform;
using MudBlazor;

namespace HorseShow.UI.Services;

/// <summary>Scoped per circuit/window. The dark-mode choice is persisted through <see cref="IUserSettings"/>.</summary>
public sealed class ThemeService(IUserSettings settings)
{
    public event Action? Changed;

    public async Task LoadAsync()
    {
        var stored = await settings.GetAsync(SettingKeys.DarkMode);
        if (bool.TryParse(stored, out var dark))
        {
            IsDarkMode = dark;
        }
    }

    public bool IsDarkMode
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke();
        }
    }

    public MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2E5E4E",
            Secondary = "#8C6A3F",
            AppbarBackground = "#2E5E4E",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#7FB69E",
            Secondary = "#C9A368",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif"] },
        },
    };

    public async Task Toggle()
    {
        IsDarkMode = !IsDarkMode;
        await settings.SetAsync(SettingKeys.DarkMode, IsDarkMode.ToString());
    }
}
