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
            errors.Add("manifestVersion deve essere >= 1.");
        }

        if (manifest.Games.Count == 0)
        {
            errors.Add("Il manifest non contiene giochi.");
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in manifest.Games)
        {
            if (string.IsNullOrWhiteSpace(game.Id))
            {
                errors.Add("Trovato un gioco con id mancante o vuoto.");
                continue;
            }

            if (!seenIds.Add(game.Id))
            {
                errors.Add($"Id gioco duplicato: '{game.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(game.Name))
            {
                errors.Add($"games[{game.Id}]: name mancante.");
            }

            if (string.IsNullOrWhiteSpace(game.VersionCode))
            {
                errors.Add($"games[{game.Id}]: versionCode mancante.");
            }

            foreach (var (sourceName, source) in game.Sources)
            {
                ValidateSourceFile(source, $"games[{game.Id}].sources.{sourceName}", errors, warnings);
            }

            ValidatePatchPlatform(game.Patch?.Windows, $"games[{game.Id}].patch.windows", errors, warnings);
            ValidatePatchPlatform(game.Patch?.LinuxX64, $"games[{game.Id}].patch.linux-x64", errors, warnings);
        }

        ValidateToolEntry(manifest.Tools?.SevenZip, "tools.sevenZip", errors, warnings);
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
            warnings.Add($"{path}: nessun URL disponibile (accettabile solo se pensato per import locale).");
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
            warnings.Add($"{path}: sezione assente nel manifest.");
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
    }

    private static void ValidateHash(string? sha256, string path, List<string> errors, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(sha256) || sha256.Equals("TODO", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"{path}: sha256 non ancora impostato (TODO).");
            return;
        }

        if (!Sha256Pattern().IsMatch(sha256))
        {
            errors.Add($"{path}: sha256 non valido ('{sha256}'), attesi 64 caratteri esadecimali.");
        }
    }
}
