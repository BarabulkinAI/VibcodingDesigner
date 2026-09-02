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
}
