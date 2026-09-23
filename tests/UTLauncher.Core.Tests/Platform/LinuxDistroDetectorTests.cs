using UTLauncher.Core.Platform;

namespace UTLauncher.Core.Tests.Platform;

public class LinuxDistroDetectorTests
{
    private static string WriteOsRelease(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void DetectId_ParsesQuotedId()
    {
        var path = WriteOsRelease("NAME=\"Fedora Linux\"\nID=fedora\nVERSION_ID=43\n");

        try
        {
            Assert.Equal("fedora", LinuxDistroDetector.DetectId(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DetectId_ParsesUnquotedId()
    {
        var path = WriteOsRelease("NAME=Arch\nID=arch\n");

        try
        {
            Assert.Equal("arch", LinuxDistroDetector.DetectId(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DetectId_ReturnsNull_WhenFileMissing()
    {
        Assert.Null(LinuxDistroDetector.DetectId("/nonexistent/os-release"));
    }

    [Fact]
    public void DetectId_ReturnsNull_WhenIdLineMissing()
    {
        var path = WriteOsRelease("NAME=Something\n");

        try
        {
            Assert.Null(LinuxDistroDetector.DetectId(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
