using UTLauncher.Core.Tasks;

namespace UTLauncher.Cli;

public sealed class ConsoleProgressReporter : IProgress<TaskProgress>
{
    private string? _lastStepText;
    private double? _lastPercentBucket;

    public void Report(TaskProgress value)
    {
        var bucket = value.PercentComplete.HasValue ? Math.Floor(value.PercentComplete.Value / 5) * 5 : (double?)null;

        if (value.StepText == _lastStepText && bucket == _lastPercentBucket)
        {
            return;
        }

        _lastStepText = value.StepText;
        _lastPercentBucket = bucket;

        var percentText = value.PercentComplete.HasValue ? $" ({value.PercentComplete.Value:0}%)" : string.Empty;
        Console.WriteLine($"{value.StepText}{percentText}");
    }
}
