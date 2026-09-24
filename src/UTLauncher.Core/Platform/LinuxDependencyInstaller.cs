using Microsoft.Extensions.Logging;
using UTLauncher.Core.Processes;
using UTLauncher.Core.Tasks;

namespace UTLauncher.Core.Platform;

/// <summary>
/// Installs a missing system library automatically via the detected distro's package manager,
/// elevated through <c>pkexec</c> (the standard way for a desktop-Linux GUI app to request root
/// without a terminal - it shows the desktop's own native authentication dialog). Best-effort: if
/// pkexec isn't available (no polkit agent running) or the distro/library combination isn't
/// mapped, this logs a warning and leaves the library missing rather than failing the install -
/// UT2004 still has its own bundled copy either way, and UT99 will surface a clear in-game error
/// if OpenAL specifically is still missing afterward.
/// </summary>
public sealed class LinuxDependencyInstaller(
    SystemLibraryLocator libraryLocator,
    ProcessRunner processRunner,
    ILogger<LinuxDependencyInstaller> logger)
{
    public async Task EnsureInstalledAsync(
        IReadOnlyList<string> libraryNames,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var distroId = LinuxDistroDetector.DetectId();

        foreach (var libraryName in libraryNames)
        {
            await EnsureLibraryInstalledAsync(libraryName, distroId, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnsureLibraryInstalledAsync(
        string libraryName,
        string? distroId,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        var path = await libraryLocator.FindAsync(libraryName, cancellationToken).ConfigureAwait(false);

        // Same fallback SystemChecker/Ut2004Installer use for libomp's unversioned name.
        if (path is null && libraryName == "libomp.so.5")
        {
            path = await libraryLocator.FindAsync("libomp.so", cancellationToken).ConfigureAwait(false);
        }

        if (path is not null)
        {
            return;
        }

        var packageName = SystemChecker.PackageNameFor(distroId, libraryName);
        var installArgs = BuildPackageManagerInstallArgs(distroId, packageName);
        if (installArgs is null)
        {
            logger.LogWarning(
                "No known package manager command to install {Library} on distro '{DistroId}'; leaving it missing",
                libraryName, distroId ?? "unknown");
            return;
        }

        logger.LogInformation("{Library} not found on the system, installing it automatically", libraryName);
        progress?.Report(TaskProgress.Indeterminate($"Installing {packageName}"));

        ProcessResult result;
        try
        {
            result = await processRunner.RunAsync("pkexec", installArgs, workingDirectory: null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.LogWarning(ex, "Could not run pkexec to install {Library}; leaving it missing", libraryName);
            return;
        }

        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Automatic install of {Library} failed (exit code {ExitCode}); leaving it missing",
                libraryName, result.ExitCode);
        }
    }

    internal static string[]? BuildPackageManagerInstallArgs(string? distroId, string? packageName)
    {
        if (packageName is null)
        {
            return null;
        }

        return distroId switch
        {
            "fedora" => ["dnf", "install", "-y", packageName],
            "ubuntu" or "debian" or "linuxmint" => ["apt-get", "install", "-y", packageName],
            "arch" or "endeavouros" or "manjaro" => ["pacman", "-S", "--noconfirm", packageName],
            _ => null,
        };
    }
}
