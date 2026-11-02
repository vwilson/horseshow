#if WINDOWS
using Microsoft.UI.Windowing;
using Windows.Graphics;
#endif

namespace HorseShow.App.Platform;

/// <summary>Window state the cross-platform <see cref="Window"/> API does not expose.</summary>
public static class WindowPlacement
{
    /// <summary>
    /// True when the window is neither minimized nor maximized, so its frame is worth remembering.
    /// A minimized window reports the -32000,-32000 parking spot, which would reopen off-screen.
    /// </summary>
    public static bool IsRestored(Window window)
    {
#if WINDOWS
        return window.Handler?.PlatformView is Microsoft.UI.Xaml.Window { AppWindow.Presenter: OverlappedPresenter presenter }
            && presenter.State == OverlappedPresenterState.Restored;
#else
        return true;
#endif
    }

    /// <summary>
    /// Centers the window on the primary display when no display shows any of it, as happens when the
    /// saved position belongs to a monitor that has since been unplugged.
    /// </summary>
    public static void EnsureOnScreen(Window window)
    {
#if WINDOWS
        if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window { AppWindow: { } appWindow })
        {
            return;
        }

        var position = appWindow.Position;
        var size = appWindow.Size;
        if (DisplayArea.GetFromRect(new RectInt32(position.X, position.Y, size.Width, size.Height), DisplayAreaFallback.None) is not null)
        {
            return;
        }

        var work = DisplayArea.Primary.WorkArea;
        appWindow.Move(new PointInt32(
            work.X + Math.Max(0, (work.Width - size.Width) / 2),
            work.Y + Math.Max(0, (work.Height - size.Height) / 2)));
#endif
    }
}
