using Dock.Model.Mvvm.Controls;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Docking;

public sealed class PropertiesTool : Tool
{
    public PropertiesPanelViewModel ViewModel { get; }

    public PropertiesTool(PropertiesPanelViewModel viewModel)
    {
        ViewModel = viewModel;
        Id = "Properties";
        Title = "Свойства";
        CanClose = false;
    }
}
