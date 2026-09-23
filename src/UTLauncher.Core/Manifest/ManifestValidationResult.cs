namespace UTLauncher.Core.Manifest;

public sealed record ManifestValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}
