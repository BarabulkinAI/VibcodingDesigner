using System.Drawing;
using ReportDesigner.Models;

namespace ReportDesigner.Services;

/// <summary>
/// Hit-test по <see cref="DesignSnapshot"/>: находит объект под точкой канваса (px, координаты
/// страницы). Полосы перебираются сверху вниз, объекты внутри полосы — от последнего к первому
/// (верхний по z-order объект побеждает).
/// </summary>
public static class SnapshotHitTester
{
    /// <summary>
    /// Допуск hit-test'а вокруг границ объекта, px. Нужен, потому что у линии высота (или
    /// ширина — для вертикальной) может быть ровно 0: <see cref="RectangleF.Contains(PointF)"/>
    /// у прямоугольника нулевой площади не матчит вообще ни одну точку, и без допуска такую
    /// линию невозможно выделить кликом.
    /// </summary>
    private const float HitTestTolerancePx = 3f;

    public static (string BandName, DesignObjectInfo Object)? FindObjectAt(DesignSnapshot snapshot, PointF pagePointPx)
    {
        if (FindBandAt(snapshot, pagePointPx) is not { } band) return null;

        var bandRelativePoint = new PointF(pagePointPx.X, pagePointPx.Y - band.Top);
        for (var i = band.Objects.Count - 1; i >= 0; i--)
        {
            var obj = band.Objects[i];
            if (obj.Visible && InflatedBounds(obj.Bounds).Contains(bandRelativePoint))
                return (band.Name, obj);
        }

        return null;
    }

    private static RectangleF InflatedBounds(RectangleF bounds)
    {
        var inflated = bounds;
        inflated.Inflate(HitTestTolerancePx, HitTestTolerancePx);
        return inflated;
    }

    /// <summary>Находит полосу под точкой канваса (без учёта объектов) — например, чтобы понять,
    /// в какую полосу попадёт новый объект, размещаемый через Toolbox.</summary>
    public static BandSnapshot? FindBandAt(DesignSnapshot snapshot, PointF pagePointPx)
    {
        if (snapshot.Pages.Count == 0) return null;
        var page = snapshot.Pages[0];

        foreach (var band in page.Bands)
        {
            if (pagePointPx.Y >= band.Top && pagePointPx.Y <= band.Top + band.Height)
                return band;
        }

        return null;
    }
}
