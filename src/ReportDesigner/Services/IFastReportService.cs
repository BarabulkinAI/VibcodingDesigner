using FastReport;

namespace ReportDesigner.Services;

public interface IFastReportService
{
    Report CurrentReport { get; }
    void CreateNew();
    void Load(string path);
    void Save(string path);
    void AddTextToDataBand(string text, float xCm, float yCm, float wCm, float hCm);
}