using Microsoft.Extensions.Logging;
using UTLauncher.Core.Download;
using UTLauncher.Core.Extraction;
using UTLauncher.Core.InstallRegistry;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Processes;
using UTLauncher.Core.Tasks;
using UTLauncher.Core.Tools;

namespace UTLauncher.Core.Games;

public sealed class Ut2004Installer(
    Downloader downloader,
    Iso9660Extractor isoExtractor,
    ArchiveExtractor archiveExtractor,
    ProcessRunner processRunner,
    ToolManager toolManager,
    SystemLibraryLocator systemLibraryLocator,
    InstallationRegistry registry,
    IPlatform platform,
    ILogger<Ut2004Installer> logger)
{
    // Ported verbatim from OldUnreal/FullGameInstallers @ 103b2b269cd9dd85b5098071aec1d80b5118877e
    // (Linux/src/entrypoints/install-ut2004.sh, UNPACK_IGNORE_PATTERNS): drops the autorun/setup
    // clutter from the ISO, keeping only the DiskN/*.cab (+.hdr) InstallShield payload.
    private static readonly string[] UnpackIgnorePatterns =
    [
        "AutoRunData",
        "Disk1/layout.bin",
        "Disk1/Setup.*",
        "Disk1/setup.*",
        "SoNow",
        "*.*",
    ];

    // Ported from Linux/src/steps/ut2004_install_files.sh: maps each InstallShield component
    // folder (as unshield extracts it) to its destination folder relative to the install root.
    // A null target means "copy straight into the install root".
    private static readonly (string Source, string? Target)[] FoldersAndTargets =
    [
        ("All_Animations", "Animations"),
        ("All_Benchmark", "Benchmark"),
        ("All_ForceFeedback", "ForceFeedback"),
        ("All_Help", "Help"),
        ("All_KarmaData", "KarmaData"),
        ("All_Maps", "Maps"),
        ("All_Music", "Music"),
        ("All_StaticMeshes", "StaticMeshes"),
        ("All_Textures", "Textures"),
        ("All_Web", "Web"),
        ("All_UT2004.EXE", "System"),
        ("English_Manual", "Manual"),
        ("English_Sounds_Speech_System_Help", null),
    ];

    // Ported from ut2004_special_fixes.sh: on a case-sensitive filesystem these wrongly-cased
    // files can end up sitting next to the correctly-cased ones shipped by the patch.
    private static readonly string[] CommonWrongCasings =
    [
        "Bonuspack.u",
        "Gui2K4.u",
        "Gameplay.u",
        "Ipdrv.u",
        "Skaarjpack.u",
        "StreamLineFX.u",
        "UT2K4Assault.u",
        "UT2K4AssaultFull.u",
        "XVoting.u",
        "xWebAdmin.u",
    ];

    // UE_SYSTEM_FOLDER_SUFFIX is empty for UT2004 on x86_64 (Linux/src/lib/architecture.sh):
    // everything lives directly under "System", and the real binary is "System/UT2004"
    // (ut2004-bin / ut2004-bin-amd64 are symlinks to it, verified against the real patch).
    // Windows keeps the same "System" folder (Windows/UT2004.nsi).
    private const string SystemFolderName = "System";
    private const string GameBinaryName = "UT2004";

    private bool IsWindows => platform.Id == "windows";

    public async Task<InstallationRecord> InstallAsync(
        Manifest.Manifest manifest,
        GameEntry game,
        string destination,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        destination = Path.GetFullPath(destination);
        var installerDirectory = Path.Combine(destination, "Installer");
        var stagingDirectory = Path.Combine(installerDirectory, ".staging");
        Directory.CreateDirectory(installerDirectory);

        logger.LogInformation("Installing {GameName} to {Destination}", game.Name, destination);

        var unshieldPath = await toolManager
            .EnsureAvailableAsync(ExternalToolKind.Unshield, manifest, progress, cancellationToken)
            .ConfigureAwait(false);

        var isoResult = await DownloadWithAlternativesAsync(game, "iso", installerDirectory, progress, cancellationToken)
            .ConfigureAwait(false);

        var patchFile = (IsWindows ? game.Patch?.Windows : game.Patch?.LinuxX64)
            ?? throw new InvalidOperationException($"Manifest is missing the {platform.Id} patch for '{game.Id}'.");
        var patchResult = await DownloadPatchAsync(patchFile, installerDirectory, progress, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Extracting game files from {IsoPath} to staging", isoResult.Path);
        await isoExtractor.ExtractAsync(
            isoResult.Path,
            stagingDirectory,
            relativePath => !OldUnrealExclusionMatcher.IsExcluded(relativePath, UnpackIgnorePatterns),
            progress,
            cancellationToken).ConfigureAwait(false);

        var dataDirectory = await UnpackCabsAsync(stagingDirectory, unshieldPath, progress, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Installing extracted files to {Destination}", destination);
        InstallExtractedFiles(dataDirectory, destination, IsWindows);
        Directory.Delete(stagingDirectory, recursive: true);

        logger.LogInformation("Extracting patch {PatchTag} from {PatchPath}", game.Patch?.Tag, patchResult.Path);
        await archiveExtractor.ExtractAsync(patchResult.Path, destination, progress, cancellationToken)
            .ConfigureAwait(false);

        MakeGameBinaryExecutable(destination);
        RemoveWrongCaseFiles(destination);
        await ApplyLibraryPreferenceFixesAsync(destination, cancellationToken).ConfigureAwait(false);
        FixMainMenuClass(Path.Combine(destination, SystemFolderName, "UT2004.ini"));

        var record = new InstallationRecord(
            GameId: game.Id,
            InstallPath: destination,
            VersionCode: game.VersionCode,
            Platform: platform.Id,
            SourceHashes: new Dictionary<string, string>
            {
                ["iso"] = isoResult.Sha256Hex,
                ["patch"] = patchResult.Sha256Hex,
            },
            InstalledAtUtc: DateTimeOffset.UtcNow);

        await registry.UpsertAsync(record, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Installation of {GameName} complete", game.Name);
        return record;
    }

    private async Task<DownloadResult> DownloadWithAlternativesAsync(
        GameEntry game,
        string sourceKey,
        string installerDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!game.Sources.TryGetValue(sourceKey, out var source) || source.FileName is null)
        {
            throw new InvalidOperationException(
                $"Manifest source '{sourceKey}' is missing or incomplete for game '{game.Id}'.");
        }

        var alternatives = source.Alternatives ?? [source];
        var destinationPath = Path.Combine(installerDirectory, source.FileName);

        Exception? lastError = null;
        foreach (var alternative in alternatives)
        {
            if (alternative.Sha256 is null || alternative.Urls is null)
            {
                continue;
            }

            try
            {
                var request = new DownloadRequest(alternative.Urls, destinationPath, alternative.Sha256, alternative.Size);
                return await downloader.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);
            }
            catch (DownloadException ex)
            {
                logger.LogWarning(ex, "Source alternative for '{SourceKey}' failed, trying the next one if available", sourceKey);
                lastError = ex;
            }
        }

        throw new DownloadException($"All source alternatives failed for '{destinationPath}'.", lastError);
    }

    private async Task<DownloadResult> DownloadPatchAsync(
        PatchPlatformFile patchFile,
        string installerDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (patchFile.FileName is null || patchFile.Url is null || patchFile.Sha256 is null)
        {
            throw new InvalidOperationException("Manifest patch entry is missing required fields.");
        }

        var destinationPath = Path.Combine(installerDirectory, patchFile.FileName);
        var request = new DownloadRequest([patchFile.Url], destinationPath, patchFile.Sha256, patchFile.Size);
        return await downloader.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> UnpackCabsAsync(
        string stagingDirectory,
        string unshieldPath,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        var cabsDirectory = Path.Combine(stagingDirectory, "Cabs");
        if (Directory.Exists(cabsDirectory))
        {
            Directory.Delete(cabsDirectory, recursive: true);
        }

        Directory.CreateDirectory(cabsDirectory);

        foreach (var diskDirectory in Directory.EnumerateDirectories(stagingDirectory, "Disk*"))
        {
            foreach (var file in Directory.EnumerateFiles(diskDirectory)
                .Where(f => f.EndsWith(".cab", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase)))
            {
                var linkPath = Path.Combine(cabsDirectory, Path.GetFileName(file));
                File.CreateSymbolicLink(linkPath, file);
            }
        }

        var dataDirectory = Path.Combine(stagingDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);

        var mainCabPath = Path.Combine(cabsDirectory, "data1.cab");
        logger.LogInformation("Unpacking install CABs from {CabPath}", mainCabPath);
        progress?.Report(TaskProgress.Indeterminate("Unpacking install CABs"));

        var result = await processRunner.RunAsync(
            unshieldPath,
            ["-d", dataDirectory, "x", mainCabPath],
            workingDirectory: null,
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"unshield failed to extract '{mainCabPath}' (exit code {result.ExitCode}).");
        }

        Directory.Delete(cabsDirectory, recursive: true);

        // Free the raw Disk1-5 payload (equal in size to the ISO itself) now that unshield has
        // extracted everything from it - it would otherwise coexist with the extracted Data
        // folder and the final destination copy, which the UT2004 dataset is large enough to
        // make a real disk space concern.
        foreach (var diskDirectory in Directory.EnumerateDirectories(stagingDirectory, "Disk*"))
        {
            Directory.Delete(diskDirectory, recursive: true);
        }

        return dataDirectory;
    }

    private static void InstallExtractedFiles(string dataDirectory, string destination, bool isWindows)
    {
        // Windows-only content that isn't needed on Linux (the real Linux binary comes from
        // the patch, extracted separately). On Windows these are exactly the files the game runs,
        // so they must be kept.
        if (!isWindows)
        {
            DeleteFilesWithExtension(Path.Combine(dataDirectory, "All_UT2004.EXE"), ".exe");
            var soundsSystemDirectory = Path.Combine(dataDirectory, "English_Sounds_Speech_System_Help", "System");
            DeleteFilesWithExtension(soundsSystemDirectory, ".bat");
            DeleteFilesWithExtension(soundsSystemDirectory, ".dll");
            DeleteFilesWithExtension(soundsSystemDirectory, ".exe");
        }

        foreach (var (source, target) in FoldersAndTargets)
        {
            var sourceDirectory = Path.Combine(dataDirectory, source);
            if (!Directory.Exists(sourceDirectory))
            {
                continue;
            }

            var resolvedTarget = target is null ? destination : Path.Combine(destination, target);
            Directory.CreateDirectory(resolvedTarget);
            MoveDirectoryContents(sourceDirectory, resolvedTarget);
        }

        // The backup ISO alternative's data doesn't carry the US_License file group.
        var backupLicenseDirectory = Path.Combine(dataDirectory, "US_License.int");
        if (Directory.Exists(backupLicenseDirectory))
        {
            var systemDirectory = Path.Combine(destination, "System");
            Directory.CreateDirectory(systemDirectory);
            MoveDirectoryContents(backupLicenseDirectory, systemDirectory);
        }
    }

    private static void DeleteFilesWithExtension(string directory, string extension)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*" + extension))
        {
            File.Delete(file);
        }
    }

    // Moves rather than copies: the staging directory is always a subfolder of destination
    // (same filesystem), the staging files are deleted right after this runs anyway, and the
    // UT2004 data set is large enough that copying would transiently double disk usage.
    private static void MoveDirectoryContents(string sourceDirectory, string destinationDirectory)
    {
        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            var destinationFileDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationFileDirectory))
            {
                Directory.CreateDirectory(destinationFileDirectory);
            }

            File.Move(filePath, destinationPath, overwrite: true);
        }
    }

    private static void MakeGameBinaryExecutable(string destination)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // SharpCompress does not restore Unix permissions from tar entries (see ArchiveExtractor),
        // and File.Copy doesn't preserve them either, so the exec bit is set explicitly here.
        var path = Path.Combine(destination, SystemFolderName, GameBinaryName);
        if (!File.Exists(path))
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    private static void RemoveWrongCaseFiles(string destination)
    {
        var systemDirectory = Path.Combine(destination, SystemFolderName);
        foreach (var wrongCasing in CommonWrongCasings)
        {
            var path = Path.Combine(systemDirectory, wrongCasing);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private async Task ApplyLibraryPreferenceFixesAsync(string destination, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var systemDirectory = Path.Combine(destination, SystemFolderName);

        await PreferSystemLibraryAsync(systemDirectory, "libopenal.so.1", cancellationToken).ConfigureAwait(false);
        await PreferSystemLibraryAsync(systemDirectory, "libSDL3.so.0", cancellationToken).ConfigureAwait(false);
        await ApplyLibompFixAsync(systemDirectory, cancellationToken).ConfigureAwait(false);
    }

    private async Task PreferSystemLibraryAsync(string systemDirectory, string bundledFileName, CancellationToken cancellationToken)
    {
        var bundledPath = Path.Combine(systemDirectory, bundledFileName);
        if (!File.Exists(bundledPath))
        {
            return;
        }

        var systemPath = await systemLibraryLocator.FindAsync(bundledFileName, cancellationToken).ConfigureAwait(false);
        if (systemPath is null)
        {
            return;
        }

        logger.LogInformation("System already provides {Library} ({Path}), removing the bundled copy", bundledFileName, systemPath);
        foreach (var file in Directory.EnumerateFiles(systemDirectory, bundledFileName + "*"))
        {
            File.Delete(file);
        }
    }

    private async Task ApplyLibompFixAsync(string systemDirectory, CancellationToken cancellationToken)
    {
        var systemLibompPath = await systemLibraryLocator.FindAsync("libomp.so.5", cancellationToken).ConfigureAwait(false);
        var requiresSymlink = false;

        if (systemLibompPath is null)
        {
            systemLibompPath = await systemLibraryLocator.FindAsync("libomp.so", cancellationToken).ConfigureAwait(false);
            requiresSymlink = systemLibompPath is not null;
        }

        var bundledPath = Path.Combine(systemDirectory, "libomp.so.5");
        if (File.Exists(bundledPath) && systemLibompPath is not null)
        {
            logger.LogInformation("System already provides libomp ({Path}), removing the bundled copy", systemLibompPath);
            foreach (var file in Directory.EnumerateFiles(systemDirectory, "libomp.so.5*"))
            {
                File.Delete(file);
            }
        }

        if (requiresSymlink && systemLibompPath is not null)
        {
            var linkPath = Path.Combine(systemDirectory, "libomp.so.5");
            if (!File.Exists(linkPath))
            {
                File.CreateSymbolicLink(linkPath, systemLibompPath);
            }
        }
    }

    private static void FixMainMenuClass(string iniPath)
    {
        const string OldLine = "MainMenuClass=GUI2K4.UT2K4MainMenu";
        const string NewLine = "MainMenuClass=GUI2K4.UT2K4MainMenuWS";

        if (!File.Exists(iniPath))
        {
            return;
        }

        var lines = File.ReadAllLines(iniPath);
        var changed = false;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i] == OldLine)
            {
                lines[i] = NewLine;
                changed = true;
            }
        }

        if (changed)
        {
            File.WriteAllLines(iniPath, lines);
        }
    }
}
