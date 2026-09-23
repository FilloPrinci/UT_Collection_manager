namespace UTLauncher.Core.Tasks;

public sealed class LauncherTask(string name)
{
    public string Name { get; } = name;

    public LauncherTaskStatus Status { get; private set; } = LauncherTaskStatus.Pending;

    public TaskProgress? LastProgress { get; private set; }

    public Exception? Error { get; private set; }

    public event EventHandler<TaskProgress>? ProgressChanged;

    public event EventHandler<LauncherTaskStatus>? StatusChanged;

    public async Task RunAsync(Func<IProgress<TaskProgress>, CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        SetStatus(LauncherTaskStatus.Running);
        var reporter = new DelegatingProgress<TaskProgress>(ReportProgress);

        try
        {
            await work(reporter, cancellationToken).ConfigureAwait(false);
            SetStatus(LauncherTaskStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            SetStatus(LauncherTaskStatus.Cancelled);
            throw;
        }
        catch (Exception ex)
        {
            Error = ex;
            SetStatus(LauncherTaskStatus.Failed);
            throw;
        }
    }

    private void ReportProgress(TaskProgress progress)
    {
        LastProgress = progress;
        ProgressChanged?.Invoke(this, progress);
    }

    private void SetStatus(LauncherTaskStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, status);
    }

    private sealed class DelegatingProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
