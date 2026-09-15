using Dock.Model.Mvvm.Controls;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Docking;

/// <summary>
/// Обёртка над превью документа. В отличие от остальных панелей видимость управляется не
/// стандартным крестиком Dock (CanClose), а чекбоксом «Вид → Панель превью»
/// (<see cref="MainViewModel.IsPreviewVisible"/>) — единственный источник истины, чтобы не
/// расходиться с крестиком закрытия панели.
/// </summary>
public sealed class PreviewTool : Tool
{
    public MainViewModel ViewModel { get; }

    public PreviewTool(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        Id = "Preview";
        Title = "Превью";
        CanClose = false;
    }
}
