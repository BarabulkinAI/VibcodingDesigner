using FastReport;
using FastReport.Export.Html;
using FastReport.Export.Image;

namespace ReportDesigner.Services;

/// <summary>Экспорт отчёта в распространённые форматы через движок FastReport.</summary>
public class ExportService : IExportService
{   
    public void ExportPng(Report report, string path, int resolution = 96)
    {
        var export = new ImageExport
        {
            ImageFormat = ImageExportFormat.Png,
            Resolution = resolution,
            SeparateFiles = false,
        };

        using var stream = File.Create(path);
        report.Prepare();
        report.Export(export, stream);
    }

    public void ExportHtml(Report report, string path)
    {
        using var stream = File.Create(path);
        report.Prepare();
        report.Export(new HTMLExport(), stream);
    }
}