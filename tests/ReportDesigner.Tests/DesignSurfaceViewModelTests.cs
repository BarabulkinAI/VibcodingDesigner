using System.Drawing;
using System.Linq;
using ReportDesigner.Models;
using ReportDesigner.Services;
using ReportDesigner.UI.ViewModels;
using Xunit;

namespace ReportDesigner.Tests;

public class DesignSurfaceViewModelTests
{
    private static (FastReportService Service, DesignSurfaceViewModel Vm, string ObjectName) CreateWithObject()
    {
        var service = new FastReportService();
        service.CreateNew();
        // Небольшой объект по центру 2-см полосы — есть запас, чтобы двигать его в любую
        // сторону на десяток px, не упираясь в границу полосы (это отдельно проверяется
        // в Drag_ClampsToBandTop_WhenDraggedAboveBand).
        var name = service.AddObject(DesignObjectType.Shape, 0.3f, 0.3f, 1f, 0.3f);
        // AddObject уже выставил IsDirty=true — сбрасываем, чтобы тесты ниже проверяли
        // именно эффект жестов канваса, а не факт создания фикстуры.
        service.MarkSaved("fixture.frx");
        var vm = new DesignSurfaceViewModel(service);
        return (service, vm, name);
    }

    private static (FastReportService Service, DesignSurfaceViewModel Vm) CreateEmpty()
    {
        var service = new FastReportService();
        service.CreateNew();
        return (service, new DesignSurfaceViewModel(service));
    }

    private static RectangleF BoundsOf(FastReportService service, string name) =>
        service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects).Single(o => o.Name == name).Bounds;

    [Fact]
    public void UpdateDrag_SetsPreviewOverride_WithoutCallingService()
    {
        var (service, vm, name) = CreateWithObject();
        var original = BoundsOf(service, name);

        vm.BeginDrag(name, new PointF(0f, 0f));
        vm.UpdateDrag(new PointF(20f, 5f));

        Assert.NotNull(vm.PreviewBoundsOverridePx);
        Assert.Equal(original.X + 20f, vm.PreviewBoundsOverridePx!.Value.X, 3);
        // Bounds в сервисе ещё не изменились — коммита не было.
        Assert.Equal(original, BoundsOf(service, name));
        Assert.False(service.IsDirty);
    }

    [Fact]
    public void CommitDrag_WithMovement_UpdatesServiceAndRebuildsSnapshot()
    {
        var (service, vm, name) = CreateWithObject();
        var original = BoundsOf(service, name);

        var documentChangedRaised = false;
        vm.DocumentChanged += () => documentChangedRaised = true;

        vm.BeginDrag(name, new PointF(0f, 0f));
        vm.UpdateDrag(new PointF(15f, 10f));
        vm.CommitDrag();

        var moved = BoundsOf(service, name);
        Assert.Equal(original.X + 15f, moved.X, 1);
        Assert.Equal(original.Y + 10f, moved.Y, 1);
        Assert.True(service.IsDirty);
        Assert.True(documentChangedRaised);
        Assert.Null(vm.PreviewBoundsOverridePx);
    }

    [Fact]
    public void CommitDrag_WithoutMovement_DoesNotDirtyDocument()
    {
        var (service, vm, name) = CreateWithObject();

        vm.BeginDrag(name, new PointF(0f, 0f));
        // Мышь не двигалась — сразу коммит (обычный клик).
        vm.CommitDrag();

        Assert.False(service.IsDirty);
    }

    [Fact]
    public void CancelDrag_ClearsPreviewAndDoesNotTouchService()
    {
        var (service, vm, name) = CreateWithObject();
        var original = BoundsOf(service, name);

        vm.BeginDrag(name, new PointF(0f, 0f));
        vm.UpdateDrag(new PointF(50f, 50f));
        vm.CancelDrag();

        Assert.Null(vm.PreviewBoundsOverridePx);
        Assert.False(service.IsDirty);
        Assert.Equal(original, BoundsOf(service, name));
    }

    [Fact]
    public void Drag_ClampsToBandTop_WhenDraggedAboveBand()
    {
        var (service, vm, name) = CreateWithObject();

        vm.BeginDrag(name, new PointF(0f, 0f));
        vm.UpdateDrag(new PointF(0f, -1000f)); // тащим далеко вверх, за пределы полосы

        Assert.Equal(0f, vm.PreviewBoundsOverridePx!.Value.Top, 3);
    }

    [Fact]
    public void Resize_BottomRightHandle_GrowsObjectOnCommit()
    {
        var (service, vm, name) = CreateWithObject();
        var original = BoundsOf(service, name);

        vm.BeginResize(name, ResizeHandle.BottomRight, new PointF(0f, 0f));
        vm.UpdateResize(new PointF(10f, 6f));
        vm.CommitResize();

        var resized = BoundsOf(service, name);
        Assert.Equal(original.Width + 10f, resized.Width, 1);
        Assert.Equal(original.Height + 6f, resized.Height, 1);
        Assert.True(service.IsDirty);
    }

    [Fact]
    public void Resize_LineViaRightHandle_KeepsHeightAtZero()
    {
        // Регрессия: у только что созданной линии (Height = 0) растягивание за правый конец
        // раньше "подбрасывало" высоту до minSizePx (~8px) из-за безусловного min-size clamp'а,
        // и линия рисовалась по диагонали вместо ровной горизонтальной.
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Line, 0.5f, 0.5f, 3f, 0f);
        service.MarkSaved("fixture.frx");
        var vm = new DesignSurfaceViewModel(service);
        var original = BoundsOf(service, name);
        Assert.Equal(0f, original.Height, 3);

        vm.BeginResize(name, ResizeHandle.Right, new PointF(0f, 0f));
        vm.UpdateResize(new PointF(20f, 0f));
        vm.CommitResize();

        var resized = BoundsOf(service, name);
        Assert.Equal(0f, resized.Height, 3);
        Assert.Equal(original.Width + 20f, resized.Width, 1);
    }

    [Fact]
    public void CommitResize_PersistsSize_EvenWhenCommitDragIsCalledFirst()
    {
        // DesignSurface.OnPointerReleased вызывает CommitDrag(), затем CommitResize() подряд,
        // не зная заранее, какой жест шёл — этот тест воспроизводит именно такой порядок вызовов
        // (регрессия: раньше CommitDrag() успевал закоммитить только позицию и сбросить состояние
        // жеста до того, как CommitResize() успевал применить размер).
        var (service, vm, name) = CreateWithObject();
        var original = BoundsOf(service, name);

        vm.BeginResize(name, ResizeHandle.BottomRight, new PointF(0f, 0f));
        vm.UpdateResize(new PointF(10f, 6f));
        vm.CommitDrag();
        vm.CommitResize();

        var resized = BoundsOf(service, name);
        Assert.Equal(original.Width + 10f, resized.Width, 1);
        Assert.Equal(original.Height + 6f, resized.Height, 1);
    }

    [Fact]
    public void DeleteSelected_RemovesObjectAndClearsSelection()
    {
        var (service, vm, name) = CreateWithObject();
        vm.BeginDrag(name, new PointF(0, 0));
        vm.CommitDrag(); // выделяем без перемещения
        Assert.Equal(name, vm.SelectedObjectName);

        vm.DeleteSelectedCommand.Execute(null);

        Assert.Null(vm.SelectedObjectName);
        Assert.DoesNotContain(
            service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects),
            o => o.Name == name);
    }

    [Fact]
    public void SelectedObjectName_SurvivesSnapshotRebuild()
    {
        var (service, vm, name) = CreateWithObject();
        vm.BeginDrag(name, new PointF(0, 0));
        vm.CommitDrag();
        Assert.Equal(name, vm.SelectedObjectName);

        // Любая другая мутация пересобирает снимок целиком.
        vm.Nudge(1, 0);

        Assert.Equal(name, vm.SelectedObjectName);
        Assert.NotNull(vm.FindSelectedObject());
        Assert.Equal(name, vm.FindSelectedObject()!.Value.Object.Name);
    }

    [Fact]
    public void HandleEscape_DuringGesture_CancelsGestureButKeepsSelection()
    {
        var (service, vm, name) = CreateWithObject();
        var original = BoundsOf(service, name);

        vm.BeginDrag(name, new PointF(0, 0));
        vm.UpdateDrag(new PointF(30f, 30f));
        vm.HandleEscape();

        Assert.Null(vm.PreviewBoundsOverridePx);
        Assert.Equal(name, vm.SelectedObjectName); // выделение не снимается, только жест отменяется
        Assert.Equal(original, BoundsOf(service, name));
        Assert.False(service.IsDirty);
    }

    [Fact]
    public void HandleEscape_AtRest_ClearsSelection()
    {
        var (service, vm, name) = CreateWithObject();
        vm.BeginDrag(name, new PointF(0, 0));
        vm.CommitDrag();
        Assert.Equal(name, vm.SelectedObjectName);

        vm.HandleEscape();

        Assert.Null(vm.SelectedObjectName);
    }

    [Fact]
    public void SelectTool_SetsPendingToolType()
    {
        var (_, vm) = CreateEmpty();

        vm.SelectToolCommand.Execute(DesignObjectType.Text);

        Assert.Equal(DesignObjectType.Text, vm.PendingToolType);
    }

    [Fact]
    public void PlaceObjectAt_WithoutPendingTool_DoesNothing()
    {
        var (service, vm) = CreateEmpty();

        vm.PlaceObjectAt(new PointF(100f, 100f));

        Assert.Empty(service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects));
        Assert.Null(vm.SelectedObjectName);
    }

    [Fact]
    public void PlaceObjectAt_CreatesObjectAtClickPositionAndSelectsIt()
    {
        var (service, vm) = CreateEmpty();
        vm.SelectToolCommand.Execute(DesignObjectType.Text);

        var dataBand = service.GetSnapshot().Pages[0].Bands.Single(b => b.Kind == BandKind.Data);
        var clickPoint = new PointF(200f, dataBand.Top + 10f);

        vm.PlaceObjectAt(clickPoint);

        Assert.Null(vm.PendingToolType); // инструмент одноразовый — гасится после клика
        Assert.NotNull(vm.SelectedObjectName);

        var created = service.GetSnapshot().Pages[0].Bands
            .Single(b => b.Kind == BandKind.Data).Objects.Single();
        Assert.Equal(DesignObjectType.Text, created.Type);
        Assert.Equal(vm.SelectedObjectName, created.Name);
        Assert.Equal(200f, created.Bounds.X, 1);
        Assert.Equal(10f, created.Bounds.Y, 1);
    }

    [Fact]
    public void PlaceObjectAt_ClickOutsideAnyBand_CancelsToolWithoutCreatingObject()
    {
        var (service, vm) = CreateEmpty();
        vm.SelectToolCommand.Execute(DesignObjectType.Shape);

        var pageHeight = service.GetSnapshot().Pages[0].Height;
        vm.PlaceObjectAt(new PointF(10f, pageHeight + 500f)); // далеко за пределами страницы

        Assert.Null(vm.PendingToolType);
        Assert.Empty(service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects));
    }

    [Fact]
    public void HandleEscape_WithPendingTool_CancelsToolWithoutTouchingSelection()
    {
        var (service, vm, name) = CreateWithObject();
        vm.BeginDrag(name, new PointF(0, 0));
        vm.CommitDrag(); // выделяем существующий объект без перемещения
        vm.SelectToolCommand.Execute(DesignObjectType.Line);

        vm.HandleEscape();

        Assert.Null(vm.PendingToolType);
        Assert.Equal(name, vm.SelectedObjectName); // прежнее выделение осталось нетронутым
    }

    [Fact]
    public void Nudge_MovesSelectedObjectAndCommitsImmediately()
    {
        var (service, vm, name) = CreateWithObject();
        vm.BeginDrag(name, new PointF(0, 0));
        vm.CommitDrag();
        var original = BoundsOf(service, name);

        vm.Nudge(1, -1);

        var moved = BoundsOf(service, name);
        Assert.Equal(original.X + 1, moved.X, 1);
        Assert.Equal(original.Y - 1, moved.Y, 1);
        Assert.True(service.IsDirty);
    }
}
