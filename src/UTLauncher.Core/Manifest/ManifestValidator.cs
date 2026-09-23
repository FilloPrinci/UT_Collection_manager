using System.Text.RegularExpressions;

namespace UTLauncher.Core.Manifest;

public static partial class ManifestValidator
{
    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Sha256Pattern();

    public static ManifestValidationResult Validate(Manifest manifest)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (manifest.ManifestVersion < 1)
        {
            errors.Add("manifestVersion must be >= 1.");
        }

        if (manifest.Games.Count == 0)
        {
            errors.Add("The manifest does not contain any games.");
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in manifest.Games)
        {
            if (string.IsNullOrWhiteSpace(game.Id))
            {
                errors.Add("Found a game with a missing or empty id.");
                continue;
            }

            if (!seenIds.Add(game.Id))
            {
                errors.Add($"Duplicate game id: '{game.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(game.Name))
            {
                errors.Add($"games[{game.Id}]: missing name.");
            }

            if (string.IsNullOrWhiteSpace(game.VersionCode))
            {
                errors.Add($"games[{game.Id}]: missing versionCode.");
            }

            foreach (var (sourceName, source) in game.Sources)
            {
                ValidateSourceFile(source, $"games[{game.Id}].sources.{sourceName}", errors, warnings);
            }

            ValidatePatchPlatform(game.Patch?.Windows, $"games[{game.Id}].patch.windows", errors, warnings);
            ValidatePatchPlatform(game.Patch?.LinuxX64, $"games[{game.Id}].patch.linux-x64", errors, warnings);

            var windowsDependencies = game.Dependencies?.Windows;
            if (windowsDependencies?.VcRedistX86 is { } vcRedistX86)
            {
                ValidateHash(vcRedistX86.Sha256, $"games[{game.Id}].dependencies.windows.vcRedistX86", errors, warnings);
            }

            if (windowsDependencies?.VcRedistX64 is { } vcRedistX64)
            {
                ValidateHash(vcRedistX64.Sha256, $"games[{game.Id}].dependencies.windows.vcRedistX64", errors, warnings);
            }

            if (windowsDependencies?.DirectXWebSetup is { } directXWebSetup)
            {
                ValidateHash(directXWebSetup.Sha256, $"games[{game.Id}].dependencies.windows.directXWebSetup", errors, warnings);
            }

            if (windowsDependencies?.DirectxJune2010 is { } directXJune2010)
            {
                ValidateHash(directXJune2010.Sha256, $"games[{game.Id}].dependencies.windows.directxJune2010", errors, warnings);
            }

            if (windowsDependencies?.Vcredist2013X64 is { } vcredist2013X64)
            {
                ValidateHash(vcredist2013X64.Sha256, $"games[{game.Id}].dependencies.windows.vcredist2013x64", errors, warnings);
            }
        }

        ValidateToolEntry(manifest.Tools?.Unshield, "tools.unshield", errors, warnings);
        ValidateToolEntry(manifest.Tools?.Umu, "tools.umu", errors, warnings);
        ValidateToolEntry(manifest.Tools?.Proton, "tools.proton", errors, warnings);

        return new ManifestValidationResult(errors, warnings);
    }

    private static void ValidateSourceFile(SourceFile source, string path, List<string> errors, List<string> warnings)
    {
        if (source.Alternatives is not null)
        {
            for (var i = 0; i < source.Alternatives.Count; i++)
            {
                ValidateSourceFile(source.Alternatives[i], $"{path}.alternatives[{i}]", errors, warnings);
            }

            return;
        }

        ValidateHash(source.Sha256, path, errors, warnings);

        if (source.Urls is null || source.Urls.Count == 0)
        {
            warnings.Add($"{path}: no URL available (only acceptable if intended for local import).");
        }
    }

    private static void ValidatePatchPlatform(PatchPlatformFile? patch, string path, List<string> errors, List<string> warnings)
    {
        if (patch is null)
        {
            return;
        }

        ValidateHash(patch.Sha256, path, errors, warnings);
    }

    private static void ValidateToolEntry(ToolEntry? tool, string path, List<string> errors, List<string> warnings)
    {
        if (tool is null)
        {
            warnings.Add($"{path}: section missing from the manifest.");
            return;
        }

        ValidateToolPlatformFile(tool.Windows, $"{path}.windows", errors, warnings);
        ValidateToolPlatformFile(tool.Linux, $"{path}.linux", errors, warnings);
    }

    private static void ValidateToolPlatformFile(ToolPlatformFile? file, string path, List<string> errors, List<string> warnings)
    {
        if (file is null)
        {
            return;
        }

        ValidateHash(file.Sha256, path, errors, warnings);

        foreach (var extraFile in file.ExtraFiles ?? [])
        {
            ValidateHash(extraFile.Sha256, $"{path}.extraFiles[{extraFile.FileName}]", errors, warnings);
        }
    }

    private static void ValidateHash(string? sha256, string path, List<string> errors, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(sha256) || sha256.Equals("TODO", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"{path}: sha256 not set yet (TODO).");
            return;
        }

        if (!Sha256Pattern().IsMatch(sha256))
        {
            errors.Add($"{path}: invalid sha256 ('{sha256}'), expected 64 hexadecimal characters.");
        }
    }
}
