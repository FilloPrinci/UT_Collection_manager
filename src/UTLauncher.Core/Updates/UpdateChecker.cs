using System.Text.Json;

namespace UTLauncher.Core.Updates;

public sealed record UpdateCheckResult(bool IsUpdateAvailable, string? LatestVersion, string ReleasesUrl);

/// <summary>
/// Checks GitHub's "latest release" API for a newer launcher version than the one currently
/// running. This only checks and reports - it never downloads or replaces anything itself;
/// the actual update is still "download the new package from the Releases page and run
/// install.sh (or extract the zip) again", same as a first install.
/// </summary>
public sealed class UpdateChecker(HttpClient httpClient)
{
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/FilloPrinci/UT_Collection_manager/releases/latest";

    public const string ReleasesPageUrl = "https://github.com/FilloPrinci/UT_Collection_manager/releases/latest";

    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, CancellationToken cancellationToken)
    {
        // Local/dev builds (csproj default "0.0.0-dev", or the "dev" fallback when there's no
        // version metadata at all) have no meaningful version to compare against a real release -
        // skip the check entirely rather than nagging every from-source build with "0.0.0" being
        // older than everything.
        if (currentVersion.Contains("dev", StringComparison.OrdinalIgnoreCase) ||
            !TryParseVersion(currentVersion, out var current))
        {
            return new UpdateCheckResult(false, null, ReleasesPageUrl);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
        request.Headers.UserAgent.ParseAdd("UTLauncher-UpdateChecker");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var tagName = document.RootElement.TryGetProperty("tag_name", out var tagProperty)
            ? tagProperty.GetString()
            : null;

        if (tagName is null || !TryParseVersion(tagName, out var latest))
        {
            return new UpdateCheckResult(false, tagName, ReleasesPageUrl);
        }

        return new UpdateCheckResult(latest > current, tagName, ReleasesPageUrl);
    }

    private static bool TryParseVersion(string raw, out Version version)
    {
        var trimmed = raw.TrimStart('v', 'V');

        var plusIndex = trimmed.IndexOf('+');
        if (plusIndex >= 0)
        {
            trimmed = trimmed[..plusIndex];
        }

        var dashIndex = trimmed.IndexOf('-');
        if (dashIndex >= 0)
        {
            trimmed = trimmed[..dashIndex];
        }

        return Version.TryParse(trimmed, out version!);
    }
}
