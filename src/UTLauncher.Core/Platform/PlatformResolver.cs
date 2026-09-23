using System.Runtime.InteropServices;

namespace UTLauncher.Core.Platform;

public static class PlatformResolver
{
    public static IPlatform Resolve(IEnvironmentInfo environment)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsPlatform(environment);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxPlatform(environment);
        }

        throw new PlatformNotSupportedException($"Piattaforma non supportata: {RuntimeInformation.OSDescription}");
    }
}
