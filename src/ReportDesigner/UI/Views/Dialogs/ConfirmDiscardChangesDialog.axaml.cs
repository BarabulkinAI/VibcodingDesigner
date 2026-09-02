using Avalonia.Controls;
using Avalonia.Interactivity;
using ReportDesigner.Services;

namespace ReportDesigner.UI.Views.Dialogs;

public partial class ConfirmDiscardChangesDialog : Window
{
    public ConfirmDiscardChangesDialog() : this("документ")
    {
    }

    public ConfirmDiscardChangesDialog(string documentDisplayName)
    {
        InitializeComponent();
        MessageText.Text = $"В документе «{documentDisplayName}» есть несохранённые изменения. " +
                            "Сохранить их перед продолжением?";
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => Close(DiscardChangesResult.Save);
    private void OnDiscardClick(object? sender, RoutedEventArgs e) => Close(DiscardChangesResult.Discard);
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(DiscardChangesResult.Cancel);
}
