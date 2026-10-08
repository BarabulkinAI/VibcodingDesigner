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
    public static DesignSnapshot Build(Report report, string? activePageName = null)
    {
        var pages = new List<PageSnapshot>();
        foreach (var pageBase in report.Pages)
        {
            if (pageBase is not ReportPage page) continue;
            pages.Add(BuildPage(page));
        }
        return new DesignSnapshot { Pages = pages, ActivePageName = activePageName };
    }

    private static PageSnapshot BuildPage(ReportPage page)
    {
        // Реальный движок FastReport (Report.Prepare()/экспорт/превью) рисует полосы страницы со
        // сдвигом на LeftMargin/TopMargin от края бумаги — они НЕ являются частью самих полос,
        // это чистый оффсет страницы. Топ первой полосы начинаем с TopMargin (а не с 0), тогда
        // весь остальной код, читающий BandSnapshot.Top, автоматически получает уже готовую
        // page-space координату (см. DesignSurface.ToPageBounds/SnapshotHitTester — они и так
        // всегда используют band.Top как есть). Для X аналогичного "накопителя" нет — левое поле
        // прокидывается отдельным полем PageSnapshot.MarginLeft, его прибавляют/вычитают там, где
        // band-относительный X переводится в/из координат страницы (см. PageSnapshot.MarginLeft).
        var marginLeftPx = UnitConverter.MmToPx(page.LeftMargin);
        var marginTopPx = UnitConverter.MmToPx(page.TopMargin);
        var marginBottomPx = UnitConverter.MmToPx(page.BottomMargin);
        var pageHeightPx = UnitConverter.MmToPx(page.PaperHeight);

        // PageFooter реальный движок всегда прижимает к низу листа; ReportSummary — только когда
        // у него явно выставлен BandBase.PrintOnBottom (без флага печатается сразу после
        // ColumnFooter/данных, как обычная полоса потока) — подтверждено эмпирически побайтовым
        // анализом экспортированного PNG (см. docs/ARCHITECTURE.md, «Бэклог»).
        var pinnedToBottom = new List<BandBase>();
        if (page.ReportSummary is { PrintOnBottom: true } summary) pinnedToBottom.Add(summary);
        if (page.PageFooter is { } footer) pinnedToBottom.Add(footer);

        var bands = new List<BandSnapshot>();
        var topPx = marginTopPx;
        foreach (BandBase band in EnumerateTopFlowBands(page, pinnedToBottom))
        {
            var heightPx = ToPx(band.Height);
            bands.Add(BuildBand(band, topPx, heightPx));
            topPx += heightPx;
        }

        // Прижатые к низу полосы начинаются с (низ листа − их суммарная высота), но не выше
        // topPx — если полос сверху набралось больше, чем остаётся места до низа страницы, они
        // идут сразу после них, а не заезжают друг на друга.
        var pinnedHeightPx = pinnedToBottom.Sum(b => ToPx(b.Height));
        var pinnedTopPx = Math.Max(topPx, pageHeightPx - marginBottomPx - pinnedHeightPx);
        foreach (var band in pinnedToBottom)
        {
            var heightPx = ToPx(band.Height);
            bands.Add(BuildBand(band, pinnedTopPx, heightPx));
            pinnedTopPx += heightPx;
        }

        if (page.Overlay is { } overlay)
            bands.Add(BuildBand(overlay, topPx, ToPx(overlay.Height))); // оверлей накладывается поверх, его позиция в потоке не критична

        return new PageSnapshot
        {
            Name = page.Name,
            Width = UnitConverter.MmToPx(page.PaperWidth),
            Height = pageHeightPx,
            MarginLeft = marginLeftPx,
            Bands = bands,
        };
    }

    private static BandSnapshot BuildBand(BandBase band, float topPx, float heightPx) => new()
    {
        Name = band.Name,
        Kind = ToBandKind(band),
        Top = topPx,
        Height = heightPx,
        DataSourceName = (band as DataBand)?.DataSource?.Name,
        Objects = BuildObjects(band),
    };

    /// <summary>
    /// Возвращает полосы, идущие обычным потоком сверху вниз (без тех, что прижаты к низу
    /// страницы — см. <paramref name="pinnedToBottom"/>). Специальные полосы хранятся в отдельных
    /// свойствах <see cref="ReportPage"/>, а data/group полосы — в коллекции
    /// <see cref="ReportPage.Bands"/>. ColumnFooter печатается сразу после данных колонки, ПЕРЕД
    /// ReportSummary — подтверждено эмпирически (в самих свойствах <see cref="ReportPage"/>
    /// порядок обратный), поэтому порядок здесь отличается от объявления свойств.
    /// </summary>
    private static IEnumerable<BandBase> EnumerateTopFlowBands(ReportPage page, List<BandBase> pinnedToBottom)
    {
        if (page.ReportTitle != null) yield return page.ReportTitle;
        if (page.PageHeader != null) yield return page.PageHeader;
        if (page.ColumnHeader != null) yield return page.ColumnHeader;
        foreach (BandBase band in page.Bands)
            yield return band;
        if (page.ColumnFooter != null) yield return page.ColumnFooter;
        if (page.ReportSummary is { } summary && !pinnedToBottom.Contains(summary))
            yield return summary;
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