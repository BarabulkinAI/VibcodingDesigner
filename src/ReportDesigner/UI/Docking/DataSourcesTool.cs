using Dock.Model.Mvvm.Controls;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Docking;

public sealed class DataSourcesTool : Tool
{
    public DataSourcesViewModel ViewModel { get; }

    public DataSourcesTool(DataSourcesViewModel viewModel)
    {
        ViewModel = viewModel;
        Id = "DataSources";
        Title = "Источники данных";
        CanClose = false;
    }
}
