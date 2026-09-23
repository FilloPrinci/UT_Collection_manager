using UTLauncher.Core.Games;

namespace UTLauncher.Core.Tests.Games;

public class OldUnrealExclusionMatcherTests
{
    private static readonly string[] Ut99UnpackIgnorePatterns =
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

    [Theory]
    [InlineData("System/UnrealTournament.ini")]
    [InlineData("System/User.ini")]
    [InlineData("System/Setup.bat")]
    [InlineData("System/UnrealTournament.exe")]
    [InlineData("System/SomeLib.dll")]
    [InlineData("Autorun.inf")]
    [InlineData("Setup.exe")]
    [InlineData("System/Core.int")]
    [InlineData("System/UnrealTournament.itt")]
    public void IsExcluded_ReturnsTrue_ForKnownIgnoredEntries(string relativePath)
    {
        Assert.True(OldUnrealExclusionMatcher.IsExcluded(relativePath, Ut99UnpackIgnorePatterns));
    }

    [Theory]
    [InlineData("DirectX7/DSETUP.dll")]
    [InlineData("GameSpy/Support.exe")]
    [InlineData("Microsoft/DirectX/setup.exe")]
    [InlineData("System400/Manual.txt")]
    public void IsExcluded_ReturnsTrue_ForFilesInsideExcludedDirectories(string relativePath)
    {
        Assert.True(OldUnrealExclusionMatcher.IsExcluded(relativePath, Ut99UnpackIgnorePatterns));
    }

    [Theory]
    [InlineData("System/UnrealTournament.u")]
    [InlineData("System/ut-bin")]
    [InlineData("Maps/DM-Deck16][.unr")]
    [InlineData("Textures/UWindow.utx")]
    [InlineData("System/Core.u")]
    [InlineData("readme.txt")]
    public void IsExcluded_ReturnsFalse_ForGameContentFiles(string relativePath)
    {
        Assert.False(OldUnrealExclusionMatcher.IsExcluded(relativePath, Ut99UnpackIgnorePatterns));
    }

    [Fact]
    public void IsExcluded_DoesNotMatchWildcardOutsideItsDirectory()
    {
        // "System/*.exe" must not exclude an .exe living in a different directory.
        Assert.False(OldUnrealExclusionMatcher.IsExcluded("Help/Readme.exe", Ut99UnpackIgnorePatterns));
    }

    [Fact]
    public void IsExcluded_IsCaseInsensitive()
    {
        Assert.True(OldUnrealExclusionMatcher.IsExcluded("system/unrealtournament.INI", Ut99UnpackIgnorePatterns));
    }
}
