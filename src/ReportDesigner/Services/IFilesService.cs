namespace ReportDesigner.Services;

/// <summary>Диалоги открытия/сохранения файла отчёта (*.frx).</summary>
public interface IFilesService
{
    /// <summary>Показывает диалог "Открыть"; возвращает путь либо null при отмене.</summary>
    Task<string?> PickOpenReportPathAsync();

    /// <summary>Показывает диалог "Сохранить как"; возвращает путь либо null при отмене.</summary>
    Task<string?> PickSaveReportPathAsync(string? suggestedFileName);

    /// <summary>Показывает диалог выбора файла изображения; возвращает путь либо null при отмене.</summary>
    Task<string?> PickImagePathAsync();

    /// <summary>Показывает диалог "Сохранить как" для экспорта в PNG; возвращает путь либо null при отмене.</summary>
    Task<string?> PickExportPngPathAsync(string? suggestedFileName);

    /// <summary>Показывает диалог "Сохранить как" для экспорта в HTML; возвращает путь либо null при отмене.</summary>
    Task<string?> PickExportHtmlPathAsync(string? suggestedFileName);
}
