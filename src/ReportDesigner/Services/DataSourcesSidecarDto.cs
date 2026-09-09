namespace ReportDesigner.Services;

/// <summary>Формат JSON-файла, который хранит источники данных дизайнера рядом с .frx
/// (см. FastReportService.Save/Load/LoadAsTemplate). Намеренно отдельный от приватного
/// DataSourceDefinition — чтобы рефакторинг внутреннего представления не менял формат файла.</summary>
internal sealed class DataSourcesSidecarDto
{
    public List<DataSourceDto> Sources { get; set; } = new();

    /// <summary>Полоса (DataBand.Name) → имя источника.</summary>
    public Dictionary<string, string> BandAssignments { get; set; } = new();
}

internal sealed class DataSourceDto
{
    public required string Name { get; set; }
    public required List<string> Columns { get; set; }
    public required List<List<string>> Rows { get; set; }
}
