namespace HorseShow.Import.Model;

public enum IssueSeverity { Warning, Error }

public sealed record ImportIssue(IssueSeverity Severity, string Message)
{
    public static ImportIssue Error(string message) => new(IssueSeverity.Error, message);
    public static ImportIssue Warning(string message) => new(IssueSeverity.Warning, message);
}
