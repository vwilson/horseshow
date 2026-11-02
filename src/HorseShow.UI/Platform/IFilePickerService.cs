namespace HorseShow.UI.Platform;

public sealed record PickedFile(string FileName, Func<Task<Stream>> OpenRead);

public interface IFilePickerService
{
    /// <param name="extensions">Extensions including the dot, e.g. ".csv".</param>
    Task<PickedFile?> PickAsync(params string[] extensions);
}

public interface IFileSaveService
{
    /// <returns>The saved path, or null if the user cancelled.</returns>
    Task<string?> SaveAsync(string suggestedFileName, Stream content);
}
