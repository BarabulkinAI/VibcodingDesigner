using System.Drawing;
using FastReport;
using FastReport.Utils;
using ReportDesigner.Models;

namespace ReportDesigner.Services;

/// <summary>
/// Единственная точка доступа к модели отчёта (FastReport). Все мутации (создание,
/// перемещение, ресайз, удаление объектов и полос) выполняются здесь, после чего
/// канвас/дерево перестраивают свой снимок через <see cref="GetSnapshot"/>.
/// </summary>
public class FastReportService : IFastReportService
{
    public Report CurrentReport { get; private set; } = new();

    public void CreateNew()
    {
        CurrentReport = new Report();
        var page = new ReportPage { Name = EnsureUniqueComponentName("Page") };
        page.PaperHeight = 297; // A4, мм
        page.PaperWidth = 210;  // A4, мм
        CurrentReport.Pages.Add(page);

        AddBand(BandKind.ReportTitle, 2f);
        AddBand(BandKind.PageHeader, 2f);
        AddBand(BandKind.Data, 2f);
        AddBand(BandKind.PageFooter, 2f);
        AddBand(BandKind.ReportSummary, 2f);
    }

    public void Load(string path)
    {
        var report = new Report();
        report.Load(path); // FastReport сам пересоберёт зависимости
        CurrentReport = report;
    }

    public void Save(string path) => CurrentReport.Save(path);

    public DesignSnapshot GetSnapshot() => DesignSnapshotBuilder.Build(CurrentReport);

    // ------------------------------------------------------------------
    // Полосы
    // ------------------------------------------------------------------

    public string AddBand(BandKind kind, float heightCm = 2f, string? bandName = null)
    {
        var page = GetFirstPage();
        var height = heightCm * Units.Centimeters;

        Base bandBase = kind switch
        {
            BandKind.ReportTitle => new ReportTitleBand { Height = height },
            BandKind.ReportSummary => new ReportSummaryBand { Height = height },
            BandKind.PageHeader => new PageHeaderBand { Height = height },
            BandKind.PageFooter => new PageFooterBand { Height = height },
            BandKind.ColumnHeader => new ColumnHeaderBand { Height = height },
            BandKind.ColumnFooter => new ColumnFooterBand { Height = height },
            BandKind.GroupHeader => new GroupHeaderBand { Height = height },
            BandKind.GroupFooter => new GroupFooterBand { Height = height },
            BandKind.Child => new ChildBand { Height = height },
            BandKind.Overlay => new OverlayBand { Height = height },
            _ => new DataBand { Height = height },
        };

        var band = bandBase as BandBase;
        if (band == null)
            throw new ArgumentException($"Тип {kind} не является полосой.", nameof(kind));

        band.Name = bandName ?? EnsureUniqueComponentName(kind.ToString());
        InsertBandInOrder(page, band, kind);
        return band.Name;
    }

    public void RemoveBand(string bandName)
    {
        var page = GetFirstPage();       
        BandBase? band = null;
        foreach (BandBase item in page.Bands)
        {
            if(item.Name == bandName)
            {
                band = item; break;
            }
        }
         if(band == null) throw new KeyNotFoundException($"Полоса '{bandName}' не найдена.");
        page.Bands.Remove(band);
    }

    // ------------------------------------------------------------------
    // Объекты
    // ------------------------------------------------------------------

    public string AddObject(DesignObjectType type, float leftCm, float topCm, float widthCm, float heightCm, string? bandName = null)
    {
        var band = FindBand(bandName);
        var obj = CreateObject(type, leftCm, topCm, widthCm, heightCm);
        obj.Name = EnsureUniqueComponentName(DefaultName(type));
        band.Objects.Add(obj);
        return obj.Name;
    }

    public void MoveObject(string objectName, float leftCm, float topCm)
    {
        var obj = FindObject(objectName);
        obj.Left = leftCm * Units.Centimeters;
        obj.Top = topCm * Units.Centimeters;
    }

    public void ResizeObject(string objectName, float widthCm, float heightCm)
    {
        var obj = FindObject(objectName);
        obj.Width = widthCm * Units.Centimeters;
        obj.Height = heightCm * Units.Centimeters;
    }

    public void DeleteObject(string objectName)
    {
        foreach (var pageBase in CurrentReport.Pages)
        {
            if (pageBase is not ReportPage page) continue;
            foreach (BandBase band in page.Bands)
            {
                foreach (var baseObj in band.Objects)
                {
                    if (baseObj is ReportComponentBase obj && obj.Name == objectName)
                    {
                        band.Objects.Remove(obj);
                        return;
                    }
                }
            }
        }
        throw new KeyNotFoundException($"Объект '{objectName}' не найден.");
    }

    public void AddTextToDataBand(string text, float xCm, float yCm, float wCm, float hCm)
    {
        var band = FindBand();
        var name = AddObject(DesignObjectType.Text, xCm, yCm, wCm, hCm, band.Name);
        if (FindObject(name) is TextObject textObject)
            textObject.Text = text;
    }

    // ------------------------------------------------------------------
    // Внутреннее
    // ------------------------------------------------------------------

    private ReportPage GetFirstPage() =>
        CurrentReport.Pages.OfType<ReportPage>().FirstOrDefault()
        ?? throw new InvalidOperationException("Отчёт не содержит страниц. Сначала вызовите CreateNew().");

    private BandBase FindBand(string? bandName = null)
    {
        var page = GetFirstPage();
        if (bandName != null)
        {
            BandBase? band = null;
            foreach (BandBase baseBand in page.Bands) 
            { 
                if(baseBand.Name == bandName)
                {
                    band = baseBand; break;
                }
            }
            if (band != null) return band;
            throw new KeyNotFoundException($"Полоса '{bandName}' не найдена.");
        }

        return page.Bands.OfType<DataBand>().FirstOrDefault() ?? page.Bands[0];
    }

    private ReportComponentBase FindObject(string objectName)
    {
        foreach (var pageBase in CurrentReport.Pages)
        {
            if (pageBase is not ReportPage page) continue;
            foreach (BandBase band in page.Bands)
            {
                foreach (var baseObj in band.Objects)
                {
                    if (baseObj is ReportComponentBase obj && obj.Name == objectName)
                        return obj;
                }
            }
        }
        throw new KeyNotFoundException($"Объект '{objectName}' не найден.");
    }

    private ReportComponentBase CreateObject(DesignObjectType type, float leftCm, float topCm, float widthCm, float heightCm)
    {
        var bounds = new RectangleF(
            leftCm * Units.Centimeters, topCm * Units.Centimeters,
            widthCm * Units.Centimeters, heightCm * Units.Centimeters);

        return type switch
        {
            DesignObjectType.Text => CreateTextObject(bounds),
            DesignObjectType.Line => CreateLineObject(bounds),
            DesignObjectType.Shape => CreateShapeObject(bounds),
            DesignObjectType.Picture => new PictureObject { Bounds = bounds },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    private static TextObject CreateTextObject(RectangleF bounds) => new()
    {
        Bounds = bounds,
        Text = "Текст",
        Font = new Font("Arial", 10),
        Border = { Lines = BorderLines.All },
    };

    private static LineObject CreateLineObject(RectangleF bounds) => new()
    {
        Bounds = bounds,
        StartPoint = new PointF(0, 0),
        EndPoint = new PointF(bounds.Width, 0),
        LineWidth = 2,
        LineColor = Color.Black,
    };

    private static ShapeObject CreateShapeObject(RectangleF bounds)
    {
        var obj = new ShapeObject
        {
            Bounds = bounds,
            Shape = ShapeKind.Rectangle,
        };
        obj.FillColor = Color.White;
        return obj;
    }

    private static string DefaultName(DesignObjectType type) => type switch
    {
        DesignObjectType.Text => "Text",
        DesignObjectType.Line => "Line",
        DesignObjectType.Shape => "Shape",
        DesignObjectType.Picture => "Picture",
        _ => "Object",
    };

    private string EnsureUniqueComponentName(string baseName)
    {
        if (!ComponentExists(baseName)) return baseName;
        for (var i = 1; ; i++)
        {
            var candidate = $"{baseName}{i}";
            if (!ComponentExists(candidate)) return candidate;
        }
    }

    private bool ComponentExists(string name)
    {
        foreach (PageBase pageBase in CurrentReport.Pages)
        {
            if (pageBase.Name == name) return true;
            if (pageBase is not ReportPage page) continue;
            foreach (BandBase band in page.Bands)
            {
                if (band.Name == name) return true;
                foreach (var baseObj in band.Objects)
                {
                    if (baseObj is ReportComponentBase obj && obj.Name == name) return true;
                }
            }
        }
        return false;
    }

    private static void InsertBandInOrder(ReportPage page, BandBase band, BandKind kind)
    {
        var rank = BandRank(kind);
        for (var index = 0; index < page.Bands.Count; index++)
        {
            if (BandRank(DesignSnapshotBuilder.ToBandKind(page.Bands[index])) > rank)
            {
                page.Bands.Insert(index, band);
                return;
            }
        }
        page.Bands.Add(band);
    }

    private static int BandRank(BandKind kind) => kind switch
    {
        BandKind.ReportTitle => 0,
        BandKind.PageHeader => 1,
        BandKind.ColumnHeader => 2,
        BandKind.GroupHeader => 3,
        BandKind.Data => 4,
        BandKind.GroupFooter => 5,
        BandKind.ColumnFooter => 6,
        BandKind.PageFooter => 7,
        BandKind.ReportSummary => 8,
        BandKind.Overlay => 9,
        BandKind.Child => 10,
        _ => 4,
    };
}