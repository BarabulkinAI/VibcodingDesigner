using System.Drawing;
using FastReport;
using FastReport.Utils;

namespace ReportDesigner.Services;

public class FastReportService : IFastReportService
{
    public Report CurrentReport { get; private set; } = new();

    public void CreateNew()
    {
        CurrentReport = new Report();
        var page = new ReportPage { Name = "Page1" };
        page.PaperHeight = 297; // format A4
        page.PaperWidth = 210; // format A4
        CurrentReport.Pages.Add(page);

        var dataBand = new DataBand { Name = "Data1", Height = Units.Centimeters * 2 };
        page.Bands.Add(dataBand);
    }

    public void AddTextToDataBand(string text, float xCm, float yCm, float wCm, float hCm)
    {
        var page = CurrentReport.Pages[0] as ReportPage;
        var band = page?.Bands.OfType<DataBand>().FirstOrDefault();
        if (band == null) return;

        var obj = new TextObject
        {
            Name = "Text" + Guid.NewGuid().ToString("N")[..4],
            Bounds = new RectangleF(
                xCm * Units.Centimeters, yCm * Units.Centimeters,
                wCm * Units.Centimeters, hCm * Units.Centimeters),
            Text = text,
            Border = { Lines = BorderLines.All }
        };
        band.Objects.Add(obj);
    }

    public void Save(string path) => CurrentReport.Save(path);
    public void Load(string path) => CurrentReport.Load(path);
}