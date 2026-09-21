using ReportDesigner.Services;
using Xunit;

namespace ReportDesigner.Tests;

public class DockLayoutStoreTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"layout_{Guid.NewGuid():N}", "layout.json");

    [Fact]
    public void Load_NullWhenFileMissing()
    {
        Assert.Null(new DockLayoutStore(TempPath()).Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAndCreatesDirectory()
    {
        var path = TempPath();
        try
        {
            new DockLayoutStore(path).Save("{\"a\":1}");

            Assert.Equal("{\"a\":1}", new DockLayoutStore(path).Load());
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
