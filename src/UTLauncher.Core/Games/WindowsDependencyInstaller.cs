using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using UTLauncher.Core.Download;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Processes;
using UTLauncher.Core.Tasks;

namespace UTLauncher.Core.Games;

/// <summary>
/// Installs the Windows-only runtime dependencies UT99/UT2004/UT4 need: for UT99/UT2004, ported
/// from OldUnreal's Windows/Common.nsh (Visual C++ Redistributable x86/x64, DirectX end-user
/// runtime); for UT4, the legacy DirectX June 2010 redistributable and VC++ 2013 x64 (SPEC.md
/// §6.3). Skips whatever is already present, runs the rest silently.
/// </summary>
public sealed class WindowsDependencyInstaller(
    Downloader downloader,
    ProcessRunner processRunner,
    ILogger<WindowsDependencyInstaller> logger)
{
    public async Task EnsureInstalledAsync(
        GameEntry game,
        string installerDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var windows = game.Dependencies?.Windows;
        if (windows is null)
        {
            return;
        }

        if (windows.VcRedistX86 is { } vcRedistX86)
        {
            await EnsureVcRedistAsync(vcRedistX86, installerDirectory, progress, cancellationToken).ConfigureAwait(false);
        }

        if (windows.VcRedistX64 is { } vcRedistX64)
        {
            await EnsureVcRedistAsync(vcRedistX64, installerDirectory, progress, cancellationToken).ConfigureAwait(false);
        }

        if (windows.DirectXWebSetup is { } directXWebSetup)
        {
            // No presence check here: OldUnreal's script always runs dxwebsetup.exe /q
            // unconditionally, and it is itself a fast no-op when nothing needs updating.
            var installerPath = await DownloadAsync(
                directXWebSetup.FileName, directXWebSetup.Url, directXWebSetup.Sha256, directXWebSetup.Size,
                installerDirectory, progress, cancellationToken).ConfigureAwait(false);
            await RunElevatedInstallerAsync(installerPath, directXWebSetup.InstallArgs, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        if (windows.DirectxJune2010 is { } directXJune2010)
        {
            await EnsureDirectXJune2010Async(directXJune2010, installerDirectory, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        if (windows.Vcredist2013X64 is { } vcredist2013X64)
        {
            await EnsureVcRedist2013Async(vcredist2013X64, installerDirectory, progress, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task EnsureVcRedistAsync(
        VcRedistInstaller dependency,
        string installerDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (IsVcRedistInstalled(dependency))
        {
            logger.LogInformation("{FileName} is already installed, skipping", dependency.FileName);
            return;
        }

        var installerPath = await DownloadAsync(
            dependency.FileName, dependency.Url, dependency.Sha256, dependency.Size,
            installerDirectory, progress, cancellationToken).ConfigureAwait(false);
        await RunElevatedInstallerAsync(installerPath, dependency.InstallArgs, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    private bool IsVcRedistInstalled(VcRedistInstaller dependency)
    {
        if (!OperatingSystem.IsWindows() || dependency.RegistryKeyHklm is null || dependency.RegistryValueName is null)
        {
            return false;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(dependency.RegistryKeyHklm);
            return key?.GetValue(dependency.RegistryValueName) is int value && value == 1;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            logger.LogWarning(ex, "Could not read registry key {KeyPath}, assuming not installed", dependency.RegistryKeyHklm);
            return false;
        }
    }

    private async Task EnsureVcRedist2013Async(
        VcRedistDependency dependency,
        string installerDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (IsVcRedist2013Installed(dependency))
        {
            logger.LogInformation("{FileName} is already installed, skipping", dependency.FileName);
            return;
        }

        var installerPath = await DownloadAsync(
            dependency.FileName, dependency.Url, dependency.Sha256, dependency.Size,
            installerDirectory, progress, cancellationToken).ConfigureAwait(false);
        await RunElevatedInstallerAsync(installerPath, dependency.InstallArgs, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    private bool IsVcRedist2013Installed(VcRedistDependency dependency)
    {
        if (!OperatingSystem.IsWindows() || dependency.RegistryKeysHklm is null || dependency.RegistryKeysHklm.Count == 0)
        {
            return false;
        }

        try
        {
            foreach (var keyPath in dependency.RegistryKeysHklm)
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                if (key is null)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            logger.LogWarning(ex, "Could not read one of the VC++ 2013 registry keys, assuming not installed");
            return false;
        }
    }

    // The offline DirectX June 2010 redistributable is a self-extracting archive, not a plain
    // installer: "/Q /T:<dir>" unpacks it (no elevation needed - it just writes to a temp folder),
    // then the extracted DXSETUP.exe actually installs the components ("/silent", elevated, same
    // as the other dependencies here). This is the documented Microsoft procedure for a silent
    // DirectX install from a game installer.
    private async Task EnsureDirectXJune2010Async(
        DirectXDependency dependency,
        string installerDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (IsDirectXJune2010Installed(dependency))
        {
            logger.LogInformation("DirectX June 2010 components are already installed, skipping");
            return;
        }

        var installerPath = await DownloadAsync(
            dependency.FileName, dependency.Url, dependency.Sha256, dependency.Size,
            installerDirectory, progress, cancellationToken).ConfigureAwait(false);

        var extractDirectory = Path.Combine(installerDirectory, "DirectXJune2010");
        Directory.CreateDirectory(extractDirectory);

        logger.LogInformation("Extracting {InstallerPath} to {ExtractDirectory}", installerPath, extractDirectory);
        progress?.Report(TaskProgress.Indeterminate("Extracting DirectX components"));

        var extractResult = await processRunner.RunAsync(
            installerPath,
            ["/Q", $"/T:{extractDirectory}"],
            workingDirectory: installerDirectory,
            cancellationToken).ConfigureAwait(false);

        if (!extractResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to extract DirectX June 2010 redistributable (exit code {extractResult.ExitCode}).");
        }

        var dxSetupPath = Path.Combine(extractDirectory, "DXSETUP.exe");
        if (!File.Exists(dxSetupPath))
        {
            throw new InvalidOperationException($"DXSETUP.exe not found after extraction at '{dxSetupPath}'.");
        }

        await RunElevatedInstallerAsync(dxSetupPath, "/silent", progress, cancellationToken).ConfigureAwait(false);
    }

    private bool IsDirectXJune2010Installed(DirectXDependency dependency)
    {
        if (!OperatingSystem.IsWindows() || dependency.CheckFilesInSystem32 is null || dependency.CheckFilesInSystem32.Count == 0)
        {
            return false;
        }

        var system32 = Environment.SystemDirectory;
        return dependency.CheckFilesInSystem32.All(fileName => File.Exists(Path.Combine(system32, fileName)));
    }

    private async Task<string> DownloadAsync(
        string? fileName,
        string? url,
        string? sha256,
        long? size,
        string installerDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (fileName is null || url is null || sha256 is null)
        {
            throw new InvalidOperationException("Manifest Windows dependency entry is missing required fields.");
        }

        var destinationPath = Path.Combine(installerDirectory, fileName);
        var request = new DownloadRequest([url], destinationPath, sha256, size);
        var result = await downloader.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);
        return result.Path;
    }

    // Can't go through the shared ProcessRunner here: these installers request administrator
    // elevation via their own embedded manifest, which Windows only honors when launched through
    // the shell (UseShellExecute = true, Verb = "runas") - and ShellExecute-launched processes
    // cannot have their stdout/stderr redirected, so only the command and exit code are logged.
    private async Task RunElevatedInstallerAsync(
        string installerPath,
        string? installArgs,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(installerPath);
        var arguments = installArgs ?? string.Empty;

        logger.LogInformation("Running elevated installer: {InstallerPath} {Arguments}", installerPath, arguments);
        progress?.Report(TaskProgress.Indeterminate($"Installing {fileName}"));

        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(installerPath) ?? string.Empty,
            UseShellExecute = true,
            Verb = "runas",
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException(
                $"'{fileName}' requires administrator approval to install, which was declined.", ex);
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Process {FileName} exited with code {ExitCode}", fileName, process.ExitCode);

        // 3010 = success, reboot required (both vc_redist and dxwebsetup follow this convention).
        if (process.ExitCode != 0 && process.ExitCode != 3010)
        {
            throw new InvalidOperationException($"'{fileName}' failed to install (exit code {process.ExitCode}).");
        }
    }
}
