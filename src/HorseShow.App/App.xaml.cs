namespace HorseShow.App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage())
        {
            Title = "HorseShow",
            MinimumWidth = 1100,
            MinimumHeight = 700,
            Width = 1400,
            Height = 900,
        };
    }
}
