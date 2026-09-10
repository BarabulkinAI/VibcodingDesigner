namespace ReportDesigner.Models;

/// <summary>Графические объекты, которые умеет размещать дизайнер.</summary>
public enum DesignObjectType
{
    Text,
    Line,
    Shape,
    Picture
}

/// <summary>Виды полос (band) отчёта.</summary>
public enum BandKind
{
    ReportTitle,
    PageHeader,
    ColumnHeader,
    GroupHeader,
    Data,
    GroupFooter,
    ColumnFooter,
    PageFooter,
    ReportSummary,
    Overlay,
    Child
}

public enum DesignTextAlign
{
    Left,
    Center,
    Right
}

public enum DesignVerticalAlign
{
    Top,
    Middle,
    Bottom
}

public enum DesignShapeKind
{
    Rectangle,
    Ellipse,
    Diamond,
    Triangle,
    RoundedRectangle
}

/// <summary>Пресет размера бумаги (книжная ориентация, мм) — см. «Файл → Параметры страницы».</summary>
public enum PageSizePreset
{
    A4,
    A3
}