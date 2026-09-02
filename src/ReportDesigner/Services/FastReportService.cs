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
    private string? _currentFilePath;
    private bool _isDirty;

    public Report CurrentReport { get; private set; } = new();

    /// <inheritdoc/>
    public string? CurrentFilePath => _currentFilePath;

    /// <inheritdoc/>
    public bool IsDirty => _isDirty;

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

        // Новый документ: пути ещё нет, несохранённых изменений нет.
        _currentFilePath = null;
        _isDirty = false;
    }

    public void Load(string path)
    {
        var report = new Report();
        report.Load(path); // FastReport сам пересоберёт зависимости
        CurrentReport = report;
        MarkSaved(path);
    }

    public void Save(string path)
    {
        CurrentReport.Save(path);
        MarkSaved(path);
    }

    /// <inheritdoc/>
    public void MarkSaved(string path)
    {
        _currentFilePath = path;
        _isDirty = false;
    }

    public DesignSnapshot GetSnapshot() => DesignSnapshotBuilder.Build(CurrentReport);

    // ------------------------------------------------------------------
    // Полосы
    // ------------------------------------------------------------------

    public string AddBand(BandKind kind, float heightCm = 2f, string? bandName = null)
    {
        var page = GetFirstPage();
        var height = heightCm * Units.Centimeters;

        BandBase band = kind switch
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

        band.Name = bandName ?? EnsureUniqueComponentName(kind.ToString());
        AttachBandToPage(page, band);
        _isDirty = true;
        return band.Name;
    }

    public void RemoveBand(string bandName)
    {
        var page = GetFirstPage();
        var band = EnumerateAllBands(page).FirstOrDefault(b => b.Name == bandName)
            ?? throw new KeyNotFoundException($"Полоса '{bandName}' не найдена.");
        page.RemoveChild(band);
        _isDirty = true;
    }

    /// <summary>
    /// Назначает полосу на страницу. Специальные полосы (титул, колонтитулы,
    /// сводка, оверлей) хранятся в отдельных свойствах <see cref="ReportPage"/>,
    /// а data/group полосы — в коллекции <see cref="ReportPage.Bands"/>.
    /// </summary>
    private static void AttachBandToPage(ReportPage page, BandBase band)
    {
        switch (band)
        {
            case ReportTitleBand b: page.ReportTitle = b; break;
            case ReportSummaryBand b: page.ReportSummary = b; break;
            case PageHeaderBand b: page.PageHeader = b; break;
            case PageFooterBand b: page.PageFooter = b; break;
            case ColumnHeaderBand b: page.ColumnHeader = b; break;
            case ColumnFooterBand b: page.ColumnFooter = b; break;
            case OverlayBand b: page.Overlay = b; break;
            default: page.Bands.Add(band); break;
        }
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
        _isDirty = true;
        return obj.Name;
    }

    public void MoveObject(string objectName, float leftCm, float topCm)
    {
        var obj = FindObject(objectName);
        obj.Left = leftCm * Units.Centimeters;
        obj.Top = topCm * Units.Centimeters;
        _isDirty = true;
    }

    public void ResizeObject(string objectName, float widthCm, float heightCm)
    {
        var obj = FindObject(objectName);
        obj.Width = widthCm * Units.Centimeters;
        obj.Height = heightCm * Units.Centimeters;
        _isDirty = true;
    }

    public void DeleteObject(string objectName)
    {
        foreach (var pageBase in CurrentReport.Pages)
        {
            if (pageBase is not ReportPage page) continue;
            foreach (var band in EnumerateAllBands(page))
            {
                foreach (var baseObj in band.Objects)
                {
                    if (baseObj is ReportComponentBase obj && obj.Name == objectName)
                    {
                        band.Objects.Remove(obj);
                        _isDirty = true;
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
    // Свойства объектов
    // ------------------------------------------------------------------

    public void SetText(string objectName, string text)
    {
        var obj = FindObject(objectName) as TextObject
            ?? throw new InvalidOperationException($"Объект '{objectName}' не является текстовым.");
        obj.Text = text;
        _isDirty = true;
    }

    public void SetFont(string objectName, string fontName, float fontSize, bool bold, bool italic)
    {
        var obj = FindObject(objectName) as TextObject
            ?? throw new InvalidOperationException($"Объект '{objectName}' не является текстовым.");
        var style = FontStyle.Regular;
        if (bold) style |= FontStyle.Bold;
        if (italic) style |= FontStyle.Italic;
        obj.Font = new Font(fontName, fontSize, style);
        _isDirty = true;
    }

    public void SetTextColor(string objectName, Color color)
    {
        var obj = FindObject(objectName) as TextObject
            ?? throw new InvalidOperationException($"Объект '{objectName}' не является текстовым.");
        obj.TextColor = color;
        _isDirty = true;
    }

    public void SetHorizontalAlign(string objectName, DesignTextAlign align)
    {
        var obj = FindObject(objectName) as TextObject
            ?? throw new InvalidOperationException($"Объект '{objectName}' не является текстовым.");
        obj.HorzAlign = align switch
        {
            DesignTextAlign.Center => HorzAlign.Center,
            DesignTextAlign.Right => HorzAlign.Right,
            _ => HorzAlign.Left,
        };
        _isDirty = true;
    }

    public void SetVerticalAlign(string objectName, DesignVerticalAlign align)
    {
        var obj = FindObject(objectName) as TextObject
            ?? throw new InvalidOperationException($"Объект '{objectName}' не является текстовым.");
        obj.VertAlign = align switch
        {
            DesignVerticalAlign.Middle => VertAlign.Center,
            DesignVerticalAlign.Bottom => VertAlign.Bottom,
            _ => VertAlign.Top,
        };
        _isDirty = true;
    }

    public void SetBorder(string objectName, bool show, float widthCm, Color color)
    {
        var obj = FindObject(objectName);
        obj.Border.Lines = show ? BorderLines.All : BorderLines.None;
        obj.Border.Width = widthCm * Units.Centimeters;
        obj.Border.Color = color;
        _isDirty = true;
    }

    public void SetFillColor(string objectName, Color color)
    {
        var obj = FindObject(objectName) as ShapeObject
            ?? throw new InvalidOperationException($"Объект '{objectName}' не является фигурой.");
        obj.FillColor = color;
        _isDirty = true;
    }

    public void SetShapeKind(string objectName, DesignShapeKind kind)
    {
        var obj = FindObject(objectName) as ShapeObject
            ?? throw new InvalidOperationException($"Объект '{objectName}' не является фигурой.");
        obj.Shape = kind switch
        {
            DesignShapeKind.Ellipse => ShapeKind.Ellipse,
            DesignShapeKind.Diamond => ShapeKind.Diamond,
            DesignShapeKind.Triangle => ShapeKind.Triangle,
            DesignShapeKind.RoundedRectangle => ShapeKind.RoundRectangle,
            _ => ShapeKind.Rectangle,
        };
        _isDirty = true;
    }

    public void SetLineStyle(string objectName, float widthCm, Color color)
    {
        var obj = FindObject(objectName) as LineObject
            ?? throw new InvalidOperationException($"Объект '{objectName}' не является линией.");
        obj.Border.Width = widthCm * Units.Centimeters;
        obj.Border.Color = color;
        _isDirty = true;
    }

    public void SetImage(string objectName, string imagePath)
    {
        var obj = FindObject(objectName) as PictureObject
            ?? throw new InvalidOperationException($"Объект '{objectName}' не является картинкой.");
        // Image.FromFile держит файл открытым, пока живёт возвращённый Image (GDI+ грузит его
        // лениво) — клонируем в независимый Bitmap и сразу освобождаем исходный, иначе файл
        // остаётся заблокированным на весь срок жизни документа.
        using var loaded = Image.FromFile(imagePath);
        obj.Image = new Bitmap(loaded);
        _isDirty = true;
    }

    public void ClearImage(string objectName)
    {
        var obj = FindObject(objectName) as PictureObject
            ?? throw new InvalidOperationException($"Объект '{objectName}' не является картинкой.");
        obj.Image = null;
        _isDirty = true;
    }

    public void SetVisible(string objectName, bool visible)
    {
        FindObject(objectName).Visible = visible;
        _isDirty = true;
    }

    public void SetName(string objectName, string newName)
    {
        var obj = FindObject(objectName);
        if (newName != objectName && ComponentExists(newName))
            throw new InvalidOperationException($"Имя '{newName}' уже используется.");
        obj.Name = newName;
        _isDirty = true;
    }

    // ------------------------------------------------------------------
    // Z-order
    // ------------------------------------------------------------------

    public void BringToFront(string objectName)
    {
        var (band, obj) = FindBandAndObject(objectName);
        MoveToZOrder(obj, obj.ZOrder, band.Objects.Count - 1);
        _isDirty = true;
    }

    public void SendToBack(string objectName)
    {
        var (_, obj) = FindBandAndObject(objectName);
        MoveToZOrder(obj, obj.ZOrder, 0);
        _isDirty = true;
    }

    public void MoveForward(string objectName)
    {
        var (band, obj) = FindBandAndObject(objectName);
        var current = obj.ZOrder;
        MoveToZOrder(obj, current, Math.Min(current + 1, band.Objects.Count - 1));
        _isDirty = true;
    }

    public void MoveBackward(string objectName)
    {
        var (_, obj) = FindBandAndObject(objectName);
        var current = obj.ZOrder;
        MoveToZOrder(obj, current, Math.Max(current - 1, 0));
        _isDirty = true;
    }

    /// <summary>
    /// Ставит объект на позицию <paramref name="desiredIndex"/> в коллекции его полосы.
    /// Сеттер <c>Base.ZOrder</c> у FastReport реализован как remove+insert относительно
    /// ТЕКУЩЕГО индекса объекта: если запрошенный индекс больше текущего, он сам вычитает 1
    /// (компенсируя сдвиг после удаления объекта из старой позиции) — поэтому, чтобы объект
    /// реально оказался на <paramref name="desiredIndex"/>, при движении вперёд нужно запросить
    /// на 1 больше. Проверено эмпирически на реальной сборке FastReport.OpenSource 2026.2.3.
    /// </summary>
    private static void MoveToZOrder(ReportComponentBase obj, int currentIndex, int desiredIndex) =>
        obj.ZOrder = desiredIndex > currentIndex ? desiredIndex + 1 : desiredIndex;

// ------------------------------------------------------------------
    // Внутреннее
    // ------------------------------------------------------------------

    private ReportPage GetFirstPage() =>
        CurrentReport.Pages.OfType<ReportPage>().FirstOrDefault()
        ?? throw new InvalidOperationException("Отчёт не содержит страниц. Сначала вызовите CreateNew().");

    /// <summary>
    /// Возвращает все полосы страницы в вертикальном порядке отображения — и из
    /// специальных свойств (<see cref="ReportPage"/>), и из коллекции <see cref="ReportPage.Bands"/>.
    /// </summary>
    private static IEnumerable<BandBase> EnumerateAllBands(ReportPage page)
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

    private BandBase FindBand(string? bandName = null)
    {
        var page = GetFirstPage();
        if (bandName != null)
        {
            var band = EnumerateAllBands(page).FirstOrDefault(b => b.Name == bandName)
                ?? throw new KeyNotFoundException($"Полоса '{bandName}' не найдена.");
            return band;
        }
        return EnumerateAllBands(page).OfType<DataBand>().FirstOrDefault()
            ?? throw new InvalidOperationException("В отчёте нет полосы данных. Добавьте её через AddBand(BandKind.Data).");
    }

    private ReportComponentBase FindObject(string objectName) => FindBandAndObject(objectName).Object;

    private (BandBase Band, ReportComponentBase Object) FindBandAndObject(string objectName)
    {
        foreach (var pageBase in CurrentReport.Pages)
        {
            if (pageBase is not ReportPage page) continue;
            foreach (var band in EnumerateAllBands(page))
            {
                foreach (var baseObj in band.Objects)
                {
                    if (baseObj is ReportComponentBase obj && obj.Name == objectName)
                        return (band, obj);
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
        Diagonal = false, // горизонтальная линия слева направо
        Border = { Lines = BorderLines.All, Width = 2f, Color = Color.Black },
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
            foreach (var band in EnumerateAllBands(page))
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
}
