namespace UTLauncher.Core.Games;

/// <summary>
/// Reimplements the matching semantics of the non-recursive 7-Zip "-x!" exclusion patterns
/// used by the OldUnreal Linux installer scripts (Linux/src/lib/unarchiver.sh), so the same
/// exclusion pattern lists can be ported verbatim from those scripts.
/// </summary>
public static class OldUnrealExclusionMatcher
{
    public static bool IsExcluded(string relativePath, IReadOnlyList<string> patterns) =>
        patterns.Any(pattern => IsExcludedByPattern(relativePath, pattern));

    private static bool IsExcludedByPattern(string relativePath, string pattern)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        var normalizedPattern = pattern.Replace('\\', '/');

        if (!normalizedPattern.Contains('/'))
        {
            // A bare root-level name matches that entry itself, or (if it is a directory)
            // everything underneath it - mirroring how 7z's -x! excludes whole directories.
            return normalizedPath.Equals(normalizedPattern, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(normalizedPattern + "/", StringComparison.OrdinalIgnoreCase);
        }

        var patternSeparator = normalizedPattern.LastIndexOf('/');
        var patternDirectory = normalizedPattern[..patternSeparator];
        var patternFileName = normalizedPattern[(patternSeparator + 1)..];

        var pathSeparator = normalizedPath.LastIndexOf('/');
        var pathDirectory = pathSeparator >= 0 ? normalizedPath[..pathSeparator] : string.Empty;
        var pathFileName = pathSeparator >= 0 ? normalizedPath[(pathSeparator + 1)..] : normalizedPath;

        if (!pathDirectory.Equals(patternDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return MatchesFileName(pathFileName, patternFileName);
    }

    private static bool MatchesFileName(string fileName, string pattern)
    {
        if (!pattern.Contains('*'))
        {
            return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        // Every wildcard pattern in the OldUnreal scripts is of the form "*.ext".
        var extension = pattern[(pattern.LastIndexOf('*') + 1)..];
        return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
    }
}
