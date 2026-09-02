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
}
