using UTLauncher.Core.Processes;

namespace UTLauncher.Core.Platform;

/// <summary>
/// Looks up shared libraries already provided by the system via <c>ldconfig</c>, so installers
/// and the system-check ("doctor") step can prefer a system-provided library over a bundled one.
/// </summary>
public sealed class SystemLibraryLocator(ProcessRunner processRunner)
{
    public async Task<string?> FindAsync(string libraryName, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        ProcessResult result;
        try
        {
            result = await processRunner.RunAsync("ldconfig", ["-p"], workingDirectory: null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }

        if (!result.Succeeded)
        {
            return null;
        }

        foreach (var line in result.StandardOutput.Split('\n'))
        {
            if (!line.Contains(libraryName, StringComparison.Ordinal))
            {
                continue;
            }

            var arrowIndex = line.IndexOf("=>", StringComparison.Ordinal);
            if (arrowIndex < 0)
            {
                continue;
            }

            var path = line[(arrowIndex + 2)..].Trim();
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
