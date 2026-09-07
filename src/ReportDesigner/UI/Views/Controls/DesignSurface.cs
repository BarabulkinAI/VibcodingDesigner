using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ReportDesigner.Models;
using ReportDesigner.Services;
using ReportDesigner.UI.ViewModels;
using SPointF = System.Drawing.PointF;
using SRectF = System.Drawing.RectangleF;
using SColor = System.Drawing.Color;
using AvPoint = Avalonia.Point;
using AvRect = Avalonia.Rect;
using AvColor = Avalonia.Media.Color;

namespace ReportDesigner.UI.Views.Controls;

/// <summary>
/// Канвас-редактор: рисует страницу отчёта (лист, сетка), выделение, 8 ручек ресайза,
/// обрабатывает перетаскивание/ресайз мышью и клавиши Del/Esc/стрелки. Вся интерактивная
/// логика (что выделено, идёт ли жест) живёт в <see cref="ViewModel"/>; контрол только рисует
/// и переводит координаты указателя в логические px страницы.
/// </summary>
public class DesignSurface : Control
{
    public static readonly StyledProperty<DesignSurfaceViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<DesignSurface, DesignSurfaceViewModel?>(nameof(ViewModel));

    public DesignSurfaceViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private const float HandleSizePx = 8f;
    private const float GridStepPx = 18.9f; // UnitConverter.CmToPx(0.5f)
    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;
    private const double ZoomStepFactor = 1.1;

    private static readonly IBrush DesktopBrush = new SolidColorBrush(AvColor.FromRgb(0xD8, 0xD8, 0xD8));
    private static readonly IBrush SheetBrush = Brushes.White;
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(AvColor.FromRgb(0xEA, 0xEA, 0xEA)), 1);
    private static readonly IPen BandBoundaryPen = new Pen(new SolidColorBrush(AvColor.FromRgb(0xC0, 0xC0, 0xC0)), 1);
    private static readonly IPen SelectionPen = new Pen(Brushes.DodgerBlue, 1.5);
    private static readonly IBrush HandleFillBrush = Brushes.White;
    private static readonly IPen HandleBorderPen = new Pen(Brushes.DodgerBlue, 1);
    private static readonly IBrush PictureBrush = new SolidColorBrush(AvColor.FromRgb(0xF0, 0xF0, 0xF0));

    static DesignSurface()
    {
        FocusableProperty.OverrideDefaultValue<DesignSurface>(true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ViewModelProperty)
        {
            if (change.OldValue is DesignSurfaceViewModel oldVm)
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            if (change.NewValue is DesignSurfaceViewModel newVm)
                newVm.PropertyChanged += OnViewModelPropertyChanged;

            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesignSurfaceViewModel.Zoom))
            InvalidateMeasure();
        if (e.PropertyName == nameof(DesignSurfaceViewModel.PendingToolType))
            Cursor = ViewModel?.PendingToolType is not null ? new Cursor(StandardCursorType.Cross) : Cursor.Default;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var page = ViewModel?.Snapshot.Pages.Count > 0 ? ViewModel.Snapshot.Pages[0] : null;
        if (ViewModel is null || page is null) return default;

        var zoom = ViewModel.Zoom;
        return new Size(page.Width * zoom, page.Height * zoom);
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    // ------------------------------------------------------------------
    // Отрисовка
    // ------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(DesktopBrush, null, new AvRect(Bounds.Size));

        var vm = ViewModel;
        var page = vm?.Snapshot.Pages.Count > 0 ? vm.Snapshot.Pages[0] : null;
        if (vm is null || page is null) return;

        var zoom = (float)vm.Zoom;
        using (context.PushTransform(Matrix.CreateScale(zoom, zoom)))
        {
            context.DrawRectangle(SheetBrush, null, new AvRect(0, 0, page.Width, page.Height));

            if (vm.ShowGrid)
                DrawGrid(context, page.Width, page.Height);

            foreach (var band in page.Bands)
            {
                DrawBandBoundary(context, band, page.Width);
                foreach (var obj in band.Objects)
                {
                    if (!obj.Visible) continue;
                    DrawObject(context, obj, GetRenderBounds(vm, band, obj));
                }
            }

            DrawSelection(context, vm);
        }
    }

    private static void DrawGrid(DrawingContext context, float width, float height)
    {
        for (var x = 0f; x <= width; x += GridStepPx)
            context.DrawLine(GridPen, new AvPoint(x, 0), new AvPoint(x, height));
        for (var y = 0f; y <= height; y += GridStepPx)
            context.DrawLine(GridPen, new AvPoint(0, y), new AvPoint(width, y));
    }

    private static void DrawBandBoundary(DrawingContext context, BandSnapshot band, float pageWidth)
    {
        context.DrawLine(BandBoundaryPen, new AvPoint(0, band.Top), new AvPoint(pageWidth, band.Top));
    }

    private static void DrawObject(DrawingContext context, DesignObjectInfo obj, SRectF bounds)
    {
        var rect = ToAvRect(bounds);

        switch (obj.Type)
        {
            case DesignObjectType.Text:
                DrawTextObject(context, obj, rect);
                break;
            case DesignObjectType.Line:
                context.DrawLine(new Pen(ToBrush(obj.LineColor), obj.LineWidth), rect.TopLeft, rect.BottomRight);
                break;
            case DesignObjectType.Shape:
                DrawShapeObject(context, obj, rect);
                break;
            case DesignObjectType.Picture:
                DrawPictureObject(context, rect);
                break;
        }
    }

    private static void DrawTextObject(DrawingContext context, DesignObjectInfo obj, AvRect rect)
    {
        var borderPen = obj.ShowBorder ? new Pen(ToBrush(obj.BorderColor), obj.BorderWidth) : null;
        context.DrawRectangle(null, borderPen, rect);

        if (string.IsNullOrEmpty(obj.Text)) return;

        var typeface = new Typeface(obj.FontName,
            obj.FontItalic ? FontStyle.Italic : FontStyle.Normal,
            obj.FontBold ? FontWeight.Bold : FontWeight.Normal);
        var formatted = new FormattedText(obj.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, obj.FontSize, ToBrush(obj.TextColor))
        {
            MaxTextWidth = Math.Max(rect.Width, 1),
            MaxTextHeight = Math.Max(rect.Height, 1),
            TextAlignment = obj.HorizontalAlign switch
            {
                DesignTextAlign.Center => TextAlignment.Center,
                DesignTextAlign.Right => TextAlignment.Right,
                _ => TextAlignment.Left,
            },
        };

        var yOffset = obj.VerticalAlign switch
        {
            DesignVerticalAlign.Middle => Math.Max(0, (rect.Height - formatted.Height) / 2),
            DesignVerticalAlign.Bottom => Math.Max(0, rect.Height - formatted.Height),
            _ => 0,
        };

        using (context.PushClip(rect))
            context.DrawText(formatted, new AvPoint(rect.X, rect.Y + yOffset));
    }

    private static void DrawShapeObject(DrawingContext context, DesignObjectInfo obj, AvRect rect)
    {
        var fillBrush = ToBrush(obj.FillColor);
        var borderPen = obj.ShowBorder ? new Pen(ToBrush(obj.BorderColor), obj.BorderWidth) : null;

        switch (obj.Shape)
        {
            case DesignShapeKind.Ellipse:
                context.DrawEllipse(fillBrush, borderPen, rect);
                break;
            case DesignShapeKind.RoundedRectangle:
                context.DrawRectangle(fillBrush, borderPen, rect, 6, 6);
                break;
            case DesignShapeKind.Diamond:
                context.DrawGeometry(fillBrush, borderPen, BuildPolygon(rect,
                    new AvPoint(rect.Center.X, rect.Top), new AvPoint(rect.Right, rect.Center.Y),
                    new AvPoint(rect.Center.X, rect.Bottom), new AvPoint(rect.Left, rect.Center.Y)));
                break;
            case DesignShapeKind.Triangle:
                context.DrawGeometry(fillBrush, borderPen, BuildPolygon(rect,
                    new AvPoint(rect.Center.X, rect.Top), new AvPoint(rect.Right, rect.Bottom), new AvPoint(rect.Left, rect.Bottom)));
                break;
            default:
                context.DrawRectangle(fillBrush, borderPen, rect);
                break;
        }
    }

    private static void DrawPictureObject(DrawingContext context, AvRect rect)
    {
        context.DrawRectangle(PictureBrush, new Pen(Brushes.Gray, 1), rect);
        var formatted = new FormattedText("IMG", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Arial"), 10, Brushes.Gray);
        var origin = new AvPoint(
            rect.X + Math.Max(0, (rect.Width - formatted.Width) / 2),
            rect.Y + Math.Max(0, (rect.Height - formatted.Height) / 2));
        context.DrawText(formatted, origin);
    }

    private static void DrawSelection(DrawingContext context, DesignSurfaceViewModel vm)
    {
        if (vm.FindSelectedObject() is not { } selected) return;

        var bandRelative = vm.PreviewBoundsOverridePx ?? selected.Object.Bounds;
        var pageBounds = ToPageBounds(selected.Band, bandRelative);
        var rect = ToAvRect(pageBounds);

        context.DrawRectangle(null, SelectionPen, rect);

        var allowedHandles = ResizeGeometry.AllowedHandles(selected.Object.Type);
        foreach (var (_, handleRect) in ResizeGeometry.GetHandles(pageBounds, HandleSizePx, allowedHandles))
            context.DrawRectangle(HandleFillBrush, HandleBorderPen, ToAvRect(handleRect));
    }

    private static StreamGeometry BuildPolygon(AvRect bounds, params AvPoint[] points)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], true);
        for (var i = 1; i < points.Length; i++)
            ctx.LineTo(points[i]);
        ctx.EndFigure(true);
        return geometry;
    }

    // ------------------------------------------------------------------
    // Указатель
    // ------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var vm = ViewModel;
        if (vm is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Focus();
        var point = ToPagePoint(e.GetPosition(this), vm.Zoom);

        if (vm.PendingToolType is not null)
        {
            vm.PlaceObjectAt(point);
            e.Handled = true;
            return;
        }

        if (vm.FindSelectedObject() is { } selected)
        {
            var pageBounds = ToPageBounds(selected.Band, selected.Object.Bounds);
            var allowedHandles = ResizeGeometry.AllowedHandles(selected.Object.Type);
            if (ResizeGeometry.HitTest(pageBounds, HandleSizePx, point, allowedHandles) is { } handle)
            {
                vm.BeginResize(selected.Object.Name, handle, point);
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
        }

        if (SnapshotHitTester.FindObjectAt(vm.Snapshot, point) is { } hit)
        {
            vm.BeginDrag(hit.Object.Name, point);
            e.Pointer.Capture(this);
        }
        else
        {
            vm.ClearSelection();
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var vm = ViewModel;
        if (vm is null) return;

        var point = ToPagePoint(e.GetPosition(this), vm.Zoom);
        vm.UpdateDrag(point);
        vm.UpdateResize(point);
        vm.UpdateCursorPosition(point);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ViewModel?.UpdateCursorPosition(null);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var vm = ViewModel;
        if (vm is null) return;

        vm.CommitDrag();
        vm.CommitResize();
        e.Pointer.Capture(null);
    }

    /// <summary>Ctrl+колесо мыши — зум канваса. Без Ctrl колесо остаётся обычным скроллом
    /// (событие не помечается обработанным, чтобы его получил родительский ScrollViewer).</summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var vm = ViewModel;
        if (vm is null || !e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Delta.Y == 0) return;

        var factor = e.Delta.Y > 0 ? ZoomStepFactor : 1.0 / ZoomStepFactor;
        vm.Zoom = Math.Clamp(vm.Zoom * factor, MinZoom, MaxZoom);
        e.Handled = true;
    }

    // ------------------------------------------------------------------
    // Клавиатура
    // ------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var vm = ViewModel;
        if (vm is null) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.C:
                    vm.CopySelected();
                    e.Handled = true;
                    return;
                case Key.V:
                    vm.Paste();
                    e.Handled = true;
                    return;
                case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                case Key.Y:
                    vm.RedoCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Z:
                    vm.UndoCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }

        switch (e.Key)
        {
            case Key.Delete:
                vm.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.HandleEscape();
                e.Handled = true;
                break;
            case Key.Left:
                vm.Nudge(-1, 0);
                e.Handled = true;
                break;
            case Key.Right:
                vm.Nudge(1, 0);
                e.Handled = true;
                break;
            case Key.Up:
                vm.Nudge(0, -1);
                e.Handled = true;
                break;
            case Key.Down:
                vm.Nudge(0, 1);
                e.Handled = true;
                break;
        }
    }

    // ------------------------------------------------------------------
    // Координаты
    // ------------------------------------------------------------------

    private static SPointF ToPagePoint(AvPoint controlPoint, double zoom) =>
        new((float)(controlPoint.X / zoom), (float)(controlPoint.Y / zoom));

    private static SRectF ToPageBounds(BandSnapshot band, SRectF bandRelativeBounds) =>
        new(bandRelativeBounds.X, band.Top + bandRelativeBounds.Y, bandRelativeBounds.Width, bandRelativeBounds.Height);

    private static SRectF GetRenderBounds(DesignSurfaceViewModel vm, BandSnapshot band, DesignObjectInfo obj)
    {
        var bandRelative = obj.Name == vm.SelectedObjectName && vm.PreviewBoundsOverridePx is { } overrideBounds
            ? overrideBounds
            : obj.Bounds;
        return ToPageBounds(band, bandRelative);
    }

    private static AvRect ToAvRect(SRectF r) => new(r.X, r.Y, r.Width, r.Height);

    private static IBrush ToBrush(SColor c) => new SolidColorBrush(AvColor.FromArgb(c.A, c.R, c.G, c.B));
}
