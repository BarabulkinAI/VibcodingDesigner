using System.Drawing;
using ReportDesigner.Models;

namespace ReportDesigner.Services;

/// <summary>Позиция ручки ресайза относительно рамки выделения.</summary>
public enum ResizeHandle
{
    TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left
}

/// <summary>
/// Чистая геометрия ресайза/выделения на канвасе: расположение 8 ручек, hit-test по ним,
/// применение перетаскивания ручки к прямоугольнику и вертикальный кламп в пределах полосы.
/// Работает в логических пикселях, не зависит от Avalonia.
/// </summary>
public static class ResizeGeometry
{
    private static readonly HashSet<ResizeHandle> LineHandles = new() { ResizeHandle.Left, ResizeHandle.Right };

    /// <summary>
    /// Линию можно тянуть только за концы (Left/Right) — она рисуется от левого верхнего до
    /// правого нижнего угла bounds, поэтому доступ к угловым/вертикальным ручкам превратил бы
    /// её в диагональ при любом случайном движении. Для остальных типов доступны все 8 ручек.
    /// </summary>
    public static HashSet<ResizeHandle>? AllowedHandles(DesignObjectType type) =>
        type == DesignObjectType.Line ? LineHandles : null;

    public static IReadOnlyList<(ResizeHandle Handle, RectangleF Rect)> GetHandles(
        RectangleF bounds, float handleSize, HashSet<ResizeHandle>? allowedHandles = null)
    {
        var half = handleSize / 2f;
        var midX = bounds.Left + bounds.Width / 2f;
        var midY = bounds.Top + bounds.Height / 2f;

        RectangleF At(float cx, float cy) => new(cx - half, cy - half, handleSize, handleSize);

        var candidates = new[]
        {
            (ResizeHandle.TopLeft, At(bounds.Left, bounds.Top)),
            (ResizeHandle.Top, At(midX, bounds.Top)),
            (ResizeHandle.TopRight, At(bounds.Right, bounds.Top)),
            (ResizeHandle.Right, At(bounds.Right, midY)),
            (ResizeHandle.BottomRight, At(bounds.Right, bounds.Bottom)),
            (ResizeHandle.Bottom, At(midX, bounds.Bottom)),
            (ResizeHandle.BottomLeft, At(bounds.Left, bounds.Bottom)),
            (ResizeHandle.Left, At(bounds.Left, midY)),
        };

        if (allowedHandles is null) return candidates;

        var filtered = new List<(ResizeHandle, RectangleF)>();
        foreach (var c in candidates)
            if (allowedHandles.Contains(c.Item1)) filtered.Add(c);
        return filtered;
    }

    /// <summary>Возвращает ручку под точкой (в тех же координатах, что и <paramref name="bounds"/>), если есть.</summary>
    public static ResizeHandle? HitTest(RectangleF bounds, float handleSize, PointF point, HashSet<ResizeHandle>? allowedHandles = null)
    {
        foreach (var (handle, rect) in GetHandles(bounds, handleSize, allowedHandles))
        {
            if (rect.Contains(point)) return handle;
        }
        return null;
    }

    /// <summary>
    /// Применяет перетаскивание ручки <paramref name="handle"/> на дельту (px) к исходному
    /// прямоугольнику, не позволяя сторонам схлопнуться меньше <paramref name="minSizePx"/>.
    /// </summary>
    public static RectangleF ApplyResize(RectangleF original, ResizeHandle handle, float dxPx, float dyPx, float minSizePx)
    {
        float left = original.Left, top = original.Top, right = original.Right, bottom = original.Bottom;

        switch (handle)
        {
            case ResizeHandle.TopLeft: left += dxPx; top += dyPx; break;
            case ResizeHandle.Top: top += dyPx; break;
            case ResizeHandle.TopRight: right += dxPx; top += dyPx; break;
            case ResizeHandle.Right: right += dxPx; break;
            case ResizeHandle.BottomRight: right += dxPx; bottom += dyPx; break;
            case ResizeHandle.Bottom: bottom += dyPx; break;
            case ResizeHandle.BottomLeft: left += dxPx; bottom += dyPx; break;
            case ResizeHandle.Left: left += dxPx; break;
        }

        // Минимальный размер применяем только к той стороне, которую реально двигает ручка —
        // иначе, например, у линии (Height = 0, ручки только Left/Right) высота всё равно
        // "подскакивала" бы до minSizePx при любом изменении длины, хотя её никто не трогал.
        var affectsWidth = handle is ResizeHandle.Left or ResizeHandle.Right
            or ResizeHandle.TopLeft or ResizeHandle.TopRight or ResizeHandle.BottomLeft or ResizeHandle.BottomRight;
        var affectsHeight = handle is ResizeHandle.Top or ResizeHandle.Bottom
            or ResizeHandle.TopLeft or ResizeHandle.TopRight or ResizeHandle.BottomLeft or ResizeHandle.BottomRight;

        if (affectsWidth && right - left < minSizePx)
        {
            var isLeftHandle = handle is ResizeHandle.Left or ResizeHandle.TopLeft or ResizeHandle.BottomLeft;
            if (isLeftHandle) left = right - minSizePx;
            else right = left + minSizePx;
        }
        if (affectsHeight && bottom - top < minSizePx)
        {
            var isTopHandle = handle is ResizeHandle.Top or ResizeHandle.TopLeft or ResizeHandle.TopRight;
            if (isTopHandle) top = bottom - minSizePx;
            else bottom = top + minSizePx;
        }

        return RectangleF.FromLTRB(left, top, right, bottom);
    }

    /// <summary>
    /// Прижимает прямоугольник к вертикальным границам полосы [0, bandHeightPx] — объект не
    /// может физически покинуть свою полосу (в сервисе нет репарентинга между полосами).
    /// Если высота объекта больше полосы, схлопывает его на всю высоту полосы.
    /// </summary>
    public static RectangleF ClampVertical(RectangleF bounds, float bandHeightPx)
    {
        var top = bounds.Top;
        var bottom = bounds.Bottom;

        if (bottom - top > bandHeightPx)
        {
            top = 0;
            bottom = bandHeightPx;
        }
        else
        {
            if (top < 0)
            {
                bottom -= top;
                top = 0;
            }
            if (bottom > bandHeightPx)
            {
                var overflow = bottom - bandHeightPx;
                top -= overflow;
                bottom -= overflow;
            }
        }

        return new RectangleF(bounds.Left, top, bounds.Width, bottom - top);
    }
}
