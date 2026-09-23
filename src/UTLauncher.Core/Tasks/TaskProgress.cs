namespace UTLauncher.Core.Tasks;

public sealed record TaskProgress(
    string StepText,
    bool IsIndeterminate,
    double? PercentComplete = null,
    long? BytesTransferred = null,
    long? TotalBytes = null,
    double? BytesPerSecond = null,
    TimeSpan? EstimatedTimeRemaining = null)
{
    public static TaskProgress Indeterminate(string stepText) => new(stepText, IsIndeterminate: true);

    public static TaskProgress Determinate(
        string stepText,
        long bytesTransferred,
        long totalBytes,
        double? bytesPerSecond = null,
        TimeSpan? estimatedTimeRemaining = null) =>
        new(
            stepText,
            IsIndeterminate: false,
            PercentComplete: totalBytes > 0 ? bytesTransferred / (double)totalBytes * 100.0 : null,
            BytesTransferred: bytesTransferred,
            TotalBytes: totalBytes,
            BytesPerSecond: bytesPerSecond,
            EstimatedTimeRemaining: estimatedTimeRemaining);
}
