using UTLauncher.Core.Games;
using UTLauncher.Core.Manifest;

namespace UTLauncher.Core.Tests.Games;

public class EngineIniMergerTests
{
    private static readonly MasterServerSection MasterServer = new(
        Domain: "master-ut4.timiimit.com",
        Protocol: "https",
        EngineIniSections:
        [
            "OnlineSubsystemMcp.OnlineContentControlsServiceMcp UnrealTournamentDev",
            "OnlineSubsystemMcp.BaseServiceMcp",
            "OnlineSubsystemMcp.GameServiceMcp",
        ]);

    [Fact]
    public void Merge_CreatesAllSections_WhenFileDoesNotExistYet()
    {
        var result = EngineIniMerger.Merge(existingContent: null, MasterServer);

        foreach (var sectionName in MasterServer.EngineIniSections!)
        {
            Assert.Contains($"[{sectionName}]", result);
        }

        Assert.Contains("Domain=master-ut4.timiimit.com", result);
        Assert.Contains("Protocol=https", result);
    }

    [Fact]
    public void Merge_AddsOnlyMissingSections_LeavingExistingContentUntouched()
    {
        var existing =
            "[SomeOtherMod.Settings]\n" +
            "CustomKey=CustomValue\n" +
            "\n" +
            "[OnlineSubsystemMcp.BaseServiceMcp]\n" +
            "Domain=custom-server.example.com\n" +
            "Protocol=https\n" +
            "SomeExtraKey=1\n";

        var result = EngineIniMerger.Merge(existing, MasterServer);

        // Untouched: the unrelated section and the already-present target section (even though
        // its Domain differs from the manifest's - existing sections are never rewritten).
        Assert.Contains("[SomeOtherMod.Settings]\nCustomKey=CustomValue", result);
        Assert.Contains("Domain=custom-server.example.com", result);
        Assert.Contains("SomeExtraKey=1", result);
        Assert.DoesNotContain("Domain=master-ut4.timiimit.com\nProtocol=https\n[OnlineSubsystemMcp.BaseServiceMcp]", result);

        // Added: the two sections that weren't there yet.
        Assert.Contains("[OnlineSubsystemMcp.OnlineContentControlsServiceMcp UnrealTournamentDev]", result);
        Assert.Contains("[OnlineSubsystemMcp.GameServiceMcp]", result);

        // Only one copy of the section that was already present.
        Assert.Equal(1, CountOccurrences(result, "[OnlineSubsystemMcp.BaseServiceMcp]"));
    }

    [Fact]
    public void Merge_ReturnsContentUnchanged_WhenAllSectionsAlreadyPresent()
    {
        var existing = EngineIniMerger.Merge(existingContent: null, MasterServer);

        var result = EngineIniMerger.Merge(existing, MasterServer);

        Assert.Equal(existing, result);
    }

    [Fact]
    public void Merge_DoesNotMatchASectionNameThatIsOnlyASubstring()
    {
        var existing = "[OnlineSubsystemMcp.BaseServiceMcpExtended]\nSomeKey=1\n";

        var result = EngineIniMerger.Merge(existing, MasterServer);

        Assert.Equal(1, CountOccurrences(result, "[OnlineSubsystemMcp.BaseServiceMcp]"));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
