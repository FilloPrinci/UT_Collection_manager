using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using UTLauncher.Core.Download;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Tasks;

namespace UTLauncher.Core.Games;

/// <summary>
/// Installs the Windows-only runtime dependencies UT99/UT2004 need, ported from OldUnreal's
/// Windows/Common.nsh (Visual C++ Redistributable x86/x64, DirectX end-user runtime): skip
/// whatever is already present, run the rest silently.
/// </summary>
public sealed class WindowsDependencyInstaller(Downloader downloader, ILogger<WindowsDependencyInstaller> logger)
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
