namespace UTLauncher.Core.Platform;

public sealed record SystemCheckResult(string Name, bool IsOk, string Message, string? Hint = null);
