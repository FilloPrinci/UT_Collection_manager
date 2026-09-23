namespace UTLauncher.Core.Games;

public sealed record VerificationResult(bool IsValid, IReadOnlyList<string> Issues);
