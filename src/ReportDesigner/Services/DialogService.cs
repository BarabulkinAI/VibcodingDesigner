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
}
