namespace HorseShow.App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage())
        {
            Title = "HorseShow",
            MinimumWidth = 1100,
            MinimumHeight = 700,
            Width = Preferences.Default.Get("window.width", 1400.0),
            Height = Preferences.Default.Get("window.height", 900.0),
        };

        // Restore the last position only when it was saved; otherwise let Windows place the window.
        if (Preferences.Default.ContainsKey("window.x"))
        {
            window.X = Preferences.Default.Get("window.x", 100.0);
            window.Y = Preferences.Default.Get("window.y", 100.0);
        }

        window.Destroying += (_, _) =>
        {
            Preferences.Default.Set("window.width", window.Width);
            Preferences.Default.Set("window.height", window.Height);
            Preferences.Default.Set("window.x", window.X);
            Preferences.Default.Set("window.y", window.Y);
        };

        return window;
    }
}
