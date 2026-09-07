using ReportDesigner.Models;
using ReportDesigner.Services;
using Xunit;

namespace ReportDesigner.Tests;

/// <summary>
/// Undo/redo хранится как снимок Report целиком (Save/Load в MemoryStream) + отдельно копия
/// источников данных (не входят в сериализацию .frx). Единственная точка фиксации — Checkpoint(),
/// которую вызывает DesignSurfaceViewModel.CommitChange() после каждой мутации — в этих тестах
/// вызывается напрямую, т.к. они бьют по сервису, а не по ViewModel (см.
/// DesignSurfaceViewModelTests для проверки самой интеграции с CommitChange()).
/// </summary>
public class FastReportServiceUndoRedoTests
{
    private static string DataBandName(FastReportService service) =>
        service.GetSnapshot().Pages[0].Bands.Single(b => b.Kind == BandKind.Data).Name;

    [Fact]
    public void Undo_NoOpWhenStackEmpty()
    {
        var service = new FastReportService();
        service.CreateNew();

        Assert.False(service.CanUndo);
        service.Undo(); // не должно бросать

        Assert.Empty(service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects));
    }

    [Fact]
    public void Redo_NoOpWhenStackEmpty()
    {
        var service = new FastReportService();
        service.CreateNew();

        Assert.False(service.CanRedo);
        service.Redo(); // не должно бросать
    }

    [Fact]
    public void Undo_RevertsLastMutation()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.Checkpoint(); // как будто CommitChange() после CreateNew (в реальности этого не бывает — CreateNew уже сеет базовую точку — но здесь просто фиксируем текущее API)

        var name = service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        service.Checkpoint();

        Assert.True(service.CanUndo);
        service.Undo();

        Assert.DoesNotContain(service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects), o => o.Name == name);
    }

    [Fact]
    public void Redo_ReappliesUndoneMutation()
    {
        var service = new FastReportService();
        service.CreateNew();

        var name = service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        service.Checkpoint();
        service.Undo();
        Assert.True(service.CanRedo);

        service.Redo();

        Assert.Contains(service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects), o => o.Name == name);
    }

    [Fact]
    public void MultipleUndo_RevertsInReverseOrder()
    {
        var service = new FastReportService();
        service.CreateNew();

        var a = service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        service.Checkpoint();
        var b = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 1);
        service.Checkpoint();

        service.Undo(); // отменяет добавление b
        var namesAfterFirstUndo = service.GetSnapshot().Pages[0].Bands.SelectMany(x => x.Objects).Select(o => o.Name).ToList();
        Assert.Contains(a, namesAfterFirstUndo);
        Assert.DoesNotContain(b, namesAfterFirstUndo);

        service.Undo(); // отменяет добавление a — назад к пустому документу
        Assert.Empty(service.GetSnapshot().Pages[0].Bands.SelectMany(x => x.Objects));
    }

    [Fact]
    public void NewMutationAfterUndo_ClearsRedoStack()
    {
        var service = new FastReportService();
        service.CreateNew();

        service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        service.Checkpoint();
        service.Undo();
        Assert.True(service.CanRedo);

        service.AddObject(DesignObjectType.Shape, 0, 0, 2, 1);
        service.Checkpoint();

        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Undo_RestoresDataSourceAssignment()
    {
        var service = new FastReportService();
        service.CreateNew();
        var dataBand = DataBandName(service);

        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        service.Checkpoint();

        service.AssignBandDataSource(dataBand, "Клиенты");
        service.Checkpoint();

        service.Undo(); // отменяет привязку источника к полосе

        var band = service.GetSnapshot().Pages[0].Bands.Single(b => b.Name == dataBand);
        Assert.Null(band.DataSourceName);
        Assert.Contains("Клиенты", service.GetDataSourceNames()); // сам источник ещё существует

        service.Undo(); // отменяет создание источника
        Assert.Empty(service.GetDataSourceNames());
    }

    [Fact]
    public void Redo_RestoresDataSourceAssignment()
    {
        var service = new FastReportService();
        service.CreateNew();
        var dataBand = DataBandName(service);
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        service.Checkpoint();
        service.AssignBandDataSource(dataBand, "Клиенты");
        service.Checkpoint();
        service.Undo();

        service.Redo();

        var band = service.GetSnapshot().Pages[0].Bands.Single(b => b.Name == dataBand);
        Assert.Equal("Клиенты", band.DataSourceName);
    }

    [Fact]
    public void CreateNew_ClearsUndoHistory()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        service.Checkpoint();
        Assert.True(service.CanUndo);

        service.CreateNew();

        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Load_ClearsUndoHistory()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        service.Checkpoint();
        var path = Path.Combine(Path.GetTempPath(), $"undo_load_{Guid.NewGuid():N}.frx");
        try
        {
            service.Save(path);

            service.Load(path);

            Assert.False(service.CanUndo);
            Assert.False(service.CanRedo);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Undo_MarksDocumentDirty()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        service.Checkpoint();
        service.MarkSaved("dummy.frx");

        service.Undo();

        Assert.True(service.IsDirty);
    }
}
