using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ReportDesigner.Services;

namespace ReportDesigner.UI.Views.Controls;

/// <summary>
/// Горизонтальная или вертикальная линейка с делениями в см вокруг канваса-редактора.
/// Не привязана к ViewModel напрямую (как DesignSurface) — берёт масштаб и смещение прокрутки
/// через простые Avalonia-свойства, которые MainWindow заполняет биндингом (Zoom) и обработчиком
/// ScrollViewer.ScrollChanged (ScrollOffset), т.к. живого биндинга на текущий Offset у
/// ScrollViewer нет.
/// </summary>
public class RulerView : Control
{
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<RulerView, double>(nameof(Zoom), 1.0);

    public static readonly StyledProperty<double> ScrollOffsetProperty =
        AvaloniaProperty.Register<RulerView, double>(nameof(ScrollOffset));

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<RulerView, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double ScrollOffset
    {
        get => GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
    private static readonly IPen MajorTickPen = new Pen(Brushes.Gray, 1);
    private static readonly IPen MinorTickPen = new Pen(new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), 1);
    private static readonly IBrush LabelBrush = Brushes.Gray;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ZoomProperty || change.Property == ScrollOffsetProperty || change.Property == BoundsProperty)
            InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        context.DrawRectangle(BackgroundBrush, null, new Rect(bounds.Size));

        var viewportPx = Orientation == Orientation.Horizontal ? bounds.Width : bounds.Height;
        if (viewportPx <= 0 || Zoom <= 0) return;

        var startCm = UnitConverter.PxToCm((float)(ScrollOffset / Zoom));
        var endCm = UnitConverter.PxToCm((float)((ScrollOffset + viewportPx) / Zoom));

        foreach (var tick in RulerTickCalculator.GetTicks(Zoom, startCm, endCm))
        {
            var screenPos = tick.PositionPx * Zoom - ScrollOffset;
            if (Orientation == Orientation.Horizontal)
                DrawHorizontalTick(context, screenPos, tick, bounds.Height);
            else
                DrawVerticalTick(context, screenPos, tick, bounds.Width);
        }
    }

    private void DrawHorizontalTick(DrawingContext context, double screenPos, RulerTickCalculator.Tick tick, double height)
    {
        var tickLength = tick.IsMajor ? height * 0.6 : height * 0.3;
        var pen = tick.IsMajor ? MajorTickPen : MinorTickPen;
        context.DrawLine(pen, new Point(screenPos, height - tickLength), new Point(screenPos, height));

        if (!tick.IsMajor) return;
        var formatted = FormatLabel(tick.ValueCm);
        context.DrawText(formatted, new Point(screenPos + 2, 1));
    }

    private void DrawVerticalTick(DrawingContext context, double screenPos, RulerTickCalculator.Tick tick, double width)
    {
        var tickLength = tick.IsMajor ? width * 0.6 : width * 0.3;
        var pen = tick.IsMajor ? MajorTickPen : MinorTickPen;
        context.DrawLine(pen, new Point(width - tickLength, screenPos), new Point(width, screenPos));

        if (!tick.IsMajor) return;
        var formatted = FormatLabel(tick.ValueCm);
        context.DrawText(formatted, new Point(1, screenPos + 1));
    }

    private static FormattedText FormatLabel(float valueCm)
    {
        var text = valueCm % 1 == 0 ? ((int)valueCm).ToString(CultureInfo.InvariantCulture) : valueCm.ToString("0.#", CultureInfo.InvariantCulture);
        return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Arial"), 9, LabelBrush);
    }
}
