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
    /// <summary>Отформатированный масштаб для статус-бара, обновляется автоматически при
    /// изменении Zoom.</summary>
    [ObservableProperty] public partial string ZoomPercentText { get; set; } = "100 %";

    /// <summary>Позиция курсора над канвасом в см (для статус-бара) — null, когда курсор вне
    /// канваса.</summary>
    [ObservableProperty] public partial double? CursorXCm { get; set; }
    [ObservableProperty] public partial double? CursorYCm { get; set; }
    /// <summary>Готовая для статус-бара строка — пусто, когда курсор вне канваса. Отдельно от
    /// CursorXCm/CursorYCm, чтобы не тащить форматирование чисел в XAML.</summary>
    [ObservableProperty] public partial string CursorPositionText { get; set; } = "";

    /// <summary>Инструмент, выбранный в Toolbox — следующий клик на канвасе создаст объект
    /// этого типа. Null — обычный режим выделения/перетаскивания.</summary>
    [ObservableProperty] public partial DesignObjectType? PendingToolType { get; set; }

    [ObservableProperty] public partial bool CanUndo { get; set; }
    [ObservableProperty] public partial bool CanRedo { get; set; }

    /// <summary>Вызывается после любой зафиксированной мутации документа (для обновления превью/заголовка).</summary>
    public event Action? DocumentChanged;

    public DesignSurfaceViewModel(IFastReportService fastReportService)
    {
        _service = fastReportService;
        Snapshot = _service.GetSnapshot();
    }

    /// <summary>Вызывается после New/Open — сбрасывает выделение и текущий жест. Не чекпойнтит
    /// историю отмены (как Undo/Redo) — New/Open уже сбросили её сами и заложили точку отсчёта
    /// (IFastReportService.CreateNew/Load), лишний Checkpoint() тут задним числом запушил бы эту
    /// же самую точку отсчёта в стек отмены, включив "Отменить" без единой реальной мутации.</summary>
    public void Reset() => RefreshFromService(checkpoint: false);

    partial void OnZoomChanged(double value) => ZoomPercentText = $"{value:P0}";

    /// <summary>Вызывается DesignSurface при движении указателя (для статус-бара). null — курсор
    /// покинул канвас.</summary>
    public void UpdateCursorPosition(PointF? pagePointPx)
    {
        if (pagePointPx is not { } p)
        {
            CursorXCm = null;
            CursorYCm = null;
            CursorPositionText = "";
            return;
        }

        CursorXCm = UnitConverter.PxToCm(p.X);
        CursorYCm = UnitConverter.PxToCm(p.Y);
        CursorPositionText = $"X: {CursorXCm:F1} см, Y: {CursorYCm:F1} см";
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
    // Копирование / вставка (Ctrl+C / Ctrl+V)
    // ------------------------------------------------------------------

    /// <summary>«Буфер обмена» — не системный Clipboard, а данные выделенного объекта из
    /// снимка плюс полоса, в которой он был на момент копирования. Вставка не клонирует объект
    /// FastReport, а создаёт новый через уже протестированный AddObject и переносит свойства
    /// теми же сеттерами, что использует PropertiesPanelViewModel.</summary>
    private (BandSnapshot Band, DesignObjectInfo Object)? _clipboard;

    public void CopySelected() => _clipboard = FindSelectedObject();

    public void Paste()
    {
        if (_clipboard is not { } clip) return;

        var allBands = Snapshot.Pages.SelectMany(p => p.Bands).ToList();
        var targetBand = allBands.FirstOrDefault(b => b.Name == clip.Band.Name) ?? allBands.FirstOrDefault();
        if (targetBand is null) return;

        var offsetPx = UnitConverter.CmToPx(0.5f);
        var moved = new RectangleF(
            clip.Object.Bounds.X + offsetPx, clip.Object.Bounds.Y + offsetPx,
            clip.Object.Bounds.Width, clip.Object.Bounds.Height);
        var clamped = ResizeGeometry.ClampVertical(moved, targetBand.Height);

        var name = _service.AddObject(clip.Object.Type,
            UnitConverter.PxToCm(clamped.X), UnitConverter.PxToCm(clamped.Y),
            UnitConverter.PxToCm(clamped.Width), UnitConverter.PxToCm(clamped.Height),
            targetBand.Name);

        ApplyClipboardProperties(name, clip.Object);

        SelectedObjectName = name;
        CommitChange();
    }

    /// <summary>Картинки не копируются (нет пути передать Bitmap через существующий API без
    /// записи на диск) — сознательное ограничение MVP.</summary>
    private void ApplyClipboardProperties(string name, DesignObjectInfo obj)
    {
        _service.SetVisible(name, obj.Visible);

        switch (obj.Type)
        {
            case DesignObjectType.Text:
                _service.SetText(name, obj.Text ?? "");
                _service.SetFont(name, obj.FontName, obj.FontSize, obj.FontBold, obj.FontItalic);
                _service.SetTextColor(name, obj.TextColor);
                _service.SetHorizontalAlign(name, obj.HorizontalAlign);
                _service.SetVerticalAlign(name, obj.VerticalAlign);
                _service.SetBorder(name, obj.ShowBorder, UnitConverter.PxToCm(obj.BorderWidth), obj.BorderColor);
                break;
            case DesignObjectType.Shape:
                _service.SetShapeKind(name, obj.Shape);
                _service.SetFillColor(name, obj.FillColor);
                _service.SetBorder(name, obj.ShowBorder, UnitConverter.PxToCm(obj.BorderWidth), obj.BorderColor);
                break;
            case DesignObjectType.Line:
                _service.SetLineStyle(name, UnitConverter.PxToCm(obj.LineWidth), obj.LineColor);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Внутреннее
    // ------------------------------------------------------------------

    /// <summary>
    /// Пересобирает снимок из сервиса и уведомляет подписчиков. Публичный — вызывается не
    /// только изнутри (после жестов/удаления), но и внешними ViewModel (ObjectTree,
    /// PropertiesPanel, DataSources), которые мутируют документ напрямую через
    /// IFastReportService и должны после этого синхронизировать канвас. Заодно единственная
    /// точка, где фиксируется чекпойнт истории отмены (см. IFastReportService.Checkpoint) —
    /// именно поэтому все мутирующие ViewModel обязаны звать CommitChange после каждой мутации.
    /// </summary>
    public void CommitChange() => RefreshFromService(checkpoint: true);

    [RelayCommand]
    private void Undo()
    {
        _service.Undo();
        RefreshFromService(checkpoint: false);
    }

    [RelayCommand]
    private void Redo()
    {
        _service.Redo();
        RefreshFromService(checkpoint: false);
    }

    private void RefreshFromService(bool checkpoint)
    {
        if (checkpoint)
        {
            _service.Checkpoint();
        }
        else
        {
            // Undo/Redo подменяют документ целиком — прежнее выделение и активный жест могли
            // указывать на объект, которого в восстановленном состоянии уже нет (или который
            // ещё не существовал).
            ClearGestureState();
            SelectedObjectName = null;
        }

        Snapshot = _service.GetSnapshot();
        CanUndo = _service.CanUndo;
        CanRedo = _service.CanRedo;
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
