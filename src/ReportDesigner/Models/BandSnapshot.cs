namespace ReportDesigner.Models;

/// <summary>
/// Снимок полосы (band) отчёта. Полосы выстраиваются вертикально сверху вниз
/// в порядке добавления; <see cref="Top"/> — накопленная высота предыдущих полос.
/// </summary>
public sealed class BandSnapshot
{
    public required string Name { get; init; }
    public required BandKind Kind { get; init; }
    /// <summary>Верхняя граница полосы в px от верха страницы.</summary>
    public float Top { get; init; }
    /// <summary>Высота полосы в px.</summary>
    public float Height { get; init; }
    public IReadOnlyList<DesignObjectInfo> Objects { get; init; } = Array.Empty<DesignObjectInfo>();
}