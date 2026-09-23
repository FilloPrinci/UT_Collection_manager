using System.IO.Compression;
using Microsoft.Extensions.Logging;
using UTLauncher.Core.Download;
using UTLauncher.Core.Hashing;
using UTLauncher.Core.InstallRegistry;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Tasks;

namespace UTLauncher.Core.Games;

/// <summary>
/// Installs UT4 (2017 pre-alpha, UT4ever build v1.1.0) on Windows, per SPEC.md §6.3. Logic
/// derived from utshakka/ut4installer's documented behavior, reimplemented from the spec (that
/// repo has no explicit license: no code is copied from it).
/// </summary>
public sealed class Ut4Installer(
    Downloader downloader,
    WindowsDependencyInstaller windowsDependencyInstaller,
    InstallationRegistry registry,
    IPlatform platform,
    ILogger<Ut4Installer> logger)
{
    private const string InstallInfoSourceLocation = @"C:\Generic\Install\Path";

    public async Task<InstallationRecord> InstallAsync(
        GameEntry game,
        string destination,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("UT4 can currently only be installed on Windows.");
        }

        destination = Path.GetFullPath(destination);
        ValidateDestination(destination);
        EnsureEnoughDiskSpace(game, destination);

        // Staged next to the destination rather than inside it: the destination must not exist
        // yet (ValidateDestination), and gets created only by extracting the game zip into it.
        var installerDirectory = Path.Combine(Path.GetDirectoryName(destination)!, ".ut4-installer");
        Directory.CreateDirectory(installerDirectory);

        logger.LogInformation("Installing {GameName} to {Destination}", game.Name, destination);

        var packageResult = await DownloadSourceAsync(game, "package", installerDirectory, progress, cancellationToken)
            .ConfigureAwait(false);

        var gameZipPath = await ExtractInnerGameZipAsync(game, packageResult.Path, installerDirectory, progress, cancellationToken)
            .ConfigureAwait(false);

        var parentDirectory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException($"Could not determine the parent folder of '{destination}'.");
        Directory.CreateDirectory(parentDirectory);

        logger.LogInformation("Extracting {GameZipPath} to {ParentDirectory}", gameZipPath, parentDirectory);
        await ExtractZipAsync(gameZipPath, parentDirectory, progress, cancellationToken).ConfigureAwait(false);

        // Frees ~10 GB now that it's been fully extracted: unlike the downloaded "package" (kept
        // for resumability), this file is cheap to regenerate from it if ever needed again, and
        // the UT4 install is large enough that disk space is a real concern (SPEC.md §6.3 step 1).
        File.Delete(gameZipPath);

        WriteEngineIni(game, destination);
        WriteInstallInfo(game, destination);

        await windowsDependencyInstaller.EnsureInstalledAsync(game, installerDirectory, progress, cancellationToken)
            .ConfigureAwait(false);

        var record = new InstallationRecord(
            GameId: game.Id,
            InstallPath: destination,
            VersionCode: game.VersionCode,
            Platform: platform.Id,
            SourceHashes: new Dictionary<string, string>
            {
                ["package"] = packageResult.Sha256Hex,
            },
            InstalledAtUtc: DateTimeOffset.UtcNow);

        await registry.UpsertAsync(record, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Installation of {GameName} complete", game.Name);
        return record;
    }

    // SPEC.md §6.3 step 2: the game zip's own top-level folder is already "UnrealTournament", so
    // the destination must end in that name and not already exist - it gets extracted into the
    // *parent* folder.
    private static void ValidateDestination(string destination)
    {
        if (!string.Equals(Path.GetFileName(destination), "UnrealTournament", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The UT4 install path must end in 'UnrealTournament' (got '{destination}').");
        }

        if (Directory.Exists(destination))
        {
            throw new InvalidOperationException($"'{destination}' already exists.");
        }
    }

    private static void EnsureEnoughDiskSpace(GameEntry game, string destination)
    {
        if (game.DiskSpaceRequiredBytes is not { } requiredBytes)
        {
            return;
        }

        var root = Path.GetPathRoot(destination);
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        var drive = new DriveInfo(root);
        if (drive.AvailableFreeSpace < requiredBytes)
        {
            var requiredGb = requiredBytes / 1024.0 / 1024.0 / 1024.0;
            var freeGb = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
            throw new InvalidOperationException(
                $"Not enough free space on {drive.Name}: {requiredGb:0.0} GB required, {freeGb:0.0} GB available.");
        }
    }

    private async Task<DownloadResult> DownloadSourceAsync(
        GameEntry game,
        string sourceKey,
        string installerDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!game.Sources.TryGetValue(sourceKey, out var source) ||
            source.FileName is null || source.Sha256 is null || source.Urls is null || source.Urls.Count == 0)
        {
            throw new InvalidOperationException(
                $"Manifest source '{sourceKey}' is missing or incomplete for game '{game.Id}'.");
        }

        var destinationPath = Path.Combine(installerDirectory, source.FileName);
        var request = new DownloadRequest(source.Urls, destinationPath, source.Sha256, source.Size);
        return await downloader.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);
    }

    // The "package" zip is the full ~10 GB UT4ever installer bundle; the actual game data is one
    // entry inside it (source "game" in the manifest, named by "innerEntry"). Extracting just
    // that one entry avoids unpacking the rest of the package.
    private async Task<string> ExtractInnerGameZipAsync(
        GameEntry game,
        string packagePath,
        string installerDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!game.Sources.TryGetValue("package", out var packageSource) || packageSource.InnerEntry is null)
        {
            throw new InvalidOperationException("Manifest source 'package' is missing its innerEntry for game 'ut4'.");
        }

        if (!game.Sources.TryGetValue("game", out var gameSource) || gameSource.FileName is null || gameSource.Sha256 is null)
        {
            throw new InvalidOperationException("Manifest source 'game' is missing or incomplete for game 'ut4'.");
        }

        var gameZipPath = Path.Combine(installerDirectory, gameSource.FileName);
        if (File.Exists(gameZipPath))
        {
            var existingHash = await HashCalculator.ComputeFileAsync(gameZipPath, progress: null, cancellationToken)
                .ConfigureAwait(false);
            if (HashCalculator.Matches(existingHash.Sha256Hex, gameSource.Sha256))
            {
                logger.LogInformation("File already present and verified: {Path}", gameZipPath);
                return gameZipPath;
            }

            File.Delete(gameZipPath);
        }

        using var packageArchive = ZipFile.OpenRead(packagePath);
        var innerZipEntry = packageArchive.GetEntry(packageSource.InnerEntry)
            ?? throw new InvalidOperationException($"Entry '{packageSource.InnerEntry}' not found inside '{packagePath}'.");

        logger.LogInformation("Extracting {InnerEntry} from {PackagePath}", packageSource.InnerEntry, packagePath);

        var partPath = gameZipPath + ".part";
        await using (var entryStream = innerZipEntry.Open())
        await using (var outputStream = File.Create(partPath))
        {
            await CopyWithProgressAsync(
                entryStream, outputStream, innerZipEntry.Length, "Extracting game package", progress, cancellationToken)
                .ConfigureAwait(false);
        }

        var extractedHash = await HashCalculator.ComputeFileAsync(partPath, progress: null, cancellationToken)
            .ConfigureAwait(false);
        if (!HashCalculator.Matches(extractedHash.Sha256Hex, gameSource.Sha256))
        {
            File.Delete(partPath);
            throw new HashMismatchException(
                $"Hash mismatch for '{gameSource.FileName}' extracted from '{packagePath}'.",
                gameSource.Sha256,
                extractedHash.Sha256Hex);
        }

        File.Move(partPath, gameZipPath, overwrite: true);
        logger.LogInformation("Extraction completed and verified: {Path} ({Size} bytes)", gameZipPath, extractedHash.Size);
        return gameZipPath;
    }

    private static async Task CopyWithProgressAsync(
        Stream source,
        Stream destination,
        long totalBytes,
        string stepText,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 81920;
        var buffer = new byte[bufferSize];
        long copied = 0;
        int read;

        while ((read = await source.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            copied += read;
            progress?.Report(totalBytes > 0
                ? TaskProgress.Determinate(stepText, copied, totalBytes)
                : TaskProgress.Indeterminate(stepText));
        }
    }

    // System.IO.Compression rather than SharpCompress (SPEC.md §6.3 step 3): its Zip64 support is
    // needed for a ~10 GB archive, and it's already a BCL dependency with no extra package needed.
    private static async Task ExtractZipAsync(
        string zipPath,
        string destinationDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();

        for (var i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = entries[i];
            var destinationPath = Path.Combine(destinationDirectory, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            var destinationEntryDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationEntryDirectory))
            {
                Directory.CreateDirectory(destinationEntryDirectory);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
            progress?.Report(TaskProgress.Determinate($"Extracting {entry.Name}", i + 1, entries.Count));
        }
    }

    // SPEC.md §6.3 step 4: create Engine.ini with the manifest's master-server sections, or add
    // only whichever ones are missing if the file already exists. Always under the user's
    // Documents folder, regardless of where the game itself is installed (UE4 convention).
    private void WriteEngineIni(GameEntry game, string destination)
    {
        if (game.MasterServer is not { } masterServer)
        {
            throw new InvalidOperationException($"Manifest is missing 'masterServer' for game '{game.Id}'.");
        }

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var enginePath = Path.Combine(
            documentsPath, "UnrealTournament", "Saved", "Config", "WindowsNoEditor", "Engine.ini");

        Directory.CreateDirectory(Path.GetDirectoryName(enginePath)!);

        var existingContent = File.Exists(enginePath) ? File.ReadAllText(enginePath) : null;
        var mergedContent = EngineIniMerger.Merge(existingContent, masterServer);

        if (mergedContent != existingContent)
        {
            File.WriteAllText(enginePath, mergedContent);
            logger.LogInformation("Wrote {EnginePath}", enginePath);
        }
    }

    // SPEC.md §6.3 step 5: rewrite UT4UU's InstallInfo.bin (both manifest paths, relative to the
    // install destination) with only sourceLocation/installLocation replaced.
    private void WriteInstallInfo(GameEntry game, string destination)
    {
        if (game.Ut4uuInstallInfo is not { Count: > 0 } paths)
        {
            throw new InvalidOperationException($"Manifest is missing 'ut4uuInstallInfo' for game '{game.Id}'.");
        }

        foreach (var relativePath in paths)
        {
            var path = Path.Combine(destination, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                logger.LogWarning("InstallInfo.bin not found at {Path}, skipping", path);
                continue;
            }

            InstallInfoBinCodec.RewriteLocations(path, InstallInfoSourceLocation, destination);
            logger.LogInformation("Wrote {Path}", path);
        }
    }
}
