namespace UTLauncher.Core.Platform;

/// <summary>
/// Runs the system checks described in SPEC.md §5.1: disk space, and (on Linux) whether the
/// optional libraries UT99/UT2004 prefer over their bundled copies are already present, with a
/// copyable install command for the detected distro when they aren't. Shared by the CLI's
/// `doctor` command and the App's system-check view so both report the same thing.
/// </summary>
public sealed class SystemChecker(SystemLibraryLocator libraryLocator)
{
    private static readonly (string LibraryName, string DisplayName)[] OptionalLibraries =
    [
        ("libopenal.so.1", "OpenAL"),
        ("libSDL3.so.0", "SDL3"),
        ("libomp.so.5", "libomp"),
    ];

    public async Task<IReadOnlyList<SystemCheckResult>> RunAsync(CancellationToken cancellationToken)
    {
        var results = new List<SystemCheckResult> { CheckDiskSpace() };

        if (OperatingSystem.IsWindows())
        {
            return results;
        }

        var distroId = LinuxDistroDetector.DetectId();

        foreach (var (libraryName, displayName) in OptionalLibraries)
        {
            var path = await libraryLocator.FindAsync(libraryName, cancellationToken).ConfigureAwait(false);

            // Ut2004Installer also falls back to the unversioned "libomp.so" (symlinking it to
            // libomp.so.5 itself), so this checks the same fallback to stay consistent with what
            // install actually does.
            if (path is null && libraryName == "libomp.so.5")
            {
                path = await libraryLocator.FindAsync("libomp.so", cancellationToken).ConfigureAwait(false);
            }

            if (path is not null)
            {
                results.Add(new SystemCheckResult(
                    displayName, true, $"Found on the system ({path}) - UT99/UT2004 will prefer it."));
                continue;
            }

            var hint = InstallHintFor(distroId, libraryName);
            results.Add(new SystemCheckResult(
                displayName, false, "Not found on the system - UT99/UT2004 will use the copy bundled with the patch.", hint));
        }

        return results;
    }

    private static SystemCheckResult CheckDiskSpace()
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var root = Path.GetPathRoot(Path.GetFullPath(home));
            if (string.IsNullOrEmpty(root))
            {
                return new SystemCheckResult("Disk space", false, "Could not be determined.");
            }

            var drive = new DriveInfo(root);
            var freeGigabytes = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
            return new SystemCheckResult("Disk space", true, $"{freeGigabytes:0.0} GB free on {drive.Name}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new SystemCheckResult("Disk space", false, "Could not be determined.");
        }
    }

    public static string? InstallHintFor(string? distroId, string libraryName) => (distroId, libraryName) switch
    {
        ("fedora", "libopenal.so.1") => "sudo dnf install openal-soft",
        ("fedora", "libSDL3.so.0") => "sudo dnf install SDL3",
        ("fedora", "libomp.so.5") => "sudo dnf install libomp",
        ("ubuntu" or "debian" or "linuxmint", "libopenal.so.1") => "sudo apt install libopenal1",
        ("ubuntu" or "debian" or "linuxmint", "libSDL3.so.0") => "sudo apt install libsdl3-0",
        ("ubuntu" or "debian" or "linuxmint", "libomp.so.5") => "sudo apt install libomp5",
        ("arch" or "endeavouros" or "manjaro", "libopenal.so.1") => "sudo pacman -S openal",
        ("arch" or "endeavouros" or "manjaro", "libSDL3.so.0") => "sudo pacman -S sdl3",
        ("arch" or "endeavouros" or "manjaro", "libomp.so.5") => "sudo pacman -S openmp",
        _ => null,
    };
}
