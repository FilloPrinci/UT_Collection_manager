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

        if (value.PercentComplete is not { } percent)
        {
            Console.WriteLine(value.StepText);
            return;
        }

        var speedText = value.BytesPerSecond is > 0 ? $", {FormatSpeed(value.BytesPerSecond.Value)}" : string.Empty;
        Console.WriteLine($"{value.StepText} ({percent:0}%{speedText})");
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        return bytesPerSecond >= mb
            ? $"{bytesPerSecond / mb:0.#} MB/s"
            : $"{bytesPerSecond / kb:0.#} KB/s";
    }
}
