namespace ReportDesigner.Models;

/// <summary>Снимок страницы отчёта в логических пикселях (96 DPI).</summary>
public sealed class PageSnapshot
{
    public required string Name { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    /// <summary>Левое поле страницы (<see cref="FastReport.ReportPage.LeftMargin"/>, мм→px) —
    /// реальный движок FastReport рисует содержимое полос со сдвигом на это значение от левого
    /// края бумаги, канвас должен повторять тот же сдвиг (см. также <see cref="BandSnapshot.Top"/>,
    /// который уже включает верхнее поле — там отдельного свойства не нужно, т.к. смещение
    /// накапливается один раз при построении снимка).</summary>
    public float MarginLeft { get; init; }
    public IReadOnlyList<BandSnapshot> Bands { get; init; } = Array.Empty<BandSnapshot>();
}