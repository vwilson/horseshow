namespace HorseShow.UI.Platform;

public interface IClipboardService
{
    Task<string?> GetTextAsync();
}
