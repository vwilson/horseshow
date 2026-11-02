namespace HorseShow.Import.Tests;

public static class Fixture
{
    public static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    public static string Text(string name) => File.ReadAllText(Path(name));
    public static FileStream Open(string name) => File.OpenRead(Path(name));
}
