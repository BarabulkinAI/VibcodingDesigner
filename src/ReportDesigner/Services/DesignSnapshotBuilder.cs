using System.Drawing;
using FastReport;
using FastReport.Utils;
using ReportDesigner.Models;

namespace ReportDesigner.Services;

/// <summary>
/// Строит лёгкий снимок отчёта (<see cref="DesignSnapshot"/>) для отрисовки канваса
/// и дерева объектов. Все измерения выражаются в логических пикселях (96 DPI).
/// </summary>
public static class DesignSnapshotBuilder
{
    public static DesignSnapshot Build(Report report)
    {
        var pages = new List<PageSnapshot>();
        foreach (var pageBase in report.Pages)
        {
            if (pageBase is not ReportPage page) continue;
            pages.Add(BuildPage(page));
        }
        return new DesignSnapshot { Pages = pages };
    }

    private static PageSnapshot BuildPage(ReportPage page)
    {
        var bands = new List<BandSnapshot>();
        var topPx = 0f;
        foreach (BandBase band in EnumerateBands(page))
        {
            var heightPx = ToPx(band.Height);
            bands.Add(new BandSnapshot
            {
                Name = band.Name,
                Kind = ToBandKind(band),
                Top = topPx,
                Height = heightPx,
                DataSourceName = (band as DataBand)?.DataSource?.Name,
                Objects = BuildObjects(band),
            });
            topPx += heightPx;
        }

        return new PageSnapshot
        {
            Name = page.Name,
            Width = UnitConverter.MmToPx(page.PaperWidth),
            Height = UnitConverter.MmToPx(page.PaperHeight),
            Bands = bands,
        };
    }

    /// <summary>
    /// Возвращает все полосы страницы в вертикальном порядке отображения.
    /// Специальные полосы хранятся в отдельных свойствах <see cref="ReportPage"/>,
    /// а data/group полосы — в коллекции <see cref="ReportPage.Bands"/>.
    /// </summary>
    private static IEnumerable<BandBase> EnumerateBands(ReportPage page)
    {
        if (page.ReportTitle != null) yield return page.ReportTitle;
        if (page.PageHeader != null) yield return page.PageHeader;
        if (page.ColumnHeader != null) yield return page.ColumnHeader;
        foreach (BandBase band in page.Bands)
            yield return band;
        if (page.ReportSummary != null) yield return page.ReportSummary;
        if (page.ColumnFooter != null) yield return page.ColumnFooter;
        if (page.PageFooter != null) yield return page.PageFooter;
        if (page.Overlay != null) yield return page.Overlay;
    }

    private static List<DesignObjectInfo> BuildObjects(BandBase band)
    {
        var objects = new List<DesignObjectInfo>();
        foreach (var baseObj in band.Objects)
        {
            if (baseObj is not ReportComponentBase obj) continue;
            var info = BuildObject(obj);
            if (info != null) objects.Add(info);
        }
        return objects;
    }

    private static DesignObjectInfo? BuildObject(ReportComponentBase obj) => obj switch
    {
        TextObject t => new DesignObjectInfo
        {
            Name = t.Name,
            Type = DesignObjectType.Text,
            Bounds = ToBoundsPx(t),
            Visible = t.Visible,
            Text = t.Text,
            FontName = t.Font?.Name ?? "Arial",
            FontSize = t.Font?.Size ?? 10,
            FontBold = t.Font?.Bold ?? false,
            FontItalic = t.Font?.Italic ?? false,
            TextColor = t.TextColor/*TextFill?.FillColor ?? Color.Black*/,
            HorizontalAlign = MapHAlign(t.HorzAlign),
            VerticalAlign = MapVAlign(t.VertAlign),
            ShowBorder = t.Border?.Lines != BorderLines.None,
            BorderWidth = t.Border?.Width ?? 0,
            BorderColor = t.Border?.Color ?? Color.Black,
        },
        LineObject l => new DesignObjectInfo
        {
            Name = l.Name,
            Type = DesignObjectType.Line,
            Bounds = ToBoundsPx(l),
            Visible = l.Visible,
            LineWidth = l.Border?.Width ?? 1,
            LineColor = l.Border?.Color ?? Color.Black,
        },
        ShapeObject s => new DesignObjectInfo
        {
            Name = s.Name,
            Type = DesignObjectType.Shape,
            Bounds = ToBoundsPx(s),
            Visible = s.Visible,
            Shape = MapShape(s.Shape),
            FillColor = s.FillColor ,//?? Color.White,
            ShowBorder = s.Border?.Lines != BorderLines.None,
            BorderWidth = s.Border?.Width ?? 0,
            BorderColor = s.Border?.Color ?? Color.Black,
        },
        PictureObject p => new DesignObjectInfo
        {
            Name = p.Name,
            Type = DesignObjectType.Picture,
            Bounds = ToBoundsPx(p),
            Visible = p.Visible,
            HasImage = p.Image != null,
        },
        _ => null,
    };

    private static RectangleF ToBoundsPx(ReportComponentBase obj) =>
        new(ToPx(obj.Left), ToPx(obj.Top), ToPx(obj.Width), ToPx(obj.Height));

    /// <summary>Юниты FastReport → логические пиксели (96 DPI).</summary>
    public static float ToPx(float units) => units / /*Units.Pixels*/ 96 * UnitConverter.LogicalDpi;

    /// <summary>Логические пиксели → юниты FastReport.</summary>
    public static float ToUnits(float px) => px / UnitConverter.LogicalDpi * /*Units.Pixels*/ 96;

    internal static BandKind ToBandKind(Base band) => band switch
    {
        ReportTitleBand => BandKind.ReportTitle,
        ReportSummaryBand => BandKind.ReportSummary,
        PageHeaderBand => BandKind.PageHeader,
        PageFooterBand => BandKind.PageFooter,
        ColumnHeaderBand => BandKind.ColumnHeader,
        ColumnFooterBand => BandKind.ColumnFooter,
        GroupHeaderBand => BandKind.GroupHeader,
        GroupFooterBand => BandKind.GroupFooter,
        ChildBand => BandKind.Child,
        OverlayBand => BandKind.Overlay,
        _ => BandKind.Data,
    };

    private static DesignTextAlign MapHAlign(HorzAlign alignment) => alignment switch
    {
        HorzAlign.Center => DesignTextAlign.Center,
        HorzAlign.Right => DesignTextAlign.Right,        
        _ => DesignTextAlign.Left,
    };

    private static DesignVerticalAlign MapVAlign(VertAlign alignment) => alignment switch
    {
        VertAlign.Center => DesignVerticalAlign.Middle,
        VertAlign.Bottom => DesignVerticalAlign.Bottom,
        _ => DesignVerticalAlign.Top,
    };

    private static DesignShapeKind MapShape(ShapeKind shape) => shape switch
    {
        ShapeKind.Ellipse => DesignShapeKind.Ellipse,
        ShapeKind.Diamond => DesignShapeKind.Diamond,
        ShapeKind.Triangle => DesignShapeKind.Triangle,
        ShapeKind.RoundRectangle => DesignShapeKind.RoundedRectangle,
        _ => DesignShapeKind.Rectangle,
    };
}