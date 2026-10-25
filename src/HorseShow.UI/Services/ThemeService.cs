using MudBlazor;

namespace HorseShow.UI.Services;

/// <summary>Scoped per circuit/window. Persisting the choice arrives with Settings in Phase 5.</summary>
public sealed class ThemeService
{
    public event Action? Changed;

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

    public void Toggle() => IsDarkMode = !IsDarkMode;
}
