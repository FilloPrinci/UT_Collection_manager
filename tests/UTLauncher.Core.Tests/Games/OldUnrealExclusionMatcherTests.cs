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

    private static readonly string[] Ut2004UnpackIgnorePatterns =
    [
        "AutoRunData",
        "Disk1/layout.bin",
        "Disk1/Setup.*",
        "Disk1/setup.*",
        "SoNow",
        "*.*",
    ];

    [Theory]
    [InlineData("AutoRun.exe")]
    [InlineData("Autorun.inf")]
    [InlineData("Manual.pdf")]
    [InlineData("AutoRunData/bg.tga")]
    [InlineData("Disk1/layout.bin")]
    [InlineData("Disk1/Setup.bmp")]
    [InlineData("Disk1/setup.exe")]
    [InlineData("Disk1/setup.ini")]
    public void IsExcluded_ReturnsTrue_ForKnownUt2004IgnoredEntries(string relativePath)
    {
        Assert.True(OldUnrealExclusionMatcher.IsExcluded(relativePath, Ut2004UnpackIgnorePatterns));
    }

    [Theory]
    [InlineData("Disk1/data1.cab")]
    [InlineData("Disk1/data1.hdr")]
    [InlineData("Disk1/data2.cab")]
    [InlineData("Disk1/engine32.cab")]
    [InlineData("Disk2/data3.cab")]
    [InlineData("Disk5/data6.cab")]
    public void IsExcluded_ReturnsFalse_ForUt2004CabFiles(string relativePath)
    {
        Assert.False(OldUnrealExclusionMatcher.IsExcluded(relativePath, Ut2004UnpackIgnorePatterns));
    }

    [Fact]
    public void IsExcluded_RootWildcard_DoesNotMatchNestedFiles()
    {
        // "*.*" is a bare (non-recursive) pattern: it must only apply at the archive root,
        // not to files with a dot living inside subdirectories like Disk1/data1.cab.
        Assert.False(OldUnrealExclusionMatcher.IsExcluded("Disk1/data1.cab", Ut2004UnpackIgnorePatterns));
    }
}
