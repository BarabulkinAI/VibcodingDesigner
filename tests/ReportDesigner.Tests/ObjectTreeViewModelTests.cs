using System.Linq;
using ReportDesigner.Models;
using ReportDesigner.Services;
using ReportDesigner.UI.ViewModels;
using Xunit;

namespace ReportDesigner.Tests;

public class ObjectTreeViewModelTests
{
    private static (FastReportService Service, DesignSurfaceViewModel Surface, ObjectTreeViewModel Tree) Create()
    {
        var service = new FastReportService();
        service.CreateNew();
        var surface = new DesignSurfaceViewModel(service);
        var tree = new ObjectTreeViewModel(service, surface);
        return (service, surface, tree);
    }

    private static string DataBandName(FastReportService service) =>
        service.GetSnapshot().Pages[0].Bands.Single(b => b.Kind == BandKind.Data).Name;

    [Fact]
    public void Constructor_BuildsBandsFromSnapshot()
    {
        var (_, _, tree) = Create();

        Assert.Equal(5, tree.Bands.Count);
        Assert.All(tree.Bands, b => Assert.Empty(b.Objects));
    }

    [Fact]
    public void DocumentChanged_RebuildsTreeWithNewObject()
    {
        var (service, surface, tree) = Create();

        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        surface.CommitChange(); // так внешние ViewModel синхронизируют канвас после своих мутаций

        var dataBand = tree.Bands.Single(b => b.Name == DataBandName(service));
        Assert.Single(dataBand.Objects);
        Assert.Equal(name, dataBand.Objects[0].Name);
    }

    [Fact]
    public void SelectingNode_UpdatesDesignSurfaceSelection()
    {
        var (service, surface, tree) = Create();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        surface.CommitChange();

        var node = tree.Bands.SelectMany(b => b.Objects).Single();
        tree.SelectedNode = node;

        Assert.Equal(name, surface.SelectedObjectName);
    }

    [Fact]
    public void CanvasSelection_UpdatesSelectedNode()
    {
        var (service, surface, tree) = Create();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        surface.CommitChange();

        surface.SelectedObjectName = name;

        Assert.NotNull(tree.SelectedNode);
        Assert.Equal(name, tree.SelectedNode!.Name);
    }

    [Fact]
    public void ClearingCanvasSelection_ClearsSelectedNode()
    {
        var (service, surface, tree) = Create();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        surface.CommitChange();
        surface.SelectedObjectName = name;

        surface.SelectedObjectName = null;

        Assert.Null(tree.SelectedNode);
    }

    [Fact]
    public void SelectedNode_SurvivesTreeRebuildAfterUnrelatedMutation()
    {
        var (service, surface, tree) = Create();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        surface.CommitChange();
        tree.SelectedNode = tree.Bands.SelectMany(b => b.Objects).Single();

        service.AddObject(DesignObjectType.Text, 3, 0, 2, 1);
        surface.CommitChange(); // пересобирает дерево целиком — узлы становятся новыми экземплярами

        Assert.NotNull(tree.SelectedNode);
        Assert.Equal(name, tree.SelectedNode!.Name);
    }

    [Fact]
    public void BringToFront_CallsServiceAndRefreshesOrder()
    {
        var (service, surface, tree) = Create();
        var a = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var b = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        surface.CommitChange();
        tree.SelectedNode = tree.Bands.SelectMany(x => x.Objects).Single(n => n.Name == a);

        tree.BringToFrontCommand.Execute(null);

        var order = tree.Bands.Single(x => x.Name == DataBandName(service)).Objects.Select(o => o.Name).ToList();
        Assert.Equal(new[] { b, a }, order);
    }

    [Fact]
    public void SendToBack_CallsServiceAndRefreshesOrder()
    {
        var (service, surface, tree) = Create();
        var a = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var b = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        surface.CommitChange();
        tree.SelectedNode = tree.Bands.SelectMany(x => x.Objects).Single(n => n.Name == b);

        tree.SendToBackCommand.Execute(null);

        var order = tree.Bands.Single(x => x.Name == DataBandName(service)).Objects.Select(o => o.Name).ToList();
        Assert.Equal(new[] { b, a }, order);
    }

    [Fact]
    public void ZOrderCommands_NoOpWhenNothingSelected()
    {
        var (service, surface, tree) = Create();
        service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        surface.CommitChange();

        tree.BringToFrontCommand.Execute(null);
        tree.SendToBackCommand.Execute(null);
        tree.MoveForwardCommand.Execute(null);
        tree.MoveBackwardCommand.Execute(null);

        Assert.Null(tree.SelectedNode);
    }

    [Fact]
    public void AddBand_AddsBandToTree()
    {
        var (_, _, tree) = Create();
        var before = tree.Bands.Count;
        tree.SelectedKindToAdd = BandKind.Data;

        tree.AddBandCommand.Execute(null);

        Assert.Equal(before + 1, tree.Bands.Count);
    }

    [Fact]
    public void RemoveSelectedBand_RemovesBandAndClearsSelection()
    {
        var (service, _, tree) = Create();
        var title = DataBandName(service);
        tree.SelectedBandNode = tree.Bands.Single(b => b.Name == title);

        tree.RemoveSelectedBandCommand.Execute(null);

        Assert.DoesNotContain(tree.Bands, b => b.Name == title);
        Assert.Null(tree.SelectedBandNode);
    }

    [Fact]
    public void SelectingBand_NeverDirtiesDocument()
    {
        var (service, _, tree) = Create();

        tree.SelectedBandNode = tree.Bands[0];

        Assert.False(service.IsDirty);
    }

    [Fact]
    public void SelectingBand_PopulatesNameAndHeightFields()
    {
        var (service, _, tree) = Create();
        var band = tree.Bands[0];

        tree.SelectedBandNode = band;

        Assert.True(tree.IsBandSelected);
        Assert.Equal(band.Name, tree.BandNameEdit);
        Assert.Equal(Math.Round(UnitConverter.PxToCm(band.HeightPx), 2), tree.BandHeightCm);
    }

    [Fact]
    public void EditingBandName_RenamesBandAndKeepsSelection()
    {
        var (service, _, tree) = Create();
        tree.SelectedBandNode = tree.Bands[0];

        tree.BandNameEdit = "Заголовок1";

        Assert.NotNull(tree.SelectedBandNode);
        Assert.Equal("Заголовок1", tree.SelectedBandNode!.Name);
        Assert.Contains(tree.Bands, b => b.Name == "Заголовок1");
    }

    [Fact]
    public void EditingBandHeight_UpdatesBandHeight()
    {
        var (service, _, tree) = Create();
        var band = tree.Bands[0];
        tree.SelectedBandNode = band;

        tree.BandHeightCm = 5;

        var updated = tree.Bands.Single(b => b.Name == band.Name);
        Assert.Equal(UnitConverter.CmToPx(5f), updated.HeightPx, 1);
    }

    [Fact]
    public void CanAddSelectedKind_FalseWhenSingletonBandExists()
    {
        var (_, _, tree) = Create();

        tree.SelectedKindToAdd = BandKind.ReportTitle; // CreateNew уже добавляет её

        Assert.False(tree.CanAddSelectedKind);
    }

    [Fact]
    public void CanAddSelectedKind_TrueForNonSingletonKind()
    {
        var (_, _, tree) = Create();

        tree.SelectedKindToAdd = BandKind.Data;

        Assert.True(tree.CanAddSelectedKind);
    }

    [Fact]
    public void SelectingDataBand_ShowsAssignedDataSource()
    {
        var (service, surface, tree) = Create();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        var dataBand = DataBandName(service);
        service.AssignBandDataSource(dataBand, "Клиенты");
        surface.CommitChange();

        tree.SelectedBandNode = tree.Bands.Single(b => b.Name == dataBand);

        Assert.True(tree.IsDataBandSelected);
        Assert.Equal("Клиенты", tree.SelectedBandDataSource);
    }

    [Fact]
    public void ChangingBandDataSourceCombo_AssignsSource()
    {
        var (service, surface, tree) = Create();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        surface.CommitChange();
        var dataBand = DataBandName(service);
        tree.SelectedBandNode = tree.Bands.Single(b => b.Name == dataBand);

        tree.SelectedBandDataSource = "Клиенты";

        var band = service.GetSnapshot().Pages[0].Bands.Single(b => b.Name == dataBand);
        Assert.Equal("Клиенты", band.DataSourceName);
    }

    [Fact]
    public void BandKindOptions_ExcludesGroupFooterAndChild()
    {
        var (_, _, tree) = Create();

        Assert.DoesNotContain(BandKind.GroupFooter, tree.BandKindOptions);
        Assert.DoesNotContain(BandKind.Child, tree.BandKindOptions);
    }

    [Fact]
    public void SelectingNonDataBand_HidesDataSourceSection()
    {
        var (service, _, tree) = Create();
        var titleBand = service.GetSnapshot().Pages[0].Bands[0].Name; // ReportTitle

        tree.SelectedBandNode = tree.Bands.Single(b => b.Name == titleBand);

        Assert.False(tree.IsDataBandSelected);
    }
}
