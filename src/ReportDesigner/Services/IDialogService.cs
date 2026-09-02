namespace ReportDesigner.Services;

public enum DiscardChangesResult { Save, Discard, Cancel }

/// <summary>Диалоги, требующие подтверждения пользователя.</summary>
public interface IDialogService
{
    /// <summary>Спрашивает, что делать с несохранёнными изменениями перед New/Open/Close.</summary>
    Task<DiscardChangesResult> ConfirmDiscardChangesAsync(string documentDisplayName);
}
