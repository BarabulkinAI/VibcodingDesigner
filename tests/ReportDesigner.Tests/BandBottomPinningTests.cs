using System.Linq;
using ReportDesigner.Models;
using ReportDesigner.Services;
using Xunit;

namespace ReportDesigner.Tests;

/// <summary>ReportSummary и PageFooter должны лежать внизу страницы — и в снимке канваса
/// (<see cref="DesignSnapshotBuilder"/>), и в реальной печати движка FastReport. См.
/// docs/ARCHITECTURE.md, «Бэклог».</summary>
public class BandBottomPinningTests
{
    private const uint Yellow = 0xFFFFFF00u;
    private const uint Cyan = 0xFF00FFFFu;

    [Fact]
    public void ExportPng_ReportSummaryAndPageFooter_PrintedAtBottomOfPage_MatchingCanvasSnapshot()
    {
        var service = new FastReportService();
        service.CreateNew();
        var page = service.GetSnapshot().Pages[0];
        var reportSummary = page.Bands.Single(b => b.Kind == BandKind.ReportSummary);
        var pageFooter = page.Bands.Single(b => b.Kind == BandKind.PageFooter);

        var summaryShape = service.AddObject(DesignObjectType.Shape, 0, 0, 8, UnitConverter.PxToCm(reportSummary.Height), reportSummary.Name);
        service.SetFillColor(summaryShape, System.Drawing.Color.Yellow);
        var footerShape = service.AddObject(DesignObjectType.Shape, 0, 0, 8, UnitConverter.PxToCm(pageFooter.Height), pageFooter.Name);
        service.SetFillColor(footerShape, System.Drawing.Color.Cyan);

        var path = Path.Combine(Path.GetTempPath(), $"bottom_pin_{Guid.NewGuid():N}.png");
        try
        {
            new ExportService().ExportPng(service.CurrentReport, path);
            using var bitmap = SkiaSharp.SKBitmap.Decode(path);

            // Печать должна совпасть со снимком канваса: жёлтая сводка начинается на её Top,
            // голубой футер — на его Top (вплотную под сводкой), оба заканчиваются у низа листа.
            Assert.Equal(Yellow, (uint)bitmap.GetPixel(3, (int)reportSummary.Top + 5));
            Assert.Equal(Cyan, (uint)bitmap.GetPixel(3, (int)pageFooter.Top + 5));
            Assert.Equal(Cyan, (uint)bitmap.GetPixel(3, bitmap.Height - 5)); // футер у самого низа листа
        }
        finally
        {
            File.Delete(path);
        }
    }
}
