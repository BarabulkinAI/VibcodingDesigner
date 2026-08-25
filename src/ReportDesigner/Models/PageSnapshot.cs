namespace ReportDesigner.Models;

/// <summary>Снимок страницы отчёта в логических пикселях (96 DPI).</summary>
public sealed class PageSnapshot
{
    public required string Name { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public IReadOnlyList<BandSnapshot> Bands { get; init; } = Array.Empty<BandSnapshot>();
}