using ReportDesigner.Models;
using ReportDesigner.Services;
using Xunit;

namespace ReportDesigner.Tests;

/// <summary>
/// Жизненный цикл документа: New (без пути) → Modified (IsDirty) → Saved.
/// См. раздел «Жизненный цикл документа» в docs/ARCHITECTURE.md.
/// </summary>
public class DocumentLifecycleTests
{
    [Fact]
    public void CreateNew_IsCleanWithoutPath()
    {
        var service = new FastReportService();
        service.CreateNew();

        Assert.Null(service.CurrentFilePath);
        Assert.False(service.IsDirty);
    }

    [Fact]
    public void ObjectMutation_MarksDirty_AndSaveClearsIt()
    {
        var service = new FastReportService();
        service.CreateNew();

        var name = service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        Assert.True(service.IsDirty);

        var path = Path.Combine(Path.GetTempPath(), $"lc_obj_{Guid.NewGuid():N}.frx");
        try
        {
            service.Save(path);

            Assert.Equal(path, service.CurrentFilePath);
            Assert.False(service.IsDirty);

            // Повторная мутация снова делает документ изменённым.
            service.MoveObject(name, 1f, 2f);
            Assert.True(service.IsDirty);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json");
        }
    }

    [Fact]
    public void BandOperations_MarkDirty()
    {
        var service = new FastReportService();
        service.CreateNew();

        service.AddBand(BandKind.ColumnHeader, 1f);
        Assert.True(service.IsDirty);

        var snapshot = service.GetSnapshot().Pages[0];
        service.RemoveBand(snapshot.Bands[3].Name);
        Assert.True(service.IsDirty);
    }

    [Fact]
    public void ResizeAndDelete_MarkDirty()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Line, 0, 0, 3, 1);

        service.ResizeObject(name, 5f, 2f);
        Assert.True(service.IsDirty);

        service.DeleteObject(name);
        Assert.True(service.IsDirty);
    }

    [Fact]
    public void Load_SetsPathAndClearsDirty()
    {
        var source = new FastReportService();
        source.CreateNew();
        source.AddObject(DesignObjectType.Text, 1, 1, 3, 1);
        var path = Path.Combine(Path.GetTempPath(), $"lc_load_{Guid.NewGuid():N}.frx");
        try
        {
            source.Save(path);

            var loaded = new FastReportService();
            loaded.Load(path);

            Assert.Equal(path, loaded.CurrentFilePath);
            Assert.False(loaded.IsDirty);

            // Загруженный документ редактируем дальше — путь сохраняется.
            loaded.AddObject(DesignObjectType.Shape, 0, 0, 1, 1);
            Assert.True(loaded.IsDirty);
            Assert.Equal(path, loaded.CurrentFilePath);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json");
        }
    }
}