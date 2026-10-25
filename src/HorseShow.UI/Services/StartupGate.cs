namespace HorseShow.UI.Services;

/// <summary>
/// Lets the UI wait for database initialization without the host blocking its UI thread.
/// Hosts call <see cref="MarkReady"/> or <see cref="MarkFailed"/>; <c>Routes</c> renders a
/// progress or error view until then.
/// </summary>
public sealed class StartupGate
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completed => _tcs.Task;
    public Exception? Error { get; private set; }
    public bool IsReady => _tcs.Task.IsCompleted && Error is null;

    public void MarkReady() => _tcs.TrySetResult();

    public void MarkFailed(Exception error)
    {
        Error = error;
        _tcs.TrySetResult();
    }
}
