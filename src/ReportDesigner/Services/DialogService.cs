using ReportDesigner.Models;
using ReportDesigner.UI.Views.Dialogs;

namespace ReportDesigner.Services;

public class DialogService : IDialogService
{
    private readonly IHostWindowProvider _hostWindowProvider;

    public DialogService(IHostWindowProvider hostWindowProvider)
    {
        _hostWindowProvider = hostWindowProvider;
    }

    public Task<DiscardChangesResult> ConfirmDiscardChangesAsync(string documentDisplayName)
    {
        var dialog = new ConfirmDiscardChangesDialog(documentDisplayName);
        return dialog.ShowDialog<DiscardChangesResult>(_hostWindowProvider.MainWindow);
    }

    public Task<(PageSizePreset Preset, bool Landscape)?> ChoosePageSizeAsync(PageSizePreset currentPreset, bool currentLandscape)
    {
        var dialog = new PageSetupDialog(currentPreset, currentLandscape);
        return dialog.ShowDialog<(PageSizePreset Preset, bool Landscape)?>(_hostWindowProvider.MainWindow);
    }
}
