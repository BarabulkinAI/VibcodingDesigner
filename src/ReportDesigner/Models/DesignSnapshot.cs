namespace ReportDesigner.Models;

/// <summary>
/// Лёгкий снимок отчёта для отрисовки канваса и дерева объектов.
/// Физический источник правды — FastReport (<see cref="FastReport.Report"/>);
/// снимок перестраивается заново после каждого изменения.
/// </summary>
public sealed class DesignSnapshot
{
    public IReadOnlyList<PageSnapshot> Pages { get; init; } = Array.Empty<PageSnapshot>();
}