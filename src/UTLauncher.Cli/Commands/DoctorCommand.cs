using Microsoft.Extensions.Logging;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Processes;

namespace UTLauncher.Cli.Commands;

public static class DoctorCommand
{
    // UT99/UT2004 prefer these system libraries over the copies bundled with the OldUnreal
    // patches when available (see Ut2004Installer's special fixes). UT4's Vulkan/Python3/user
    // namespace checks aren't included yet - UT4 install isn't implemented.
    private static readonly (string LibraryName, string DisplayName)[] OptionalLibraries =
    [
        ("libopenal.so.1", "OpenAL"),
        ("libSDL3.so.0", "SDL3"),
        ("libomp.so.5", "libomp"),
    ];

    public static async Task<int> RunAsync(ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        Console.WriteLine("System check");
        Console.WriteLine("============");
        Console.WriteLine();

        CheckDiskSpace();
        Console.WriteLine();

        if (OperatingSystem.IsWindows())
        {
            Console.WriteLine("Optional system libraries are only checked on Linux.");
            return 0;
        }

        var processRunner = new ProcessRunner(loggerFactory.CreateLogger<ProcessRunner>());
        var locator = new SystemLibraryLocator(processRunner);
        var distroId = LinuxDistroDetector.DetectId();

        foreach (var (libraryName, displayName) in OptionalLibraries)
        {
            var path = await locator.FindAsync(libraryName, cancellationToken).ConfigureAwait(false);

            // Ut2004Installer also falls back to the unversioned "libomp.so" (symlinking it to
            // libomp.so.5 itself), so doctor checks the same fallback to stay consistent with
            // what install actually does, rather than telling the user to install a library the
            // installer already knows how to work around.
            if (path is null && libraryName == "libomp.so.5")
            {
                path = await locator.FindAsync("libomp.so", cancellationToken).ConfigureAwait(false);
            }

            if (path is not null)
            {
                Console.WriteLine($"[OK] {displayName}: found on the system ({path}) - UT99/UT2004 will prefer it.");
                continue;
            }

            Console.WriteLine($"[--] {displayName}: not found on the system - UT99/UT2004 will use the copy bundled with the patch.");
            var hint = InstallHintFor(distroId, libraryName);
            if (hint is not null)
            {
                Console.WriteLine($"     Install it with: {hint}");
            }
        }

        return 0;
    }

    private static void CheckDiskSpace()
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var root = Path.GetPathRoot(Path.GetFullPath(home));
            if (string.IsNullOrEmpty(root))
            {
                Console.WriteLine("[--] Disk space: could not be determined.");
                return;
            }

            var drive = new DriveInfo(root);
            var freeGigabytes = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
            Console.WriteLine($"[OK] Disk space: {freeGigabytes:0.0} GB free on {drive.Name}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.WriteLine("[--] Disk space: could not be determined.");
        }
    }

    private static string? InstallHintFor(string? distroId, string libraryName) => (distroId, libraryName) switch
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
