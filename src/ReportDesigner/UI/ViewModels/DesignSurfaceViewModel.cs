using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReportDesigner.Models;
using ReportDesigner.Services;
using ReportDesigner.ViewModels;

namespace ReportDesigner.UI.ViewModels;

/// <summary>
/// Состояние выделения и интерактивных жестов (drag/resize) канваса <c>DesignSurface</c>.
/// Публичный API — на <see cref="System.Drawing"/>-типах (px, страница целиком), без привязки
/// к Avalonia, чтобы оставаться юнит-тестируемым напрямую.
/// </summary>
public partial class DesignSurfaceViewModel : ViewModelBase
{
    private const float MinObjectSizePx = 8f;
    private const float BoundsEpsilonPx = 0.01f;

    private enum GestureKind { None, Drag, Resize }

    private readonly IFastReportService _service;

    private GestureKind _gestureKind = GestureKind.None;
    private string? _gestureObjectName;
    private float _gestureBandHeightPx;
    private RectangleF _gestureOriginalBoundsPx;
    private PointF _gestureStartPointPx;
    private ResizeHandle _gestureResizeHandle;

    [ObservableProperty] public partial DesignSnapshot Snapshot { get; set; }
    [ObservableProperty] public partial string? SelectedObjectName { get; set; }
    [ObservableProperty] public partial RectangleF? PreviewBoundsOverridePx { get; set; }
    [ObservableProperty] public partial double Zoom { get; set; } = 1.0;
    [ObservableProperty] public partial bool ShowGrid { get; set; } = true;

    /// <summary>Инструмент, выбранный в Toolbox — следующий клик на канвасе создаст объект
    /// этого типа. Null — обычный режим выделения/перетаскивания.</summary>
    [ObservableProperty] public partial DesignObjectType? PendingToolType { get; set; }

    /// <summary>Вызывается после любой зафиксированной мутации документа (для обновления превью/заголовка).</summary>
    public event Action? DocumentChanged;

    public DesignSurfaceViewModel(IFastReportService fastReportService)
    {
        _service = fastReportService;
        Snapshot = _service.GetSnapshot();
    }

    /// <summary>Вызывается после New/Open — сбрасывает выделение и текущий жест.</summary>
    public void Reset()
    {
        ClearGestureState();
        SelectedObjectName = null;
        CommitChange();
    }

    public (BandSnapshot Band, DesignObjectInfo Object)? FindSelectedObject() =>
        SelectedObjectName is { } name ? FindBandAndObject(name) : null;

    // ------------------------------------------------------------------
    // Toolbox
    // ------------------------------------------------------------------

    [RelayCommand]
    private void SelectTool(DesignObjectType type) => PendingToolType = type;

    /// <summary>
    /// Вызывается DesignSurface при клике на канвасе, если выбран инструмент (<see cref="PendingToolType"/>).
    /// Создаёт объект дефолтного размера в полосе под точкой клика (верхний левый угол — точка
    /// клика), выделяет его и сбрасывает активный инструмент (инструмент одноразовый — клик мимо
    /// полос страницы просто отменяет его, ничего не создавая).
    /// </summary>
    public void PlaceObjectAt(PointF pagePointPx)
    {
        if (PendingToolType is not { } type) return;
        PendingToolType = null;

        if (SnapshotHitTester.FindBandAt(Snapshot, pagePointPx) is not { } band) return;

        var (widthPx, heightPx) = DefaultSizePx(type);
        var bounds = ResizeGeometry.ClampVertical(
            new RectangleF(pagePointPx.X, pagePointPx.Y - band.Top, widthPx, heightPx), band.Height);

        var name = _service.AddObject(type,
            UnitConverter.PxToCm(bounds.X), UnitConverter.PxToCm(bounds.Y),
            UnitConverter.PxToCm(bounds.Width), UnitConverter.PxToCm(bounds.Height),
            band.Name);

        SelectedObjectName = name;
        CommitChange();
    }

    private static (float WidthPx, float HeightPx) DefaultSizePx(DesignObjectType type) => type switch
    {
        DesignObjectType.Text => (UnitConverter.CmToPx(4f), UnitConverter.CmToPx(1f)),
        DesignObjectType.Line => (UnitConverter.CmToPx(3f), 0f),
        DesignObjectType.Shape => (UnitConverter.CmToPx(3f), UnitConverter.CmToPx(2f)),
        DesignObjectType.Picture => (UnitConverter.CmToPx(3f), UnitConverter.CmToPx(3f)),
        _ => (UnitConverter.CmToPx(3f), UnitConverter.CmToPx(2f)),
    };

    // ------------------------------------------------------------------
    // Перетаскивание
    // ------------------------------------------------------------------

    public void BeginDrag(string objectName, PointF startPointPagePx)
    {
        if (FindBandAndObject(objectName) is not { } found) return;

        SelectedObjectName = objectName;
        _gestureKind = GestureKind.Drag;
        _gestureObjectName = objectName;
        _gestureBandHeightPx = found.Band.Height;
        _gestureOriginalBoundsPx = found.Object.Bounds;
        _gestureStartPointPx = startPointPagePx;
    }

    public void UpdateDrag(PointF currentPointPagePx)
    {
        if (_gestureKind != GestureKind.Drag) return;

        var dx = currentPointPagePx.X - _gestureStartPointPx.X;
        var dy = currentPointPagePx.Y - _gestureStartPointPx.Y;
        var moved = new RectangleF(
            _gestureOriginalBoundsPx.X + dx, _gestureOriginalBoundsPx.Y + dy,
            _gestureOriginalBoundsPx.Width, _gestureOriginalBoundsPx.Height);

        PreviewBoundsOverridePx = ResizeGeometry.ClampVertical(moved, _gestureBandHeightPx);
    }

    public void CommitDrag() => CommitGesture();

    public void CancelDrag() => ClearGestureState();

    // ------------------------------------------------------------------
    // Ресайз
    // ------------------------------------------------------------------

    public void BeginResize(string objectName, ResizeHandle handle, PointF startPointPagePx)
    {
        if (FindBandAndObject(objectName) is not { } found) return;

        SelectedObjectName = objectName;
        _gestureKind = GestureKind.Resize;
        _gestureObjectName = objectName;
        _gestureResizeHandle = handle;
        _gestureBandHeightPx = found.Band.Height;
        _gestureOriginalBoundsPx = found.Object.Bounds;
        _gestureStartPointPx = startPointPagePx;
    }

    public void UpdateResize(PointF currentPointPagePx)
    {
        if (_gestureKind != GestureKind.Resize) return;

        var dx = currentPointPagePx.X - _gestureStartPointPx.X;
        var dy = currentPointPagePx.Y - _gestureStartPointPx.Y;
        var resized = ResizeGeometry.ApplyResize(_gestureOriginalBoundsPx, _gestureResizeHandle, dx, dy, MinObjectSizePx);

        PreviewBoundsOverridePx = ResizeGeometry.ClampVertical(resized, _gestureBandHeightPx);
    }

    public void CommitResize() => CommitGesture();

    public void CancelResize() => ClearGestureState();

    /// <summary>
    /// Коммитит активный жест (если есть). DesignSurface вызывает и CommitDrag(), и
    /// CommitResize() при отпускании кнопки мыши, не зная заранее, какой жест на самом деле
    /// шёл — поэтому тип жеста (drag/resize) определяется здесь по внутреннему
    /// <see cref="_gestureKind"/>, а не по тому, какой из двух публичных методов вызвали первым.
    /// </summary>
    private void CommitGesture()
    {
        if (_gestureObjectName is null) { ClearGestureState(); return; }

        var objectName = _gestureObjectName;
        var originalBounds = _gestureOriginalBoundsPx;
        var isResize = _gestureKind == GestureKind.Resize;
        var finalBounds = PreviewBoundsOverridePx ?? originalBounds;
        ClearGestureState();

        if (BoundsEqual(finalBounds, originalBounds)) return; // клик без движения — не пачкаем документ

        _service.MoveObject(objectName, UnitConverter.PxToCm(finalBounds.X), UnitConverter.PxToCm(finalBounds.Y));
        if (isResize)
            _service.ResizeObject(objectName, UnitConverter.PxToCm(finalBounds.Width), UnitConverter.PxToCm(finalBounds.Height));

        CommitChange();
    }

    private void ClearGestureState()
    {
        _gestureKind = GestureKind.None;
        _gestureObjectName = null;
        PreviewBoundsOverridePx = null;
    }

    // ------------------------------------------------------------------
    // Клавиатура
    // ------------------------------------------------------------------

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedObjectName is not { } name) return;

        _service.DeleteObject(name);
        SelectedObjectName = null;
        CommitChange();
    }

    /// <summary>Escape: сначала гасит активный инструмент Toolbox (если выбран, но клика ещё не
    /// было), потом отменяет активный жест (если есть), иначе просто снимает выделение.</summary>
    public void HandleEscape()
    {
        if (PendingToolType is not null)
        {
            PendingToolType = null;
            return;
        }

        if (_gestureKind != GestureKind.None)
        {
            ClearGestureState();
            return;
        }

        SelectedObjectName = null;
    }

    public void ClearSelection() => SelectedObjectName = null;

    /// <summary>Сдвигает выделенный объект на (dxPx, dyPx) и сразу коммитит (клавиши-стрелки).</summary>
    public void Nudge(int dxPx, int dyPx)
    {
        if (SelectedObjectName is not { } name) return;
        if (FindBandAndObject(name) is not { } found) return;

        var moved = new RectangleF(
            found.Object.Bounds.X + dxPx, found.Object.Bounds.Y + dyPx,
            found.Object.Bounds.Width, found.Object.Bounds.Height);
        var clamped = ResizeGeometry.ClampVertical(moved, found.Band.Height);

        _service.MoveObject(name, UnitConverter.PxToCm(clamped.X), UnitConverter.PxToCm(clamped.Y));
        CommitChange();
    }

    // ------------------------------------------------------------------
    // Внутреннее
    // ------------------------------------------------------------------

    /// <summary>
    /// Пересобирает снимок из сервиса и уведомляет подписчиков. Публичный — вызывается не
    /// только изнутри (после жестов/удаления), но и внешними ViewModel (ObjectTree,
    /// PropertiesPanel), которые мутируют документ напрямую через IFastReportService и должны
    /// после этого синхронизировать канвас.
    /// </summary>
    public void CommitChange()
    {
        Snapshot = _service.GetSnapshot();
        DocumentChanged?.Invoke();
    }

    private (BandSnapshot Band, DesignObjectInfo Object)? FindBandAndObject(string objectName)
    {
        foreach (var page in Snapshot.Pages)
        {
            foreach (var band in page.Bands)
            {
                foreach (var obj in band.Objects)
                {
                    if (obj.Name == objectName) return (band, obj);
                }
            }
        }
        return null;
    }

    private static bool BoundsEqual(RectangleF a, RectangleF b) =>
        MathF.Abs(a.X - b.X) < BoundsEpsilonPx &&
        MathF.Abs(a.Y - b.Y) < BoundsEpsilonPx &&
        MathF.Abs(a.Width - b.Width) < BoundsEpsilonPx &&
        MathF.Abs(a.Height - b.Height) < BoundsEpsilonPx;
}
