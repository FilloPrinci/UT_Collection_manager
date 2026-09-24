namespace UTLauncher.Core.Platform;

/// <summary>
/// Runs the system checks described in SPEC.md §5.1: disk space, and (on Linux) whether the
/// libraries UT2004 prefers over its own bundled copies are already present - OpenAL doubles as a
/// hard requirement for UT99, whose patch bundles no fallback for it - with a copyable install
/// command for the detected distro when they aren't. Shared by the CLI's `doctor` command and the
/// App's system-check view so both report the same thing.
/// </summary>
public sealed class SystemChecker(SystemLibraryLocator libraryLocator)
{
    // UT2004's Linux patch bundles its own copies of all three (preferred only if the system
    // doesn't have them - see Ut2004Installer.ApplyLibraryPreferenceFixesAsync). UT99's patch
    // bundles none of them: for OpenAL specifically, that makes it a hard requirement for UT99
    // (no fallback - ALAudio.so simply fails to load without it, "Can't find file for package
    // ALAudio" at startup), not just a "nice to have" like it is for UT2004.
    private static readonly (string LibraryName, string DisplayName, bool RequiredForUt99)[] OptionalLibraries =
    [
        ("libopenal.so.1", "OpenAL", true),
        ("libSDL3.so.0", "SDL3", false),
        ("libomp.so.5", "libomp", false),
    ];

    public async Task<IReadOnlyList<SystemCheckResult>> RunAsync(CancellationToken cancellationToken)
    {
        var results = new List<SystemCheckResult> { CheckDiskSpace() };

        if (OperatingSystem.IsWindows())
        {
            return results;
        }

        var distroId = LinuxDistroDetector.DetectId();

        foreach (var (libraryName, displayName, requiredForUt99) in OptionalLibraries)
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
                var foundMessage = requiredForUt99
                    ? $"Found on the system ({path}) - required for UT99; UT2004 will prefer it over its own bundled copy."
                    : $"Found on the system ({path}) - UT2004 will prefer it over its own bundled copy.";
                results.Add(new SystemCheckResult(displayName, true, foundMessage));
                continue;
            }

            var hint = InstallHintFor(distroId, libraryName);
            var missingMessage = requiredForUt99
                ? "Not found on the system - required to run UT99 (no bundled fallback); UT2004 has its own bundled copy."
                : "Not found on the system - UT2004 will use its own bundled copy.";
            results.Add(new SystemCheckResult(displayName, false, missingMessage, hint));
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

    public static string? InstallHintFor(string? distroId, string libraryName)
    {
        var packageName = PackageNameFor(distroId, libraryName);
        if (packageName is null)
        {
            return null;
        }

        return distroId switch
        {
            "fedora" => $"sudo dnf install {packageName}",
            "ubuntu" or "debian" or "linuxmint" => $"sudo apt install {packageName}",
            "arch" or "endeavouros" or "manjaro" => $"sudo pacman -S {packageName}",
            _ => null,
        };
    }

    // Shared with LinuxDependencyInstaller (the automatic-install counterpart to this hint) so
    // the package name is only ever listed once per distro/library.
    internal static string? PackageNameFor(string? distroId, string libraryName) => (distroId, libraryName) switch
    {
        ("fedora", "libopenal.so.1") => "openal-soft",
        ("fedora", "libSDL3.so.0") => "SDL3",
        ("fedora", "libomp.so.5") => "libomp",
        ("ubuntu" or "debian" or "linuxmint", "libopenal.so.1") => "libopenal1",
        ("ubuntu" or "debian" or "linuxmint", "libSDL3.so.0") => "libsdl3-0",
        ("ubuntu" or "debian" or "linuxmint", "libomp.so.5") => "libomp5",
        ("arch" or "endeavouros" or "manjaro", "libopenal.so.1") => "openal",
        ("arch" or "endeavouros" or "manjaro", "libSDL3.so.0") => "sdl3",
        ("arch" or "endeavouros" or "manjaro", "libomp.so.5") => "openmp",
        _ => null,
    };
}
