using System.Drawing;

namespace ReportDesigner.Models;

/// <summary>
/// Снимок графического объекта отчёта в логических пикселях (96 DPI).
/// <see cref="Bounds"/> задаётся относительно полосы (band), на которой лежит объект.
/// </summary>
public sealed class DesignObjectInfo
{
    public required string Name { get; init; }
    public required DesignObjectType Type { get; init; }
    public required RectangleF Bounds { get; init; }
    public bool Visible { get; init; } = true;

    // --- Общие для текста и фигур ---
    public Color TextColor { get; init; } = Color.Black;
    public bool ShowBorder { get; init; }
    public float BorderWidth { get; init; } = 1f;
    public Color BorderColor { get; init; } = Color.Black;

    // --- Текст ---
    public string? Text { get; init; }
    public string FontName { get; init; } = "Arial";
    public float FontSize { get; init; } = 10f;
    public bool FontBold { get; init; }
    public bool FontItalic { get; init; }
    public DesignTextAlign HorizontalAlign { get; init; } = DesignTextAlign.Left;
    public DesignVerticalAlign VerticalAlign { get; init; } = DesignVerticalAlign.Top;

    // --- Линия ---
    public float LineWidth { get; init; } = 2f;
    public Color LineColor { get; init; } = Color.Black;

    // --- Фигура ---
    public DesignShapeKind Shape { get; init; } = DesignShapeKind.Rectangle;
    public Color FillColor { get; init; } = Color.White;

    // --- Картинка ---
    public bool HasImage { get; init; }
}