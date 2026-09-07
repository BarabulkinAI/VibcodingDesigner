using ReportDesigner.Services;
using Xunit;

namespace ReportDesigner.Tests;

public class RecentFilesServiceTests
{
    private static string TempStatePath() => Path.Combine(Path.GetTempPath(), $"recent_{Guid.NewGuid():N}.json");

    [Fact]
    public void GetRecent_EmptyWhenFileMissing()
    {
        var service = new RecentFilesService(TempStatePath());

        Assert.Empty(service.GetRecent());
    }

    [Fact]
    public void Touch_AddsPathToFront()
    {
        var path = TempStatePath();
        try
        {
            var service = new RecentFilesService(path);

            service.Touch("a.frx");
            service.Touch("b.frx");

            Assert.Equal(new[] { "b.frx", "a.frx" }, service.GetRecent());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Touch_MovesExistingPathToFrontWithoutDuplicating()
    {
        var path = TempStatePath();
        try
        {
            var service = new RecentFilesService(path);
            service.Touch("a.frx");
            service.Touch("b.frx");

            service.Touch("a.frx");

            Assert.Equal(new[] { "a.frx", "b.frx" }, service.GetRecent());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Touch_TrimsToMaxEntries()
    {
        var path = TempStatePath();
        try
        {
            var service = new RecentFilesService(path);
            for (var i = 0; i < 12; i++)
                service.Touch($"file{i}.frx");

            var recent = service.GetRecent();

            Assert.True(recent.Count <= 8);
            Assert.Equal("file11.frx", recent[0]); // самый недавний — первым
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Remove_DeletesEntry()
    {
        var path = TempStatePath();
        try
        {
            var service = new RecentFilesService(path);
            service.Touch("a.frx");
            service.Touch("b.frx");

            service.Remove("a.frx");

            Assert.DoesNotContain("a.frx", service.GetRecent());
            Assert.Contains("b.frx", service.GetRecent());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Touch_PersistsAcrossNewInstances()
    {
        var path = TempStatePath();
        try
        {
            new RecentFilesService(path).Touch("a.frx");

            var reloaded = new RecentFilesService(path).GetRecent();

            Assert.Equal(new[] { "a.frx" }, reloaded);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetRecent_CorruptedFile_ReturnsEmptyInsteadOfThrowing()
    {
        var path = TempStatePath();
        try
        {
            File.WriteAllText(path, "не json вообще");
            var service = new RecentFilesService(path);

            Assert.Empty(service.GetRecent());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
