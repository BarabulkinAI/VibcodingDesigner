using FastReport;

namespace ReportDesigner.Services;

public interface IExportService
{    
    /// <summary>Экспортирует отчёт в PNG заданным разрешением (число пикселей на дюйм).</summary>
    void ExportPng(Report report, string path, int resolution = 96);

    /// <summary>Экспортирует отчёт в HTML.</summary>
    void ExportHtml(Report report, string path);
}