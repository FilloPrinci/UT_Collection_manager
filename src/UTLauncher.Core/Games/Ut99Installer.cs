using Microsoft.Extensions.Logging;
using UTLauncher.Core.Download;
using UTLauncher.Core.Extraction;
using UTLauncher.Core.InstallRegistry;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Processes;
using UTLauncher.Core.Tasks;

namespace UTLauncher.Core.Games;

public sealed class Ut99Installer(
    Downloader downloader,
    Iso9660Extractor isoExtractor,
    ArchiveExtractor archiveExtractor,
    ProcessRunner processRunner,
    InstallationRegistry registry,
    IPlatform platform,
    ILogger<Ut99Installer> logger)
{
    // Ported verbatim from OldUnreal/FullGameInstallers @ 103b2b269cd9dd85b5098071aec1d80b5118877e
    // (Linux/src/entrypoints/install-ut99.sh, UNPACK_IGNORE_PATTERNS). These files are skipped when
    // extracting the original CD ISO because the patch step right after replaces them anyway.
    private static readonly string[] UnpackIgnorePatterns =
    [
        "System/UnrealTournament.ini",
        "System/User.ini",
        "System/*.bat",
        "System/*.dll",
        "System/*.exe",
        "Autorun.inf",
        "Setup.exe",
        "DirectX7",
        "GameSpy",
        "Microsoft",
        "NetGamesUSA.com",
        "System400",
        "System/*.ctt",
        "System/*.det",
        "System/*.elt",
        "System/*.est",
        "System/*.frt",
        "System/*.int",
        "System/*.itt",
        "System/*.nlt",
        "System/*.ptt",
        "System/*.rut",
    ];

    // x86_64 only for now (matches the manifest's "linux-x64" platform and OldUnreal's
    // Linux/src/lib/architecture.sh, which resolves UE_SYSTEM_FOLDER_SUFFIX="64" and
    // ARCHITECTURE_BINARY_SUFFIX="-amd64" for UnrealTournament on x86_64).
    private const string SystemFolderName = "System64";
    private const string UccBinaryName = "ucc-bin-amd64";
    private const string GameBinaryName = "ut-bin-amd64";

    public async Task<InstallationRecord> InstallAsync(
        GameEntry game,
        string destination,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        destination = Path.GetFullPath(destination);
        var installerDirectory = Path.Combine(destination, "Installer");
        Directory.CreateDirectory(installerDirectory);

        logger.LogInformation("Installing {GameName} to {Destination}", game.Name, destination);

        var isoResult = await DownloadSourceAsync(game, "iso", installerDirectory, progress, cancellationToken)
            .ConfigureAwait(false);
        var bonusPackResult = await DownloadSourceAsync(game, "bonusPack4", installerDirectory, progress, cancellationToken)
            .ConfigureAwait(false);

        var patchFile = game.Patch?.LinuxX64
            ?? throw new InvalidOperationException($"Manifest is missing the Linux patch for '{game.Id}'.");
        var patchResult = await DownloadPatchAsync(patchFile, installerDirectory, progress, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Extracting game files from {IsoPath}", isoResult.Path);
        await isoExtractor.ExtractAsync(
            isoResult.Path,
            destination,
            relativePath => !OldUnrealExclusionMatcher.IsExcluded(relativePath, UnpackIgnorePatterns),
            progress,
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Extracting Bonus Pack 4 from {BonusPackPath}", bonusPackResult.Path);
        await archiveExtractor.ExtractAsync(bonusPackResult.Path, destination, progress, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Extracting patch {PatchTag} from {PatchPath}", game.Patch?.Tag, patchResult.Path);
        await archiveExtractor.ExtractAsync(patchResult.Path, destination, progress, cancellationToken)
            .ConfigureAwait(false);

        MakeGameBinariesExecutable(destination);

        await UnpackCompressedMapsAsync(destination, progress, cancellationToken).ConfigureAwait(false);

        ApplySpecialFixes(destination);

        var record = new InstallationRecord(
            GameId: game.Id,
            InstallPath: destination,
            VersionCode: game.VersionCode,
            Platform: platform.Id,
            SourceHashes: new Dictionary<string, string>
            {
                ["iso"] = isoResult.Sha256Hex,
                ["bonusPack4"] = bonusPackResult.Sha256Hex,
                ["patch"] = patchResult.Sha256Hex,
            },
            InstalledAtUtc: DateTimeOffset.UtcNow);

        await registry.UpsertAsync(record, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Installation of {GameName} complete", game.Name);
        return record;
    }

    private async Task<DownloadResult> DownloadSourceAsync(
        GameEntry game,
        string sourceKey,
        string installerDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!game.Sources.TryGetValue(sourceKey, out var source) ||
            source.FileName is null || source.Sha256 is null || source.Urls is null)
        {
            throw new InvalidOperationException(
                $"Manifest source '{sourceKey}' is missing or incomplete for game '{game.Id}'.");
        }

        var destinationPath = Path.Combine(installerDirectory, source.FileName);
        var request = new DownloadRequest(source.Urls, destinationPath, source.Sha256, source.Size);
        return await downloader.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DownloadResult> DownloadPatchAsync(
        PatchPlatformFile patchFile,
        string installerDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (patchFile.FileName is null || patchFile.Url is null || patchFile.Sha256 is null)
        {
            throw new InvalidOperationException("Manifest Linux patch entry is missing required fields.");
        }

        var destinationPath = Path.Combine(installerDirectory, patchFile.FileName);
        var request = new DownloadRequest([patchFile.Url], destinationPath, patchFile.Sha256, patchFile.Size);
        return await downloader.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);
    }

    private void MakeGameBinariesExecutable(string destination)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var systemDirectory = Path.Combine(destination, SystemFolderName);

        // Only the two real binaries need the exec bit: SharpCompress does not restore Unix
        // permissions from tar entries, and chmod on the ut-bin/ucc-bin symlinks (created during
        // patch extraction) already follows through to these same targets.
        foreach (var binaryName in new[] { UccBinaryName, GameBinaryName })
        {
            var path = Path.Combine(systemDirectory, binaryName);
            if (!File.Exists(path))
            {
                continue;
            }

            var mode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
    }

    private async Task UnpackCompressedMapsAsync(
        string destination,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        var mapsDirectory = Path.Combine(destination, "Maps");
        if (!Directory.Exists(mapsDirectory))
        {
            return;
        }

        var systemDirectory = Path.Combine(destination, SystemFolderName);
        var uccBinaryPath = Path.Combine(systemDirectory, UccBinaryName);
        var compressedMaps = Directory.EnumerateFiles(mapsDirectory)
            .Where(f => f.EndsWith(".unr.uz", StringComparison.OrdinalIgnoreCase))
            .ToList();

        logger.LogInformation("Decompressing {Count} map(s)", compressedMaps.Count);

        for (var i = 0; i < compressedMaps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var compressedPath = compressedMaps[i];
            var compressedFileName = Path.GetFileName(compressedPath);
            var uncompressedName = Path.GetFileNameWithoutExtension(compressedPath); // strips ".uz" only

            progress?.Report(TaskProgress.Determinate($"Decompressing {uncompressedName}", i + 1, compressedMaps.Count));

            var decompressTargetPath = Path.Combine(mapsDirectory, uncompressedName);
            if (File.Exists(decompressTargetPath))
            {
                File.Delete(compressedPath);
                continue;
            }

            var result = await processRunner.RunAsync(
                uccBinaryPath,
                ["decompress", $"../Maps/{compressedFileName}", "-nohomedir"],
                systemDirectory,
                cancellationToken).ConfigureAwait(false);

            var decompressStagingPath = Path.Combine(systemDirectory, uncompressedName);
            if (!result.Succeeded || !File.Exists(decompressStagingPath))
            {
                throw new InvalidOperationException(
                    $"Failed to decompress map '{compressedFileName}' (ucc-bin exit code {result.ExitCode}).");
            }

            File.Move(decompressStagingPath, decompressTargetPath, overwrite: true);
            File.Delete(compressedPath);
        }
    }

    private static void ApplySpecialFixes(string destination)
    {
        // Ported from OldUnreal's steps/ut99_special_fixes.sh: DM-Cybrosis][ is both a DM and a
        // DOM map on the original CD, but only shipped under its DM name.
        var mapsDirectory = Path.Combine(destination, "Maps");
        var dmCybrosisPath = Path.Combine(mapsDirectory, "DM-Cybrosis][.unr");
        var domCybrosisPath = Path.Combine(mapsDirectory, "DOM-Cybrosis][.unr");

        if (File.Exists(dmCybrosisPath) && !File.Exists(domCybrosisPath))
        {
            File.Copy(dmCybrosisPath, domCybrosisPath);
        }
    }
}
