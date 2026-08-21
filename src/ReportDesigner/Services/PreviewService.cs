
using Avalonia.Media.Imaging;
using FastReport;
using FastReport.Export.Image;

namespace ReportDesigner.Services;

public class PreviewService : IPreviewService
{
    public Bitmap RenderPreview(Report report)
    {
        using var ms = new MemoryStream();
        var export = new ImageExport
        {
            ImageFormat = ImageExportFormat.Png,
            Resolution = 96,
            SeparateFiles = false,
            //SeparatePages = false
        };

        report.Prepare();
        report.Export(export, ms);
        ms.Position = 0;
        return new Bitmap(ms);
    }
}