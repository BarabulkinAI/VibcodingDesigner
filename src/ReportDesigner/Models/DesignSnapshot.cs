namespace ReportDesigner.Models;

/// <summary>
/// Лёгкий снимок отчёта для отрисовки канваса и дерева объектов.
/// Физический источник правды — FastReport (<see cref="FastReport.Report"/>);
/// снимок перестраивается заново после каждого изменения.
/// </summary>
public sealed class DesignSnapshot
{
    public IReadOnlyList<PageSnapshot> Pages { get; init; } = Array.Empty<PageSnapshot>();

    /// <summary>Имя страницы, которую сейчас показывают канвас и дерево объектов.</summary>
    public string? ActivePageName { get; init; }

    /// <summary>Активная страница (по <see cref="ActivePageName"/>; если такой нет — первая),
    /// null для пустого отчёта. Всё, что рисует/ищет по странице, работает через неё, а не
    /// через <c>Pages[0]</c>.</summary>
    public PageSnapshot? ActivePage =>
        Pages.FirstOrDefault(p => p.Name == ActivePageName) ?? (Pages.Count > 0 ? Pages[0] : null);
}