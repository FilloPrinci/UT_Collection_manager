using UTLauncher.Core.Manifest;
using UTLauncher.Core.Platform;

namespace UTLauncher.Core.Games;

/// <summary>
/// Fixes up UT99's [Engine.Input] key bindings in User.ini. UT99 ships with "S" already bound by
/// default to Axis aUp (the same command as Jump); rebinding "Move Backward" to S through the
/// in-game menu does not reliably clear that pre-existing default, so S can end up not moving the
/// player backward at all. This is a bug in the shipped default config / rebind UI, not something
/// introduced by our installer (verified against the untouched DefUser.ini from OldUnreal's own
/// v469e patch) - editing User.ini directly is the reliable workaround.
/// </summary>
public sealed class Ut99KeyBindingHelper
{
    // Reuses the same Aliases the game itself already defines (Aliases[2..7] in a stock
    // User.ini), exactly like the default Up/Down/Left/Right bindings do, rather than writing
    // raw "Axis ..." commands directly.
    private static readonly (string Key, string Alias)[] WasdBindings =
    [
        ("W", "MoveForward"),
        ("A", "StrafeLeft"),
        ("S", "MoveBackward"),
        ("D", "StrafeRight"),
    ];

    public async Task ApplyWasdMovementAsync(
        GameEntry game, IPlatform platform, string installPath, CancellationToken cancellationToken)
    {
        var systemDirectory = GetSystemDirectory(game, platform, installPath);
        var userIniPath = Path.Combine(systemDirectory, "User.ini");

        if (!File.Exists(userIniPath))
        {
            var defaultIniPath = Path.Combine(systemDirectory, "DefUser.ini");
            if (!File.Exists(defaultIniPath))
            {
                throw new InvalidOperationException(
                    $"Neither User.ini nor DefUser.ini was found in '{systemDirectory}'. " +
                    "Launch the game at least once first, then try again.");
            }

            File.Copy(defaultIniPath, userIniPath);
        }

        var lines = (await File.ReadAllLinesAsync(userIniPath, cancellationToken).ConfigureAwait(false)).ToList();
        ApplyBindings(lines);
        await File.WriteAllLinesAsync(userIniPath, lines, cancellationToken).ConfigureAwait(false);
    }

    public static void ApplyBindings(List<string> lines)
    {
        var sectionStart = lines.FindIndex(
            l => l.Trim().Equals("[Engine.Input]", StringComparison.OrdinalIgnoreCase));
        if (sectionStart < 0)
        {
            throw new InvalidOperationException("User.ini does not contain an [Engine.Input] section.");
        }

        var sectionEnd = lines.Count;
        for (var i = sectionStart + 1; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith('['))
            {
                sectionEnd = i;
                break;
            }
        }

        foreach (var (key, alias) in WasdBindings)
        {
            var lineIndex = -1;
            for (var i = sectionStart + 1; i < sectionEnd; i++)
            {
                var equalsIndex = lines[i].IndexOf('=');
                if (equalsIndex >= 0 && lines[i][..equalsIndex] == key)
                {
                    lineIndex = i;
                    break;
                }
            }

            var newLine = $"{key}={alias}";
            if (lineIndex >= 0)
            {
                lines[lineIndex] = newLine;
            }
            else
            {
                lines.Insert(sectionEnd, newLine);
                sectionEnd++;
            }
        }
    }

    private static string GetSystemDirectory(GameEntry game, IPlatform platform, string installPath)
    {
        if (game.Launch is null ||
            !game.Launch.TryGetValue(platform.Id, out var launchEntry) ||
            string.IsNullOrEmpty(launchEntry.Exe))
        {
            throw new InvalidOperationException($"Manifest has no launch entry for platform '{platform.Id}'.");
        }

        var relativeDirectory = Path.GetDirectoryName(launchEntry.Exe.Replace('/', Path.DirectorySeparatorChar))
            ?? string.Empty;
        return Path.Combine(installPath, relativeDirectory);
    }
}
