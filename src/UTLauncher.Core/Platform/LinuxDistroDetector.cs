namespace UTLauncher.Core.Platform;

/// <summary>
/// Reads the distro ID from /etc/os-release (the freedesktop.org standard), so system checks can
/// show a copyable install command for the right package manager.
/// </summary>
public static class LinuxDistroDetector
{
    public static string? DetectId(string osReleasePath = "/etc/os-release")
    {
        if (!File.Exists(osReleasePath))
        {
            return null;
        }

        foreach (var line in File.ReadAllLines(osReleasePath))
        {
            if (!line.StartsWith("ID=", StringComparison.Ordinal))
            {
                continue;
            }

            return line["ID=".Length..].Trim().Trim('"');
        }

        return null;
    }
}
