using ReportDesigner.Models;

namespace ReportDesigner.Services;

public enum DiscardChangesResult { Save, Discard, Cancel }

/// <summary>Диалоги, требующие подтверждения пользователя.</summary>
public interface IDialogService
{
    /// <summary>Спрашивает, что делать с несохранёнными изменениями перед New/Open/Close.</summary>
    Task<DiscardChangesResult> ConfirmDiscardChangesAsync(string documentDisplayName);

    /// <summary>Диалог «Параметры страницы» — предзаполняется текущими значениями, возвращает
    /// новый выбор или null при отмене.</summary>
    Task<(PageSizePreset Preset, bool Landscape)?> ChoosePageSizeAsync(PageSizePreset currentPreset, bool currentLandscape);
}
